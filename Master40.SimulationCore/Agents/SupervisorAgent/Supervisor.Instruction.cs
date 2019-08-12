﻿using Akka.Actor;
using AkkaSim.Definitions;
using Master40.DB.DataModel;
using static FArticles;
using static FSetEstimatedThroughputTimes;

namespace Master40.SimulationCore.Agents.SupervisorAgent
{
    public partial class Supervisor
    {
        public class Instruction
        {
            /*
                CreateContractAgent,
                RequestArticleBom,
                OrderProvided,
            */
            public class CreateContractAgent : SimulationMessage
            {
                public static CreateContractAgent Create(T_CustomerOrderPart message, IActorRef target)
                {
                    return new CreateContractAgent(message, target);
                }
                private CreateContractAgent(T_CustomerOrderPart message, IActorRef target) : base(message, target)
                {
                }
                public T_CustomerOrderPart GetObjectFromMessage { get => Message as T_CustomerOrderPart; }

            }
            public class RequestArticleBom : SimulationMessage
            {
                public static RequestArticleBom Create(FArticle message, IActorRef target)
                {
                    return new RequestArticleBom(message, target);
                }
                private RequestArticleBom(object message, IActorRef target) : base(message, target)
                {
                }
                public FArticle GetObjectFromMessage { get => Message as FArticle; }
            }
            public class OrderProvided : SimulationMessage
            {
                public static OrderProvided Create(FArticle message, IActorRef target)
                {
                    return new OrderProvided(message, target);
                }
                private OrderProvided(object message, IActorRef target) : base(message, target, true)
                {
                }
                public FArticle GetObjectFromMessage { get => Message as FArticle; }
            }

            public class Inizialized : SimulationMessage
            {
                public Inizialized() : base (true, ActorRefs.Nobody) { }
            }

            public class EndSimulation : SimulationMessage
            {
                public static EndSimulation Create(object message, IActorRef target)
                {
                    return new EndSimulation(message, target);
                }
                private EndSimulation(object message, IActorRef target) : base(message, target)
                {
                }
            }

            public class PopOrder : SimulationMessage
            {
                public static PopOrder Create(string message, IActorRef target)
                {
                    return new PopOrder(message, target);
                }
                private PopOrder(object message, IActorRef target) : base(message, target)
                {
                }
            }

            public class SystemCheck : SimulationMessage
            {
                public static SystemCheck Create(string message, IActorRef target)
                {
                    return new SystemCheck(message, target);
                }
                private SystemCheck(object message, IActorRef target) : base(message, target)
                {
                }
            }

            public class SetEstimatedThroughputTime : SimulationMessage
            {
                public static SetEstimatedThroughputTime Create(FSetEstimatedThroughputTime message, IActorRef target)
                {
                    return new SetEstimatedThroughputTime(message, target);
                }
                private SetEstimatedThroughputTime(object message, IActorRef target) : base(message, target)
                {

                }
                public FSetEstimatedThroughputTime GetObjectFromMessage { get => Message as FSetEstimatedThroughputTime; }
            }
        }
    }
}