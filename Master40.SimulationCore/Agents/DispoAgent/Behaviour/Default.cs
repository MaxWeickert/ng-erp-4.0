﻿using System;
using Master40.DB.DataModel;
using Master40.DB.Nominal;
using Master40.SimulationCore.Agents.DirectoryAgent;
using Master40.SimulationCore.Agents.StorageAgent;
using Master40.SimulationCore.Helper;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Master40.SimulationCore.Agents.ProductionAgent;
using NLog;
using NLog.Fluent;
using static FAgentInformations;
using static FArticleProviders;
using static FArticles;
using static FStockProviders;
using static FStockReservations;
using static Master40.SimulationCore.Agents.Guardian.Instruction;
using static Master40.SimulationCore.Agents.StorageAgent.Storage.Instruction;
using static Master40.SimulationCore.Agents.SupervisorAgent.Supervisor.Instruction;
using static Master40.Tools.ExtensionMethods.Negate;
using static Master40.SimulationCore.Agents.StorageAgent.Storage.Instruction.Default;

namespace Master40.SimulationCore.Agents.DispoAgent.Behaviour
{
    public class Default : SimulationCore.Types.Behaviour
    {
        internal Default(SimulationType simulationType = SimulationType.None)
            : base(childMaker: null, simulationType: simulationType)
        {
            fArticlesToProvide = new Dictionary<IActorRef, Guid>();
        }


        internal FArticle _fArticle { get; set; }

        internal Dictionary<IActorRef, Guid> fArticlesToProvide;

        internal int _quantityToProduce { get; set; }

        public override bool Action(object message)
        {
            switch (message)
            {
                case Dispo.Instruction.RequestArticle r: RequestArticle(requestArticle: r.GetObjectFromMessage); break; // Message From Contract or Production Agent
                case BasicInstruction.ResponseFromDirectory r: ResponseFromDirectory(hubInfo: r.GetObjectFromMessage); break; // return with Storage from Directory
                case Dispo.Instruction.ResponseFromStock r: ResponseFromStock(reservation: r.GetObjectFromMessage); break; // Message from Storage with Reservation
                case BasicInstruction.JobForwardEnd msg: PushForwardTimeToParent(earliestStartForForwardScheduling: msg.GetObjectFromMessage); break; // push Calculated Forward Calculated
                case Dispo.Instruction.ResponseFromSystemForBom r: ResponseFromSystemForBom(article: r.GetObjectFromMessage); break;
                case Dispo.Instruction.WithdrawArticleFromStock r: WithdrawArticleFromStock(); break; // Withdraw initiated by ResourceAgent for Production
                case BasicInstruction.ProvideArticle r: ProvideRequest(fArticleProvider: r.GetObjectFromMessage); break;
                case BasicInstruction.UpdateCustomerDueTimes msg: UpdateCustomerDueTimes(msg.GetObjectFromMessage); break;
                case BasicInstruction.RemoveVirtualChild msg: RemoveVirtualChild(); break;
                case BasicInstruction.RemovedChildRef msg: TryToFinish(); break;
                default: return false;
            }
            return true;
        }
        internal void UpdateCustomerDueTimes(long customerDue)
        {
            // Save Request Item.
            _fArticle = _fArticle.UpdateCustomerDue(customerDue);
            foreach (var productionRef in Agent.VirtualChildren)
            {
                Agent.Send(BasicInstruction.UpdateCustomerDueTimes.Create(customerDue, productionRef));
            }
        }

        internal void RemoveVirtualChild()
        {
            Agent.VirtualChildren.Remove(Agent.Sender);
            Agent.Send(BasicInstruction.RemovedChildRef.Create(Agent.Sender));
            TryToRemoveChildRefFromProduction();
        }
        internal void RequestArticle(FArticle requestArticle)
        {
            // Save Request Item.
            _fArticle = requestArticle;
            Agent.DebugMessage($"{_fArticle.Article.Name} {_fArticle.Key} is Requested to Produce.", CustomLogger.STOCK, LogLevel.Warn);
            // get related Storage Agent
            Agent.Send(instruction: Directory.Instruction.Default.RequestAgent
                                    .Create(discriminator: requestArticle.Article.Name
                                        , target: Agent.ActorPaths.StorageDirectory.Ref));
        }

        internal void ResponseFromDirectory(FAgentInformation hubInfo)
        {
            Agent.DebugMessage(msg: "Acquired stock Agent: " + hubInfo.Ref.Path.Name + " from " + Agent.Sender.Path.Name, CustomLogger.INITIALIZE, LogLevel.Warn);

            _fArticle = _fArticle.UpdateStorageAgent(s: hubInfo.Ref);
            // Create Request to Storage Agent 
            Agent.Send(instruction: Storage.Instruction.Default.RequestArticle.Create(message: _fArticle, target: hubInfo.Ref));
        }

        internal void ResponseFromStock(FStockReservation reservation)
        {
            _fArticle = _fArticle.UpdateStockExchangeId(i: reservation.TrackingId);
           
            _quantityToProduce = _fArticle.Quantity - reservation.Quantity;

            Agent.DebugMessage(msg: reservation.Quantity + " " 
                                + _fArticle.Article.Name + " are reserved and " 
                                + _quantityToProduce + " " + _fArticle.Article.Name + " need to be produced!", CustomLogger.STOCK, LogLevel.Warn);

            if (reservation.IsInStock && !_fArticle.Article.ToBuild)
            {
                Agent.DebugMessage(msg: $"Start forward scheduling for article: {_fArticle.Article.Name} {_fArticle.Key} at: {Agent.CurrentTime}", CustomLogger.SCHEDULING, LogLevel.Warn);
                PushForwardTimeToParent(earliestStartForForwardScheduling: Agent.CurrentTime);
            }


            if (reservation.IsInStock && IsNot(_fArticle.IsHeadDemand))
            {
                ProvideRequest(new FArticleProvider(articleKey: _fArticle.Key
                                                  , articleName: _fArticle.Article.Name
                                                  , stockExchangeId: reservation.TrackingId
                                                  , articleFinishedAt: Agent.CurrentTime
                                                  , customerDue: _fArticle.CustomerDue
                                                  , provider: new List<FStockProvider>(new[] { new FStockProvider(reservation.TrackingId, "In Stock") })));
            }

            // else create Production Agents if ToBuild
            if (_fArticle.Article.ToBuild)
            {
                Agent.Send(instruction: RequestArticleBom.Create(message: _fArticle.Article.Id, target: Agent.ActorPaths.SystemAgent.Ref));

                // and request the Article from  stock at Due Time
                if (_fArticle.IsHeadDemand)
                {
                    var nextRequestAt = _fArticle.DueTime - Agent.CurrentTime;
                    Agent.DebugMessage(msg: $"Ask storage for Article {_fArticle.Key} in + {nextRequestAt}", CustomLogger.STOCK, LogLevel.Warn);

                    Agent.Send(instruction: ProvideArticleAtDue.Create(message: _fArticle.Key, target: _fArticle.StorageAgent)
                                 , waitFor: nextRequestAt);
                }
            }
            // Not in Stock and Not ToBuild Agent has to Wait for stock to provide materials
        }

        internal void ResponseFromSystemForBom(M_Article article)
        {
            // Update
            var dueTime = _fArticle.DueTime;

            if (article.Operations != null)
                dueTime = _fArticle.DueTime - article.Operations.Sum(selector: x => x.Duration + x.AverageTransitionDuration);
            // TODO: Object that handles the different operations- current assumption is all operations are handled as a sequence (no alternative/parallel plans) 

            _fArticle = _fArticle.UpdateCustomerOrderAndDue(id: _fArticle.CustomerOrderId, due: dueTime, storage: _fArticle.StorageAgent)
                                             .UpdateArticle(article: article);

            // Creates a Production Agent for each element that has to be produced
            for (var i = 0; i < _quantityToProduce; i++)
            {
                var agentSetup = AgentSetup.Create(agent: Agent, behaviour: ProductionAgent.Behaviour.Factory.Get(simType: SimulationType.None));
                var instruction = CreateChild.Create(setup: agentSetup, target: ((IAgent)Agent).Guardian, source: Agent.Context.Self);
                Agent.Send(instruction: instruction);
            }
        }

        private void PushForwardTimeToParent(long earliestStartForForwardScheduling)
        {
            Agent.DebugMessage(msg:$"Earliest time to provide {_fArticle.Article.Name} {_fArticle.Key} at {earliestStartForForwardScheduling}", CustomLogger.SCHEDULING, LogLevel.Warn);
            var msg = BasicInstruction.JobForwardEnd.Create(message: earliestStartForForwardScheduling, target: Agent.VirtualParent);
            Agent.Send(instruction: msg);
        }

        internal void ProvideRequest(FArticleProvider fArticleProvider)
        {
            // TODO: Might be problematic due to inconsistent _fArticle != Storage._fArticle
            Agent.DebugMessage(msg: $"Request for {_fArticle.Quantity} {_fArticle.Article.Name} {_fArticle.Key} provided from {Agent.Sender.Path.Name} to {Agent.VirtualParent.Path.Name}", CustomLogger.STOCK, LogLevel.Warn);

            _fArticle = _fArticle.UpdateFinishedAt(f: Agent.CurrentTime);

            var providedArticles = fArticleProvider.Provider.Select(x => x.ProvidesArticleKey);
            foreach (var articlesInProduction in fArticlesToProvide)
            {
                if (IsFalse(providedArticles.Contains(articlesInProduction.Value)) // is not original provider
                    && Agent.VirtualChildren.Contains(articlesInProduction.Key)) // and ist not already finished to produce
                {
                        Agent.Send(BasicInstruction.UpdateCustomerDueTimes
                            .Create(fArticleProvider.CustomerDue, articlesInProduction.Key));
                }
            }

            Agent.Send(instruction: BasicInstruction.ProvideArticle
                                                    .Create(message: fArticleProvider
                                                            ,target: Agent.VirtualParent 
                                                           ,logThis: false));
            
        }

        internal void WithdrawArticleFromStock()
        {
            Agent.DebugMessage(msg: $"Withdraw article {_fArticle.Article.Name} {_fArticle.Key} from Stock exchange {_fArticle.StockExchangeId}", CustomLogger.STOCK, LogLevel.Warn);
            Agent.Send(instruction: WithdrawArticle
                              .Create(message: _fArticle.StockExchangeId
                                     , target: _fArticle.StorageAgent));
            _fArticle = _fArticle.SetProvided;
            TryToRemoveChildRefFromProduction();
        }

        private void TryToRemoveChildRefFromProduction()
        {
            if (_fArticle.IsHeadDemand)
            {
                TryToFinish();
                return;
            }

            if (Agent.VirtualChildren.Count == 0 && _fArticle.IsProvided)
            {
                Agent.Send(BasicInstruction.RemoveVirtualChild.Create(Agent.VirtualParent));
            }
        }

        internal void TryToFinish()
        {
            if (Agent.VirtualChildren.Count == 0 && _fArticle.IsProvided)
            {
                Agent.DebugMessage(
                    msg: $"Shutdown for {_fArticle.Article.Name} " +
                         $"(Key: {_fArticle.Key}, OrderId: {_fArticle.CustomerOrderId})"
                    , CustomLogger.DISPOPRODRELATION, LogLevel.Debug);
                Agent.TryToFinish();
                return;
            }

            Agent.DebugMessage(
                msg: $"Could not run shutdown for {_fArticle.Article.Name} " +
                     $"(Key: {_fArticle.Key}, OrderId: {_fArticle.CustomerOrderId}) cause article is provided {_fArticle.IsProvided } and has childs left  {Agent.VirtualChildren.Count} "
                , CustomLogger.DISPOPRODRELATION, LogLevel.Debug);
        }
        public override void OnChildAdd(IActorRef childRef)
        {
            var articleKey = _fArticle.Keys.ToArray()[fArticlesToProvide.Count];
            var baseArticle = _fArticle.SetKey(articleKey);
            fArticlesToProvide.Add(Agent.Context.Sender, articleKey);
            Agent.Send(instruction: Production.Instruction.StartProduction.Create(message: baseArticle, target: Agent.Context.Sender));
            Agent.DebugMessage(msg: $"Dispo<{baseArticle.Article.Name } (OrderId: { baseArticle.CustomerOrderId }) > ProductionStart has been sent for { baseArticle.Key }.");
        }
    }
}
