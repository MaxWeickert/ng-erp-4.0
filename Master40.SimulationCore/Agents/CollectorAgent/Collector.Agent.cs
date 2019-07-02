﻿using System;
using System.Collections.Generic;
using Akka.Actor;
using AkkaSim;
using Master40.DB.Data.Context;
using Master40.SimulationCore.Environment;
using Master40.SimulationCore.Environment.Options;
using Master40.SimulationCore.Helper;
using Master40.Tools.SignalR;

namespace Master40.SimulationCore.Agents.CollectorAgent
{
    public partial class Collector : SimulationMonitor
    {
        internal long Time => this._Time;
        private ICollectorBehaviour Behaviour;
        internal IMessageHub messageHub { get; }
        internal MasterDBContext DBContext;
        internal Configuration Config;
        
        internal SimulationId simulationId;
        internal SimulationNumber simulationNumber;
        internal SimulationKind simulationKind;
        internal SaveToDB saveToDB;
       
        internal new IUntypedActorContext Context => UntypedActor.Context;
        /// <summary>
        /// Collector Agent for Life data Aquesition
        /// </summary>
        /// <param name="actorPaths"></param>
        /// <param name="time">Current time span</param>
        /// <param name="debug">Parameter to activate Debug Messages on Agent level</param>
        public Collector(ActorPaths actorPaths
            , ICollectorBehaviour collectorBehaviour
            , IMessageHub msgHub
            , MasterDBContext dBContext
            , Configuration configuration
            , long time
            , List<Type> streamTypes)
            : base(time, streamTypes)
        {
            Console.WriteLine("I'm alive: " + Self.Path.ToStringWithAddress());
            Behaviour = collectorBehaviour;
            messageHub = msgHub;
            DBContext = dBContext;
            Config = configuration;
            simulationId = Config.GetOption<SimulationId>();
            simulationNumber = Config.GetOption<SimulationNumber>();
            simulationKind = Config.GetOption<SimulationKind>();
            saveToDB = Config.GetOption<SaveToDB>();
        }

        public static Props Props(ActorPaths actorPaths
            , ICollectorBehaviour collectorBehaviour
            , IMessageHub msgHub
            , MasterDBContext dBContext
            , Configuration configuration
            , long time
            , bool debug
            , List<Type> streamTypes)
        {
            return Akka.Actor.Props.Create(
                () => new Collector(actorPaths, collectorBehaviour, msgHub, dBContext, configuration, time, streamTypes));
        }

        protected override void EventHandle(object o)
        {
            Behaviour.EventHandle(this, o);
        }
       
    }
}