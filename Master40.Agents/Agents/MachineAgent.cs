﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Master40.Agents.Agents.Internal;
using Master40.Agents.Agents.Model;
using Master40.DB.Models;
using Microsoft.AspNetCore.Http.Features;

namespace Master40.Agents.Agents
{
    public class MachineAgent : Agent
    {

        // Agent to register your Services
        private readonly DirectoryAgent _directoryAgent;
        private ComunicationAgent _comunicationAgent;
        private Machine Machine { get; }
        private int ProgressQueueSize { get; }
        public List<WorkItem> Queue { get; }
        //private Queue<WorkItem> SchduledQueue;
        private List<WorkItem> ProcessingQueue { get; }
        private bool ItemsInProgess { get; set; }


        public enum InstuctionsMethods
        {
            SetComunicationAgent,
            RequestProposal,
            AcknowledgeProposal,
            StartWorkWith,
            DoWork,
            FinishWork
        }

        public MachineAgent(Agent creator, string name, bool debug, DirectoryAgent directoryAgent, Machine machine) : base(creator, name, debug)
        {
            _directoryAgent = directoryAgent;
            ProgressQueueSize = 1; // TODO COULD MOVE TO MODEL for CONFIGURATION, May not required anymore
            Queue = new List<WorkItem>();  // ThenBy( x => x.Status)
            //SchduledQueue = new Queue<WorkItem>();
            Machine = machine;
            ItemsInProgess = false;
            RegisterService();
        }


        /// <summary>
        /// Register the Machine in the System.
        /// </summary>
        public void RegisterService()
        {
            _directoryAgent.InstructionQueue.Enqueue(new InstructionSet
            {
                MethodName = DirectoryAgent.InstuctionsMethods.GetOrCreateComunicationAgentForType.ToString(),
                ObjectToProcess = this.Machine.MachineGroup.Name,
                ObjectType = typeof(string),
                SourceAgent = this
            });
        }


        /// <summary>
        /// Callback
        /// </summary>
        /// <param name="objects"></param>
        private void SetComunicationAgent(InstructionSet objects)
        {
            // save Cast to expected object
            _comunicationAgent  = objects.ObjectToProcess as ComunicationAgent;

            // throw if cast failed.
            if (_comunicationAgent == null)
                throw new ArgumentException("Could not Cast Communication Agent from InstructionSet.ObjectToProcess");

            // Debug Message
            DebugMessage("Successfull Registred Service at : " + _comunicationAgent.Name);
        }

        /// <summary>
        /// Is Called from Comunication Agent to get an Proposal when the item with a given priority can be scheduled.
        /// </summary>
        /// <param name="instructionSet"></param>
        private void RequestProposal(InstructionSet instructionSet)
        {
            var workItem = instructionSet.ObjectToProcess as WorkItem;
            if (workItem == null)
                throw new InvalidCastException("Could not Cast Workitem on InstructionSet.ObjectToProcess");
         
            // debug
            DebugMessage("Request for Proposal");

            // calculat Proposal.
            
            var proposal = new Proposal { AgentId = this.AgentId,
                                       WorkItemId = workItem.Id,
                                 PossibleSchedule = this.Queue.Sum(x => x.WorkSchedule.Duration) };
            // callback 
            CreateAndEnqueueInstuction(methodName: ComunicationAgent.InstuctionsMethods.ProposalFromMachine.ToString(),
                                  objectToProcess: proposal,
                                      targetAgent: instructionSet.SourceAgent);
        }

        /// <summary>
        /// is Called if The Proposal is accepted by Comunication Agent
        /// </summary>
        /// <param name="instructionSet"></param>
        private void AcknowledgeProposal(InstructionSet instructionSet)
        {
            var workItem = instructionSet.ObjectToProcess as WorkItem;
            if (workItem == null)
                throw new InvalidCastException("Could not Cast Workitem on InstructionSet.ObjectToProcess");

            DebugMessage("AcknowledgeProposal and Enqueued Item: " + workItem.WorkSchedule.Name);

            Queue.Add(workItem);
            // Enqued before another item?
            
            var position = Queue.OrderBy(x => x.Priority).ToList().IndexOf(workItem);
            DebugMessage("Position: " + position + " Priority:"+ workItem.Priority + " Queue length " + Queue.Count());


            // reorganize Queue if an Element has ben Queued which is More Important.
            if (position + 1 < Queue.Count())
            {
                var toRequeue = Queue.OrderBy(x => x.Priority).ToList().GetRange(position + 1, Queue.Count() - position - 1);
                foreach (var reqItem in toRequeue)
                {
                    DebugMessage("-> ToRequeue " + reqItem.Priority + " Current Possition: " + Queue.OrderBy(x => x.Priority).ToList().IndexOf(reqItem) + " Id " + reqItem.Id);

                    // remove item from current Queue
                    Queue.Remove(reqItem);
                    // reset Agent Status
                    reqItem.Status = Status.Created;
                    // Call Comunication Agent to Requeue
                    CreateAndEnqueueInstuction(methodName: ComunicationAgent.InstuctionsMethods.EnqueueWorkItem.ToString(),
                                          objectToProcess: reqItem,
                                              targetAgent: reqItem.ComunicationAgent);
                }

                DebugMessage("New Queue length = " + Queue.Count);
                foreach (var q in Queue)
                {
                    DebugMessage("--> Position" + Array.IndexOf(Queue.OrderBy(x => x.Priority).ToArray(), q) + " Priority " + q.Priority + " Id " + q.Id);
                }
            }
        }

        private void StartWorkWith(InstructionSet instructionSet)
        {
            var workItemStatus = instructionSet.ObjectToProcess as WorkItemStatus;
            if (workItemStatus == null)
            {
                throw new InvalidCastException("Could not Cast >WorkItemStatus< on InstructionSet.ObjectToProcess");
            }
            // update Status
            var workItem = Queue.FirstOrDefault(x => x.Id == workItemStatus.WorkItemId);
            
            DebugMessage("Set Item: " + workItem.WorkSchedule.Name + " | Status to: " + workItem.Status);
            // if 
            if (workItem.Status == Status.Ready)
            {
                // there is at least Something Ready so Start Work
                DoWork(new InstructionSet());
                DebugMessage("Call for Work");
            }
        }


        private void DoWork(InstructionSet instructionSet)
        {
            if (ItemsInProgess) { 
                DebugMessage("Im still working....");
                return; // still working
            }
            
            // get next Ready Element from Queue
            var item = Queue.Where(x => x.Status == Status.Ready).OrderBy(x => x.Priority).FirstOrDefault();

            // Wait if nothing More todo.
            if (item == null)
            {
                // No more work 
                DebugMessage("Nothing more Ready in Queue!");
                return;
            }

            ItemsInProgess = true;
            item.Status = Status.Processed;
            // get item = ready and lowest priority
            CreateAndEnqueueInstuction(methodName: MachineAgent.InstuctionsMethods.FinishWork.ToString(),
                                  objectToProcess: item,
                                      targetAgent: this,
                                          waitFor: item.WorkSchedule.Duration);


            //TODO Something in Queue but not ready --> pass Instruction

        }

        private void FinishWork(InstructionSet instructionSet)
        {
            var item = instructionSet.ObjectToProcess as WorkItem;
            if (item == null)
            {
                throw new InvalidCastException("Could not Cast >WorkItemStatus< on InstructionSet.ObjectToProcess");
            }

            // Set Machine State to Ready for next
            ItemsInProgess = false;
            DebugMessage("Finished Work with "+ item.WorkSchedule.Name + " take next...");

            // Set Finish and Remove from Queue
            item.Status = Status.Finished;
            Queue.Remove(item);
            // Call Comunication Agent that item has ben processed.
            CreateAndEnqueueInstuction(methodName: ComunicationAgent.InstuctionsMethods.FinishWorkItem.ToString(),
                                  objectToProcess: item,
                                      targetAgent: item.ComunicationAgent);

            // do More Work
            DoWork(new InstructionSet());
            


        }

    }
}