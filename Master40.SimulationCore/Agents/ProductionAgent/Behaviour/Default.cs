﻿using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Master40.DB.DataModel;
using Master40.SimulationCore.Agents.DispoAgent;
using Master40.SimulationCore.Agents.HubAgent;
using Master40.SimulationCore.Helper;
using Master40.SimulationCore.MessageTypes;
using Master40.SimulationImmutables;

namespace Master40.SimulationCore.Agents.ProductionAgent.Behaviour
{
    public class Default : MessageTypes.Behaviour
    {
        internal Default(Dictionary<string, object> properties) : base(null, properties) { }
        public override bool Action(Agent agent, object message)
        {
            switch (message)
            {
                case Production.Instruction.StartProduction i: StartProductionAgent((Production)agent, i.GetObjectFromMessage); break;
                case BasicInstruction.ResponseFromHub s: SetHubAgent((Production)agent, s.GetObjectFromMessage); break;
                case Production.Instruction.FinishWorkItem fw: FinishWorkItem((Production)agent, fw.GetObjectFromMessage); break;
                case Production.Instruction.ProductionStarted ps: ProductionStarted((Production)agent, ps.GetObjectFromMessage); break;
                case Production.Instruction.ProvideRequest pr: ProvideRequest((Production)agent, pr.GetObjectFromMessage); break;
                // case Instruction.Finished f: agent.TryToFinish(f.GetObjectFromMessage); break;
                default: return false;
            }
            return true;
        }
        private void StartProductionAgent(Production agent, FRequestItem requestItem)
        {
            var firstToEnqueue = false;
            // check for Children
            if (Enumerable.Any<M_ArticleBom>(requestItem.Article.ArticleBoms))
            {
                agent.DebugMessage("Last leave in Bom");
                firstToEnqueue = true;
            }

            // if item hase Workschedules Request ComClient for them
            if (requestItem.Article.WorkSchedules != null)
            {
                // Ask the Directory Agent for Service
                agent.RequestHubAgentFor(workSchedules: requestItem.Article.WorkSchedules);
                // And Create workItems
                agent.CreateWorkItemsFromRequestItem(firstItemToBuild: firstToEnqueue, requestItem);
            }

            var childItems = agent.Get<Queue<FRequestItem>>(Production.Properties.CHILD_WORKITEMS);
            // Create Dispo Agents for Childs.
            foreach (var articleBom in requestItem.Article.ArticleBoms)
            {
                childItems.Enqueue(MessageFactory.ToRequestItem(articleBom, requestItem, agent.Context.Self));

                // create Dispo Agents for to Provide Required Articles
                var agentSetup = AgentSetup.Create(agent, DispoAgent.Behaviour.BehaviourFactory.Get(DB.Enums.SimulationType.None));
                var instruction = Guardian.Guardian.Instruction.CreateChild.Create(agentSetup, agent.Guardian);
                agent.Send(instruction);
            }
            agent.Set(Production.Properties.REQUEST_ITEM, requestItem);
        }

        private void SetHubAgent(Production agent, FHubInformation hub)
        {
            var hubAgents = agent.Get<Dictionary<IActorRef, string>>(Production.Properties.HUB_AGENTS);
            var workItems = agent.Get<List<FWorkItem>>(Production.Properties.WORK_ITEMS);
            // Enque my Element at Comunication Agent
            if (hub == null)
            {
                throw new InvalidCastException(" Could not Cast Comunication agent on InstructionSet.");
            }
            agent.DebugMessage("Recived Agent from Directory: " + agent.Sender.Path.Name);

            // add agent to current Scope.
            hubAgents.Add(hub.Ref, hub.RequiredFor);

            // foreach fitting WorkSchedule
            foreach (var workItem in workItems.Where(x => x.Operation.ResourceSkill.Name == hub.RequiredFor))
            {


                agent.Send(HubAgent.Buckets.Instructions.AddWorkItemToBucket.Create(workItem, hub.Ref));
            }
        }
        private void ProductionStarted(Production agent, FWorkItem workItem)
        {
            var status = agent.Get<ElementStatus>(Production.Properties.ELEMENT_STATUS);
            if (status != workItem.Status)
            {
                status = ElementStatus.Processed;

                foreach (var childs in agent.VirtualChilds)
                {
                    agent.Send(Dispo.Instruction
                                    .WithdrawMaterialsFromStock
                                    .Create(message: "Production Start"
                                            , target: childs.Key));
                }
            }

        }

        private void ProvideRequest(Production agent, FItemStatus itemStatus)
        {
            var requestedItems = agent.Get<List<FRequestItem>>(Production.Properties.REQUESTED_ITEMS);
            var workItems = agent.Get<List<FWorkItem>>(Production.Properties.WORK_ITEMS);
            var requestItem = requestedItems.Single(x => x.Key == itemStatus.ItemId);
            var status = agent.Get<ElementStatus>(Production.Properties.ELEMENT_STATUS);

            agent.DebugMessage("Item to Remove from requestItems: " + requestItem.Article.Name + " --> left " + (workItems.Count() - 1));

            requestedItems.Remove(requestItem);
            if (workItems.Any() && requestedItems.Count() == 0)
            {
                agent.SetWorkItemReady();
            }
            if (status == ElementStatus.Finished)
            {
                agent.TryToFinish();
            }
        }

        private void FinishWorkItem(Production agent, FWorkItem workItem )
        {
            if (workItem == null)
            {
                throw new InvalidCastException("Could not Cast >WorkItemStatus< on InstructionSet.ObjectToProcess");
            }
            agent.DebugMessage("Resource called finished with: " + workItem.Operation.Name + " !");

            // Shortcut:
            //CreateAndEnqueue
            agent.Finished(new FItemStatus(workItem.Key
                                    , workItem.Status
                                    , workItem.ItemPriority));
        }       
    }
}