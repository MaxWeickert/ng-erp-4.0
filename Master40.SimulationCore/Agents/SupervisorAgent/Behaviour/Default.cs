using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using AiProvider.DataStuctures;
using Akka.Actor;
using Akka.Util.Internal;
using AkkaSim.Definitions;
using Master40.DB;
using Master40.DB.Data.Context;
using Master40.DB.Data.Helper;
using Master40.DB.Data.Helper.Types;
using Master40.DB.Data.Initializer.StoredProcedures;
using Master40.DB.DataModel;
using Master40.DB.Nominal;
using Master40.DB.ReportingModel;
using Master40.SimulationCore.Agents.ContractAgent;
using Master40.SimulationCore.Agents.DispoAgent;
using Master40.SimulationCore.Agents.Guardian;
using Master40.SimulationCore.Agents.SupervisorAgent.Types;
using Master40.SimulationCore.Environment;
using Master40.SimulationCore.Environment.Options;
using Master40.SimulationCore.Helper;
using Master40.SimulationCore.Helper.DistributionProvider;
using Master40.SimulationCore.Helper.AiProvider;
using Master40.SimulationCore.Types;
using Master40.Tools.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using static Master40.SimulationCore.Agents.SupervisorAgent.Supervisor.Instruction;
using System.IO;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace Master40.SimulationCore.Agents.SupervisorAgent.Behaviour
{
    public class Default : SimulationCore.Types.Behaviour
    {
        private DataBase<ProductionDomainContext> dbProduction { get; set; }
        private IMessageHub _messageHub { get; set; }
        private HubConnection _connection;
        private int orderCount { get; set; } = 0;
        private long _simulationEnds { get; set; }
        private int _configID { get; set; }
        private float _arrivalRate { get; set; }
        private bool _testArrivalRate { get; set; }
        private int _simulationNumber { get; set; }
        private OrderCounter _orderCounter { get; set; }
        private float _lastTimestamp { get; set; } = 0;
        private SimulationType _simulationType { get; set; }
        private decimal _transitionFactor { get; set; }
        private OrderGenerator _orderGenerator { get; set; }
        private ArticleCache _articleCache { get; set; }
        private ThroughPutDictionary _estimatedThroughPuts { get; set; } = new ThroughPutDictionary();
        private Queue<T_CustomerOrderPart> _orderQueue { get; set; } = new Queue<T_CustomerOrderPart>();
        private List<T_CustomerOrder> _openOrders { get; set; } = new List<T_CustomerOrder>();
        private int _numberOfValuesForPrediction { get; set; }
        private int _timeConstraintQueueLength { get; set; }
        private int _settlingStart { get; set; }
        private ThroughputPredictor _throughputPredictor { get; set; } = new ThroughputPredictor();
        private List<SimulationKpis> Kpis { get; set; } = new List<SimulationKpis>();
        private List<ProductProperties> ProductProperties { get; set; } = new List<ProductProperties>();
        private List<ResourceCapabilityKpis> ResourceCapabilityKpis { get; set; } = new List<ResourceCapabilityKpis>();

        public  Default(string dbNameProduction
            , IMessageHub messageHub
            , Configuration configuration
            , List<FSetEstimatedThroughputTimes.FSetEstimatedThroughputTime> estimatedThroughputTimes)
        {
            dbProduction = Dbms.GetMasterDataBase(dbName: dbNameProduction, noTracking: false);
            _articleCache = new ArticleCache(connectionString: new DbConnectionString(dbProduction.ConnectionString.Value));
            _messageHub = messageHub;
            _orderGenerator = new OrderGenerator(simConfig: configuration, productionDomainContext: dbProduction.DbContext
                , productIds: estimatedThroughputTimes.Select(x => x.ArticleId).ToList());
            _orderCounter = new OrderCounter(maxQuantity: configuration.GetOption<OrderQuantity>().Value);
            _configID = configuration.GetOption<SimulationId>().Value;
            _simulationNumber = configuration.GetOption<SimulationNumber>().Value;
            _arrivalRate = (float)configuration.GetOption<OrderArrivalRate>().Value;
            _simulationEnds = configuration.GetOption<SimulationEnd>().Value;
            _simulationType = configuration.GetOption<SimulationKind>().Value;
            _transitionFactor = configuration.GetOption<TransitionFactor>().Value;
            _numberOfValuesForPrediction = configuration.GetOption<UsePredictedThroughput>().Value;
            _timeConstraintQueueLength = configuration.GetOption<TimeConstraintQueueLength>().Value;
            _settlingStart = configuration.GetOption<SettlingStart>().Value;
            _testArrivalRate = configuration.GetOption<TestArrivalRate>().Value;
            estimatedThroughputTimes.ForEach(SetEstimatedThroughputTime);
        }
        public override bool Action(object message)
        {
            switch (message)
            {
                case BasicInstruction.ChildRef instruction: OnChildAdd(childRef: instruction.GetObjectFromMessage); break;
                // ToDo Benammung : Sollte die Letzte nachricht zwischen Produktionsagent und Contract Agent abfangen und Inital bei der ersten Forward Terminierung setzen
                case SetEstimatedThroughputTime instruction: SetEstimatedThroughputTime(getObjectFromMessage: instruction.GetObjectFromMessage); break;
                case CreateContractAgent instruction: CreateContractAgent(order: instruction.GetObjectFromMessage); break;
                case RequestArticleBom instruction: RequestArticleBom(articleId: instruction.GetObjectFromMessage); break;
                case OrderProvided instruction: OrderProvided(instruction: instruction); break;
                case AddKpi instruction: AddToKpi(kpi: instruction.GetObjectFromMessage); break; //new
                case AddResourceKpi instruction: AddToResourceKpi(resourceKpi: instruction.GetObjectFromMessage); break; //new for arrival Rate test
                case SystemCheck instruction: SystemCheck(); break;
                case EndSimulation instruction: End(); break;
                case PopOrder p: PopOrder(); break;
                default: throw new Exception(message: "Invalid Message Object.");
            }
            return true;
        }

        public override bool AfterInit()
        {
            Agent.Send(instruction: Supervisor.Instruction.PopOrder.Create(message: "Pop", target: Agent.Context.Self), waitFor: 1);
            Agent.Send(instruction: EndSimulation.Create(message: true, target: Agent.Context.Self), waitFor: _simulationEnds);
            Agent.Send(instruction: Supervisor.Instruction.SystemCheck.Create(message: "CheckForOrders", target: Agent.Context.Self), waitFor: 1);
            Agent.DebugMessage(msg: "Agent-System ready for Work");
            ProductProperties = ArticleStatistics.GetProductPropperties(dbProduction.DbContext);

            //ThroughputPredictor.LoadModel();

            return true;
        }

        private void SetEstimatedThroughputTime(FSetEstimatedThroughputTimes.FSetEstimatedThroughputTime getObjectFromMessage)
        {
            _estimatedThroughPuts.UpdateOrCreate(name: getObjectFromMessage.ArticleName, time: getObjectFromMessage.Time);
        }

        private void CreateContractAgent(T_CustomerOrder order)
        {
            dbProduction.DbContext.CustomerOrders.Add(order);
            dbProduction.DbContext.SaveChanges();

            var orderPart = order.CustomerOrderParts.First();
            _orderQueue.Enqueue(item: orderPart);
            Agent.DebugMessage(msg: $"Creating Contract Agent for order {order.Id} with {order.Name} DueTime {orderPart.CustomerOrder.DueTime}");
            var agentSetup = AgentSetup.Create(agent: Agent, behaviour: ContractAgent.Behaviour.Factory.Get(simType: _simulationType));
            var instruction = Instruction.CreateChild.Create(setup: agentSetup
                                               , target: Agent.ActorPaths.Guardians
                                                                  .Single(predicate: x => x.Key == GuardianType.Contract)
                                                                  .Value
                                               , source: Agent.Context.Self);
            Agent.Send(instruction: instruction);
        }

        /// <summary>
        /// After a child has been ordered from Guardian a ChildRef will be returned by the responsible child
        /// it has been allready added to this.VirtualChilds at this Point
        /// </summary>
        /// <param name="childRef"></param>
        public override void OnChildAdd(IActorRef childRef)
        {
            Agent.VirtualChildren.Add(item: childRef);
            Agent.Send(instruction: Contract.Instruction.StartOrder.Create(message: _orderQueue.Dequeue()
                                                        , target: childRef
                                                      , logThis: true));
        }

        private void RequestArticleBom(int articleId)
        {
            // get BOM from cached context 
            var article = _articleCache.GetArticleById(id: articleId, transitionFactor: _transitionFactor);

            Agent.DebugMessage(msg: "Request details for article: " + article.Name + " from  " + Agent.Sender.Path);

            // calback with po.bom
            Agent.Send(instruction: Dispo.Instruction.ResponseFromSystemForBom.Create(message: article, target: Agent.Sender));
        }

        private void OrderProvided(OrderProvided instruction)
        {
            if (!(instruction.Message is FArticles.FArticle requestItem))
            {
                throw new InvalidCastException(message: Agent.Name + " Cast to RequestItem Failed");
            }

            // Set the finished time of an order
            Kpis.First(pA => pA.OrderId == requestItem.CustomerOrderId).FinishingTime = requestItem.FinishedAt;

            // order.FinishingTime = (int)this.TimePeriod;
            // order.State = State.Finished;
            //_productionDomainContext.SaveChanges();
            // _messageHub.ProcessingUpdate(simId: _configID, finished: _orderCounter.ProvidedOrder(), simType: SimulationType.Decentral.ToString(), max: _orderCounter.Max);
        }

        private void End()
        {
            //Calculate the real cycle time of an order and add to list
            foreach (var item in Kpis)
            {
                item.CycleTime = item.FinishingTime - item.CreationTime;
            }

            CreateCsvOfKpiList();

            Agent.DebugMessage(msg: "End Sim");
            Agent.ActorPaths.SimulationContext.Ref.Tell(message: SimulationMessage.SimulationState.Finished);
        }

        private void PopOrder()
        {
            if (!_orderCounter.TryAddOne()) return;

            var order = _orderGenerator.GetNewRandomOrder(time: Agent.CurrentTime);

            Agent.Send(instruction: Supervisor.Instruction.PopOrder.Create(message: "PopNext", target: Agent.Context.Self), waitFor: order.CreationTime - Agent.CurrentTime);

            //Fill Kpi List with Kpis and ProductProperties
            FillKpiList(order);

            if(_numberOfValuesForPrediction > 0 && Kpis.Count() > 10)
            {
                //KickoffThroughputPrediction(order.Name, Agent);
            }

            var eta = _estimatedThroughPuts.Get(name: order.Name);
            Agent.DebugMessage(msg: $"EstimatedTransitionTime {eta.Value} for order {order.Name} {order.Id} , {order.DueTime}");

            long period = order.DueTime - (eta.Value); // 1 Tag und 1 Schicht
            if (period < 0 || eta.Value == 0)
            {
                CreateContractAgent(order);
                return;
            }
            _openOrders.Add(item: order);
        }

        private void SystemCheck()
        {
            //Check capability workload
            if (ResourceCapabilityKpis.Count > 0 && _testArrivalRate == true)
            {
                checkCapabilityWorkload();
            }     

            Agent.Send(instruction: Supervisor.Instruction.SystemCheck.Create(message: "CheckForOrders", target: Agent.Context.Self), waitFor: 1);

            // TODO Loop Through all CustomerOrderParts
            var orders = _openOrders.Where(predicate: x => x.DueTime - _estimatedThroughPuts.Get(name: x.Name).Value <= Agent.CurrentTime).ToList();
            // Debug.WriteLine("SystemCheck(" + CurrentTime + "): " + orders.Count() + " of " + _openOrders.Count() + "found");
            foreach (var order in orders)
            {
                CreateContractAgent(order);
                _openOrders.RemoveAll(match: x => x.Id == order.Id);
            }

        }

        private void KickoffThroughputPrediction(string articleName, Agent agent)
        {
            //TODO: Change data type of input list item
            //var predictedThroughput = _throughputPredictor.PredictThroughput(valuesForPrediction, agent);

            // Return ALL filled list items
            var completeKpis = Kpis.FindAll(k => k.Assembly != 0 &&
                                                 k.Material != 0 &&
                                                 k.OpenOrders != -1 &&
                                                 k.NewOrders != -1 &&
                                                 k.TotalWork != 0 &&
                                                 k.TotalSetup != 0);

            if (completeKpis.Any())
            {
                var predictedThroughput = _throughputPredictor.PredictThroughput(completeKpis.Last(), agent);
                Kpis.First(k => k.OrderId == _throughputPredictor.predictedActualThroughputList.Last()[0]).PredCycleTime = _throughputPredictor.predictedActualThroughputList.Last()[1];
                _estimatedThroughPuts.UpdateOrCreate(articleName, predictedThroughput);
            }
        }

        private void CreateCsvOfKpiList()
        {
            // remove List Items where KPIs are 0
            Kpis.RemoveAll(k => k.Assembly == 0 && k.Material == 0);

            // remove list items where orders wasn't finished
            Kpis.RemoveAll(k => k.CycleTime < 0);

            // Remove Settling Kpis
            Kpis.RemoveAll(k => k.CreationTime < _settlingStart);

            //Create a csv file for training
            var filestring = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../GeneratedData/train.csv"));
            //var filestring = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../GeneratedData/" + _simulationNumber + "_training_" + _arrivalRate + ".csv"));

            var appendCsv = false;
            var config = new CsvConfiguration(CultureInfo.InvariantCulture);

            if (File.Exists(filestring))
            {
                appendCsv = true;
                config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    // Don't write the header again.
                    HasHeaderRecord = false,
                };
            }

            var streamWriter = new StreamWriter(filestring, appendCsv);
            var csvWriter = new CsvWriter(streamWriter, config);
            csvWriter.WriteRecords(Kpis);
            streamWriter.Flush();
        }

        private void FillKpiList(T_CustomerOrder order)
        {
            //if (Agent.CurrentTime >= _timeConstraintQueueLength)
            {
                Kpis.Add(new SimulationKpis(Agent.CurrentTime, orderId: (float)order.Id));
                Kpis.First(k => k.OrderId == order.Id).CreationTime = order.CreationTime;
                Kpis.First(k => k.OrderId == order.Id).SumDuration = ProductProperties.Find(pP => pP.ArticleId == order.CustomerOrderParts.ElementAt(0).ArticleId).Duration;
                Kpis.First(k => k.OrderId == order.Id).SumOperations = ProductProperties.Find(pP => pP.ArticleId == order.CustomerOrderParts.ElementAt(0).ArticleId).OperationCount;
                Kpis.First(k => k.OrderId == order.Id).ProductionOrders = ProductProperties.Find(pP => pP.ArticleId == order.CustomerOrderParts.ElementAt(0).ArticleId).ProductionOrderCount;

                if (Kpis.First(k => k.OrderId == order.Id).Assembly == 0
                    && Kpis.First(k => k.OrderId == order.Id).Material == 0
                    && Kpis.First(k => k.OrderId == order.Id).OpenOrders == -1
                    && Kpis.First(k => k.OrderId == order.Id).NewOrders == -1
                    && Kpis.First(k => k.OrderId == order.Id).TotalWork == 0
                    && Kpis.First(k => k.OrderId == order.Id).TotalSetup == 0
                    && Agent.CurrentTime >= _timeConstraintQueueLength
                    && Kpis.Count > 0)
                {
                    //TODO: Checkout if list already contains elements
                    Kpis.First(k => k.OrderId == order.Id).Assembly = Kpis.Last(k => k.Assembly != 0).Assembly;
                    Kpis.First(k => k.OrderId == order.Id).Material = Kpis.Last(k => k.Material != 0).Material;
                    Kpis.First(k => k.OrderId == order.Id).OpenOrders = Kpis.Last(k => k.OpenOrders != -1).OpenOrders;
                    Kpis.First(k => k.OrderId == order.Id).NewOrders = Kpis.Last(k => k.NewOrders != -1).NewOrders; //search for assembly != bc NewOrders can sometimes be 0
                    Kpis.First(k => k.OrderId == order.Id).TotalWork = Kpis.Last(k => k.TotalWork != 0).TotalWork;
                    Kpis.First(k => k.OrderId == order.Id).TotalSetup = Kpis.Last(k => k.TotalSetup != 0).TotalSetup;
                }
            }
        }

        private void checkCapabilityWorkload()
        {
            //Stop Simulation Run if one ResourceCapability's workload is over maxWorkload
            var maxWorkload = 0.85;
            if (ResourceCapabilityKpis.Max(rk => rk.value) > maxWorkload)
            {
                Agent.DebugMessage(msg: "----------------------------------------------------------------------------");
                Agent.DebugMessage(msg: $"Simulation will be stopped because of high workload at {Agent.CurrentTime} ");
                Agent.DebugMessage(msg: $"Resource: { ResourceCapabilityKpis.First(rk => rk.value > maxWorkload).name } | Workload: { ResourceCapabilityKpis.First(rk => rk.value > maxWorkload).value } !");
                Agent.DebugMessage(msg: "----------------------------------------------------------------------------");
                End();
            }
        }

        private void AddToKpi(FKpi.FKpi kpi)
        {
            if (Kpis.Last().CreationTime >= kpi.Time && Kpis.Count > 0)
            {
                switch (kpi.Name)
                {
                    case "Assembly":
                        Kpis.First(k => k.CreationTime >= kpi.Time).Assembly = (float)kpi.Value;
                        break;
                    case "Material":
                        Kpis.First(k => k.CreationTime >= kpi.Time).Material = (float)kpi.Value;
                        break;
                    case "Open":
                        Kpis.First(k => k.CreationTime >= kpi.Time).OpenOrders = (float)kpi.Value;
                        break;
                    case "New":
                        Kpis.First(k => k.CreationTime >= kpi.Time).NewOrders = (float)kpi.Value;
                        break;
                    case "TotalWork":
                        Kpis.First(k => k.CreationTime >= kpi.Time).TotalWork = (float)kpi.Value;
                        break;
                    case "TotalSetup":
                        Kpis.First(k => k.CreationTime >= kpi.Time).TotalSetup = (float)kpi.Value;
                        break;
                    default:
                        Agent.DebugMessage(msg: "Invalid Kpi to add to Kpis for Prediction");
                        break;
                }
            }
        }

        private void AddToResourceKpi(FResourceKpi.FResourceKpi resourceKpi)
        {
            ResourceCapabilityKpis.Add(new ResourceCapabilityKpis(resourceKpi.Time, resourceKpi.Name, resourceKpi.Value, resourceKpi.Type));
        }
    }
}
