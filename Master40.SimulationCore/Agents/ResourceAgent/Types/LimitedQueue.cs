﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static IJobs;

namespace Master40.SimulationCore.Agents.ResourceAgent.Types
{
    public class LimitedQueue
    {
        internal List<IJob> jobs = new List<IJob>();
        public int Limit { get; set; }
        public bool CapacitiesLeft => (Limit > jobs.Count) ? true : false;

        public LimitedQueue(int limit)
        {
            Limit = limit;
        }
        public IJob Dequeue(long currentTime)
        {
            if (this.jobs.Count == 0) return null;

            var item = this.jobs.OrderBy(x => x.Priority(currentTime)).Single();
            this.jobs.Remove(item);
            return item;
        }
    }

}