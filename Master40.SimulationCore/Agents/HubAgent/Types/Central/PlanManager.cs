﻿using System;
using System.Collections.Generic;
using System.Data.HashFunction.xxHash;
using System.Linq;
using System.Text;
using Master40.DB.GanttPlanModel;

namespace Master40.SimulationCore.Agents.HubAgent.Types.Central
{
    public class PlanManager
    {
        public int PlanVersion { get; private set; }

        public PlanManager()
        {
            PlanVersion = 0; // start at first cycle, ++ after each GanttPlan synchronization
        }

        public int IncrementPlaningNumber()
        {
            PlanVersion++;
            return PlanVersion;
        }


    }
}
