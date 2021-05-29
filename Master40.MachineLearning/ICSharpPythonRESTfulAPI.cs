using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Master40.MachineLearning.DataStuctures;

namespace Master40.MachineLearning
{
    public interface ICSharpPythonRESTfulAPI
    {
        string CSharpPythonRestfulApiPredictCycleTime(string uirWebAPI, SimulationKpisReshaped kpis, out string exceptionMessage);
    }
}
