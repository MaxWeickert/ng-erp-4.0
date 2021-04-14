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
        private OrderCounter _orderCounter { get; set; }
        private float _lastTimestamp { get; set; } = 0;
        private float _newKpiTimestamp { get; set; } = 0;
        private float _lastPredict { get; set; } = 0;
        private int _createdOrders { get; set; } = 0;
        private SimulationType _simulationType { get; set; }
        private decimal _transitionFactor { get; set; }
        private OrderGenerator _orderGenerator { get; set; }
        private ArticleCache _articleCache { get; set; }
        private ThroughPutDictionary _estimatedThroughPuts { get; set; } = new ThroughPutDictionary();
        private Queue<T_CustomerOrderPart> _orderQueue { get; set; } = new Queue<T_CustomerOrderPart>();
        private List<T_CustomerOrder> _openOrders { get; set; } = new List<T_CustomerOrder>();
        private int _numberOfValuesForPrediction { get; set; }
        private int _timeConstraintQueueLength { get; set; }
        private ThroughputPredictor _throughputPredictor { get; set; } = new ThroughputPredictor();
        private List<SimulationKpis> Kpis { get; set; } = new List<SimulationKpis>();
        private List<ThroughputParameter> productProperties { get; set; } = new List<ThroughputParameter>();


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
            _simulationEnds = configuration.GetOption<SimulationEnd>().Value;
            _simulationType = configuration.GetOption<SimulationKind>().Value;
            _transitionFactor = configuration.GetOption<TransitionFactor>().Value;
            _numberOfValuesForPrediction = configuration.GetOption<UsePredictedThroughput>().Value;
            _timeConstraintQueueLength = configuration.GetOption<TimeConstraintQueueLength>().Value;
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
            productProperties = ArticleStatistics.GetProductProps(dbProduction.DbContext);
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

            // var order = _productionDomainContext.CustomerOrders
            //     .Include(navigationPropertyPath: x => x.CustomerOrderParts)
            //     .Single(predicate: x => x.Id == _productionDomainContext.CustomerOrderParts.Single(s => s.Id == requestItem.CustomerOrderId).CustomerOrderId);
            // order.FinishingTime = (int)this.TimePeriod;
            // order.State = State.Finished;
            //_productionDomainContext.SaveChanges();
            // _messageHub.ProcessingUpdate(simId: _configID, finished: _orderCounter.ProvidedOrder(), simType: SimulationType.Decentral.ToString(), max: _orderCounter.Max);
        }

        private void End()
        {
            Agent.DebugMessage(msg: "End Sim");
            Agent.ActorPaths.SimulationContext.Ref.Tell(message: SimulationMessage.SimulationState.Finished);
        }

        //TOOD:
        // - Get all variables for productIDs at start and save them in dictionary
        private void PopOrder()
        {
            if (!_orderCounter.TryAddOne()) return;

            var order = _orderGenerator.GetNewRandomOrder(time: Agent.CurrentTime);

            Agent.Send(instruction: Supervisor.Instruction.PopOrder.Create(message: "PopNext", target: Agent.Context.Self), waitFor: order.CreationTime - Agent.CurrentTime);

            
            if (Kpis.Count >= _numberOfValuesForPrediction)
            {
                // Overwrite values with the same kpi.time
                Kpis.First(pA => pA.Time == _lastTimestamp).SumDuration = productProperties.Find(pP => pP.ArticleId == order.CustomerOrderParts.ElementAt(0).ArticleId).Duration;
                Kpis.First(pA => pA.Time == _lastTimestamp).SumOperations = productProperties.Find(pP => pP.ArticleId == order.CustomerOrderParts.ElementAt(0).ArticleId).OperationCount;
                Kpis.First(pA => pA.Time == _lastTimestamp).ProductionOrders = productProperties.Find(pP => pP.ArticleId == order.CustomerOrderParts.ElementAt(0).ArticleId).ProductionOrderCount;
                KickoffThroughputPrediction(Agent);
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

        private void KickoffThroughputPrediction(Agent agent)
        {
            //var valuesForPrediction = Kpis.Skip(Math.Max(0, Kpis.Count() - _numberOfValuesForPrediction)).Take(_numberOfValuesForPrediction); //set number of values for prediction
            var valuesForPrediction = Kpis.FindAll(k => k.Time <= _lastTimestamp); //all kpis for prediction, possibly bad for efficiency
            var predictedThroughput = _throughputPredictor.PredictThroughput(valuesForPrediction, agent);
            _estimatedThroughPuts.UpdateAll(predictedThroughput);
        }

        //TODO:
        //- Material wird falsch übergeben bspw. 8.116666E+07
        private void AddToKpi(FKpi.FKpi kpi)
        {
            // New Kpi list item
            if (_lastTimestamp < kpi.Time)
            {
                _newKpiTimestamp = _lastTimestamp;
                switch (kpi.Name)
                {
                    case "Assembly":
                        Kpis.Add(new SimulationKpis((float)kpi.Time, assembly: (float)kpi.Value));
                        break;
/*                    case "Consumable":
                        Kpis.Add(new SimulationKpis((float)kpi.Time, consumable: (float)kpi.Value));
                        break;*/
                    case "Material":
                        Kpis.Add(new SimulationKpis((float)kpi.Time, material: (float)kpi.Value));
                        break;
                    case "Open":
                        Kpis.Add(new SimulationKpis((float)kpi.Time, openOrders: (float)kpi.Value));
                        break;
                    case "New":
                        Kpis.Add(new SimulationKpis((float)kpi.Time, newOrders: (float)kpi.Value));
                        break;
                    case "TotalWork":
                        Kpis.Add(new SimulationKpis((float)kpi.Time, totalWork: (float)kpi.Value));
                        break;
                    case "TotalSetup":
                        Kpis.Add(new SimulationKpis((float)kpi.Time, totalSetup: (float)kpi.Value));
                        break;
/*                    case "SumDuration":
                        Kpis.Add(new SimulationKpis((float)kpi.Time, sumDuration: (float)kpi.Value));
                        break;
                    case "SumOperations":
                        Kpis.Add(new SimulationKpis((float)kpi.Time, sumOperations: (float)kpi.Value));
                        break;
                    case "ProductionOrders":
                        Kpis.Add(new SimulationKpis((float)kpi.Time, productionOrders: (float)kpi.Value));
                        break;
                    case "CycleTime":
                        Kpis.Add(new SimulationKpis((float)kpi.Time, cycleTime: (float)kpi.Value));
                        break;*/
                    default:
                        Agent.DebugMessage(msg: "Invalid Kpi to add to Kpis for Prediction");
                        break;
                }
                _lastTimestamp = (float)kpi.Time;
            }
            else
            {
                switch (kpi.Name)
                {
                    case "Assembly":
                        Kpis.First(pA => pA.Time == kpi.Time).Assembly = (float)kpi.Value;
                        break;
/*                    case "Consumable":
                        Kpis.Add(new SimulationKpis((float)kpi.Time, consumable: (float)kpi.Value));
                        break;*/
                    case "Material":
                        Kpis.First(pA => pA.Time == kpi.Time).Material = (float)kpi.Value;
                        break;
                    case "Open":
                        Kpis.First(pA => pA.Time == kpi.Time).OpenOrders = (float)kpi.Value;
                        break;
                    case "New":
                        Kpis.First(pA => pA.Time == kpi.Time).NewOrders = (float)kpi.Value;
                        break;
                    case "TotalWork":
                        Kpis.First(pA => pA.Time == kpi.Time).TotalWork = (float)kpi.Value;
                        break;
                    case "TotalSetup":
                        Kpis.First(pA => pA.Time == kpi.Time).TotalSetup = (float)kpi.Value; ;
                        break;
/*                    case "SumDuration":
                        Kpis.Add(new SimulationKpis((float)kpi.Time, sumDuration: (float)kpi.Value));
                        break;
                    case "SumOperations":
                        Kpis.Add(new SimulationKpis((float)kpi.Time, sumOperations: (float)kpi.Value));
                        break;
                    case "ProductionOrders":
                        Kpis.Add(new SimulationKpis((float)kpi.Time, productionOrders: (float)kpi.Value));
                        break;
                    case "CycleTime":
                        Kpis.Add(new SimulationKpis((float)kpi.Time, cycleTime: (float)kpi.Value));
                        break;*/
                    default:
                        Agent.DebugMessage(msg: "Invalid Kpi to add to Kpis for Prediction");
                        break;
                }
            }
        }
    }
}
