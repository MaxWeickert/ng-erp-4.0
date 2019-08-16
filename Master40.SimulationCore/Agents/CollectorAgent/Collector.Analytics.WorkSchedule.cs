
using Akka.Actor;
using AkkaSim;
using Master40.DB.Data.Context;
using Master40.DB.Enums;
using Master40.DB.ReportingModel;
using Master40.SimulationCore.Agents.CollectorAgent.Types;
using Master40.SimulationCore.Agents.HubAgent;
using Master40.SimulationCore.Environment.Options;
using Master40.SimulationCore.Types;
using MathNet.Numerics.Statistics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using static FBreakDowns;
using static FCreateSimulationWorks;
using static FAgentInformations;
using static FSetEstimatedThroughputTimes;
using static FUpdateSimulationWorkProviders;
using static FUpdateSimulationWorks;
using static Master40.SimulationCore.Agents.CollectorAgent.Collector.Instruction;

namespace Master40.SimulationCore.Agents.CollectorAgent
{
    public class CollectorAnalyticsWorkSchedule : Behaviour, ICollectorBehaviour
    {
        private CollectorAnalyticsWorkSchedule(ResourceList resources) : base() {
            _resources = resources;
        }

        private List<SimulationWorkschedule> simulationWorkschedules = new List<SimulationWorkschedule>();
        //private List<Tuple<string, long>> tuples = new List<Tuple<string, long>>();
        private long lastIntervalStart = 0;
        private List<FUpdateSimulationWork> _updatedSimulationWork = new List<FUpdateSimulationWork>();
        private ResourceList _resources { get; set; } = new ResourceList();
        public Collector Collector { get; set; }
        private CultureInfo _cultureInfo = CultureInfo.GetCultureInfo("en-GB"); // Required to get Number output with . instead of ,
        private List<Kpi> Kpis = new List<Kpi>();

        internal static List<Type> GetStreamTypes()
        {
            return new List<Type> { typeof(FCreateSimulationWork),
                                     typeof(FUpdateSimulationWork),
                                     typeof(FUpdateSimulationWorkProvider),
                                     typeof(UpdateLiveFeed),
                                     typeof(Hub.Instruction.AddMachineToHub),
                                     typeof(BasicInstruction.ResourceBrakeDown)

            };
        }

        public static CollectorAnalyticsWorkSchedule Get(ResourceList resources)
        {
            return new CollectorAnalyticsWorkSchedule(resources);
        }

        public override bool Action(object message) => throw new Exception("Please use EventHandle method to process Messages");

        public bool EventHandle(SimulationMonitor simulationMonitor, object message)
        {
            switch (message)
            {
                case FCreateSimulationWork m: CreateSimulationWorkSchedule(m); break;
                case FUpdateSimulationWork m: UpdateSimulationWorkSchedule(m); break;
                case FUpdateSimulationWorkProvider m: UpdateSimulationWorkItemProvider(m); break;
                case Collector.Instruction.UpdateLiveFeed m: UpdateFeed(m.GetObjectFromMessage); break;
                case Hub.Instruction.AddMachineToHub m: RecoverFromBreak(m.GetObjectFromMessage); break;
                case BasicInstruction.ResourceBrakeDown m: BreakDwn(m.GetObjectFromMessage); break;
                default: return false;
            }
            return true;
        }

        private void BreakDwn(FBreakDown item)
        {
            Collector.messageHub.SendToClient(item.Resource + "_State", "offline");
        }

        private void RecoverFromBreak(FAgentInformation item)
        {
            Collector.messageHub.SendToClient(item.RequiredFor + "_State", "online");
        }

        private void UpdateFeed(bool writeResultsToDB)
        {
            // var mbz = agent.Context.AsInstanceOf<Akka.Actor.ActorCell>().Mailbox.MessageQueue.Count;
            // Debug.WriteLine("Time " + agent.Time + ": " + agent.Context.Self.Path.Name + " Mailbox left " + mbz);
            MachineUtilization();
            ThroughPut();
            lastIntervalStart = Collector.Time;


            LogToDB(writeResultsToDB);

            Collector.Context.Sender.Tell(true, Collector.Context.Self);
        }

        private void LogToDB(bool writeResultsToDB)
        {
            if (Collector.saveToDB.Value &&  writeResultsToDB)
            {
                using (var ctx = ResultContext.GetContext(Collector.Config.GetOption<DBConnectionString>().Value))
                {
                    ctx.SimulationOperations.AddRange(simulationWorkschedules);
                    ctx.Kpis.AddRange(Kpis);
                    ctx.SaveChanges();
                    ctx.Dispose();
                }
            }
        }

        private void ThroughPut()
        {

            var art = from a in simulationWorkschedules
                      where a.ArticleType == "Product"
                          && a.CreatedForOrderId != null
                          && a.Time >= Collector.Config.GetOption<TimePeriodForThrougputCalculation>().Value
                      group a by new { a.Article, a.OrderId } into arti
                      select new
                      {
                          arti.Key.Article,
                          arti.Key.OrderId
                      };

            var leadTime = from lt in simulationWorkschedules
                           group lt by lt.OrderId into so
                           select new
                           {
                               OrderID = so.Key,
                               Dlz = (double)(so.Max(x => x.End) - so.Min(x => x.Start))
                           };

            var innerJoinQuery =
                   from a in art
                   join l in leadTime on a.OrderId equals l.OrderID
                   select new
                   {
                       a.Article,
                       a.OrderId,
                       l.Dlz
                   };

            var group = from dlz in innerJoinQuery
                        group dlz by dlz.Article into agregat
                        select new
                        {
                            agregat.Key,
                            List = agregat.Select(x => x.Dlz).ToList()
                        };

            foreach (var item in group)
            {
                var thoughput = JsonConvert.SerializeObject(new { group });
                Collector.messageHub.SendToClient("Throughput",  thoughput);

                var boxPlot = item.List.FiveNumberSummary();
                var uperQuartile = Convert.ToInt64(boxPlot[3]);
                Collector.actorPaths.SimulationContext.Ref.Tell(
                    SupervisorAgent.Supervisor.Instruction.SetEstimatedThroughputTime.Create(
                        new FSetEstimatedThroughputTime(uperQuartile, item.Key)
                        , Collector.actorPaths.SystemAgent.Ref
                    )
                    , ActorRefs.NoSender);

                Debug.WriteLine("(" + Collector.Time + ")" + item.Key + ": " + uperQuartile); 
            }

            var v2 = simulationWorkschedules.Where(a => a.ArticleType == "Product"
                                                   && a.HierarchyNumber == 20
                                                   && a.End == 0);


            Collector.messageHub.SendToClient("ContractsV2", JsonConvert.SerializeObject(new { Time = Collector.Time, Processing = v2.Count().ToString() }));
        }

        private void MachineUtilization()
        {
            double divisor = Collector.Time - lastIntervalStart;
            Collector.messageHub.SendToAllClients("(" + Collector.Time + ") Update Feed from DataCollection");
            Collector.messageHub.SendToAllClients("(" + Collector.Time + ") Time since last Update: " + divisor + "min");

            //simulationWorkschedules.WriteCSV( @"C:\Users\mtko\source\output.csv");


            var lower_borders = from sw in simulationWorkschedules
                                where sw.Start < lastIntervalStart
                                   && sw.End > lastIntervalStart
                                   && sw.Machine != null
                                select new
                                {
                                    M = sw.Machine,
                                    C = 1,
                                    W = sw.End - lastIntervalStart
                                };

            var upper_borders = from sw in simulationWorkschedules
                                where sw.Start < Collector.Time
                                   && sw.End > Collector.Time
                                   && sw.Machine != null
                                select new
                                {
                                    M = sw.Machine,
                                    C = 1,
                                    W = Collector.Time - sw.Start
                                };


            var from_work = from sw in simulationWorkschedules
                            where sw.Start >= lastIntervalStart 
                               && sw.End <= Collector.Time
                               && sw.Machine != null
                            group sw by sw.Machine into mg
                            select new
                            {
                                M = mg.Key,
                                C = mg.Count(),
                                W = (long)mg.Sum(x => x.End - x.Start)
                            };
            var machineList = _resources.Select(x => new { M = x, C = 0, W = (long)0 });
            var merge = from_work.Union(lower_borders).Union(upper_borders).Union(machineList).ToList();

            var final = from m in merge
                        group m by m.M into mg
                        select new
                        {
                            M = mg.Key,
                            C = mg.Sum(x => x.C),
                            W = mg.Sum(x => x.W)
                        };

            foreach (var item in final.OrderBy(x => x.M))
            {
                var value = Math.Round(item.W / divisor, 3).ToString(_cultureInfo);
                if (value == "NaN") value = "0";
                //Debug.WriteLine(item.M + " worked " + item.W + " min of " + divisor + " min with " + item.C + " items!", "work");
                var machine = item.M.Replace(")", "").Replace("Machine(", "");
                Collector.messageHub.SendToClient(machine, value);
                CreateKpi(Collector, value, item.M, KpiType.MachineUtilization);
            }

            var totalLoad = Math.Round(final.Sum(x => x.W) / divisor / final.Count() * 100, 3).ToString(_cultureInfo);
            if (totalLoad == "NaN")  totalLoad = "0";
            Collector.messageHub.SendToClient("TotalWork", JsonConvert.SerializeObject(new { Time = Collector.Time, Load = totalLoad }));
            CreateKpi(Collector, totalLoad, "TotalWork", KpiType.MachineUtilization);
            // // Kontrolle
            // var from_work2 = from sw in tuples
            //                  group sw by sw.Item1 into mg
            //                  select new
            //                  {
            //                      M = mg.Key,
            //                      W = (double)(mg.Sum(x => x.Item2))
            //                  };
            // 
            // foreach (var item in from_work2.OrderBy(x => x.M))
            // {
            //     Debug.WriteLine(item.M + " workload " + Math.Round(item.W / divisor, 3) + " %!", "intern");
            // }
            // tuples.Clear();
        }

        private void CreateKpi(Collector agent, string value, string name, KpiType kpiType)
        {
            var k = new Kpi
            {
                Name = name,
                Value = Convert.ToDouble(value),
                Time = (int)agent.Time,
                KpiType = kpiType,
                SimulationConfigurationId = agent.simulationId.Value,
                SimulationNumber = agent.simulationNumber.Value,
                IsFinal = false,
                IsKpi = true,
                SimulationType = agent.simulationKind.Value
            };
            Kpis.Add(k);
        }

        private void CreateSimulationWorkSchedule(FCreateSimulationWork cws)
        {
            var ws = cws.Operation;
            var sws = new SimulationWorkschedule
            {
                WorkScheduleId = ws.Key.ToString(),
                Article = ws.Operation.Article.Name,
                WorkScheduleName = ws.Operation.Name,
                DueTime = (int)ws.DueTime,
                SimulationConfigurationId = Collector.simulationId.Value,
                SimulationNumber = Collector.simulationNumber.Value,
                SimulationType = Collector.simulationKind.Value,
                OrderId = "[" + cws.CustomerOrderId + "]",
                HierarchyNumber = ws.Operation.HierarchyNumber,
                ProductionOrderId = "[" + ws.ProductionAgent.Path.Uid + "]",
                Parent = cws.IsHeadDemand.ToString(),
                ParentId = "[]",
                Time = (int)(Collector.Time),
                ArticleType = cws.ArticleType
            };

            var edit = _updatedSimulationWork.FirstOrDefault(x => x.WorkScheduleId.Equals(ws.Key.ToString()));
            if (edit != null)
            {
                sws.Start = (int)edit.Start;
                sws.End = (int)(edit.Start + edit.Duration + 1);
                sws.Machine = edit.Machine;
                _updatedSimulationWork.Remove(edit);
            }



            simulationWorkschedules.Add(sws);
        }


        private void UpdateSimulationWorkSchedule(FUpdateSimulationWork uws)
        {

            var edit = simulationWorkschedules.FirstOrDefault(x => x.WorkScheduleId.Equals(uws.WorkScheduleId));
            if (edit != null)
            {
                edit.Start = (int)uws.Start;
                edit.End = (int)(uws.Start + uws.Duration + 1); // to have Time Points instead of Time Periods
                edit.Machine = uws.Machine;
                return;
            }
            _updatedSimulationWork.Add(uws);

            //tuples.Add(new Tuple<string, long>(uws.Machine, uws.Duration));
        }

        private void UpdateSimulationWorkItemProvider(FUpdateSimulationWorkProvider uswp)
        {
            foreach (var agentId in uswp.ProductionAgents)
            {
                var items = simulationWorkschedules.Where(x => x.ProductionOrderId.Equals("[" + agentId.Path.Uid.ToString() + "]")).ToList();
                foreach (var item in items)
                {
                    item.ParentId = item.Parent.Equals(false.ToString()) ? "[" + uswp.RequestAgentId + "]" : "[]";
                    item.Parent = uswp.RequestAgentName;
                    item.CreatedForOrderId = item.OrderId;
                    item.OrderId = "[" + uswp.CustomerOrderId + "]";

                    // item.OrderId = orderId;
                }
            }

        }




    }
}
