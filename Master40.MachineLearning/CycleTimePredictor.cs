using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using Master40.MachineLearning.DataStuctures;

namespace Master40.MachineLearning
{
    public class CycleTimePredictor
    {
        static readonly string rootDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../"));

        // List to compare with the real value
        public List<float[]> predictedActualThroughputList = new List<float[]>();
        long predictedCycleTime = 1920;

        public long PredictCycleTime(SimulationKpis valuesForPrediction)
        {
            // Declare variables
            string uirWebAPI;
            string exceptionMessage;
            string webResponse;

            var cSharpPythonRESTfulAPI = new CSharpPythonRESTfulAPI();

            var kpisForPredict = getReshapedKpisForPrediction(valuesForPrediction);

            // Set the UIR endpoint link. It should go to the application config file 
            uirWebAPI = "http://localhost:5000/predict/cycletime.json";
            exceptionMessage = string.Empty;

            // Get web response by calling the CSharpPythonRestfulApiPredictCycleTime() method
            webResponse = cSharpPythonRESTfulAPI.CSharpPythonRestfulApiPredictCycleTime(uirWebAPI, kpisForPredict, out exceptionMessage);

            if (string.IsNullOrEmpty(exceptionMessage))
            {
                // No errors occurred. Write the string web response     
                Console.WriteLine(webResponse.ToString());
                string cycleTime = JObject.Parse(webResponse)["CycleTime"].ToString();
                double double_predictedCycleTime = double.Parse(cycleTime, System.Globalization.CultureInfo.InvariantCulture);
                predictedCycleTime = Convert.ToInt64(double_predictedCycleTime);
            }
            else
            {
                // An error occurred. Write the exception message
                Console.WriteLine(exceptionMessage);
            }

            predictedActualThroughputList.Add(new float[] { valuesForPrediction.OrderId, predictedCycleTime });
            return predictedCycleTime;
        }

        private SimulationKpisReshaped getReshapedKpisForPrediction(SimulationKpis kpiList)
        {
            var newKpi = new SimulationKpisReshaped
            {
                Assembly = kpiList.Assembly,
                Material = kpiList.Material,
                OpenOrders = kpiList.OpenOrders,
                NewOrders = kpiList.NewOrders,
                TotalWork = kpiList.TotalWork,
                TotalSetup = kpiList.TotalSetup,
                SumDuration = kpiList.SumDuration,
                SumOperations = kpiList.SumOperations,
                ProductionOrders = kpiList.ProductionOrders,
                CycleTime = 0
            };
            return newKpi;
        }
    }
}
