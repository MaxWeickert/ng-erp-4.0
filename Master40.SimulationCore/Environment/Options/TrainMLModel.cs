using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Master40.SimulationCore.Environment.Abstractions;

namespace Master40.SimulationCore.Environment.Options
{
    public class TrainMLModel : Option<bool>
    {
        public static Type Type => typeof(TrainMLModel);
        public TrainMLModel(bool value)
        {
            _value = value;
        }
    }
}
