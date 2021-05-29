using Master40.SimulationCore.Environment.Abstractions;
using System;

namespace Master40.SimulationCore.Environment.Options
{
    public class ThroughputPredictionAlgorithm : Option<int>
    {
        public static Type Type => typeof(ThroughputPredictionAlgorithm);
        public ThroughputPredictionAlgorithm(int value)
        {
            _value = value;
        }
    }
}

