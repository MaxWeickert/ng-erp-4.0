using Master40.SimulationCore.Environment.Abstractions;
using System;

namespace Master40.SimulationCore.Environment.Options
{
    public class TestArrivalRate : Option<bool>
    {
        public static Type Type => typeof(TestArrivalRate);
        public TestArrivalRate(bool value)
        {
            _value = value;
        }
    }
}
