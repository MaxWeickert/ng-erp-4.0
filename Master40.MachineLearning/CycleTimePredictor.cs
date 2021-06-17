﻿using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Reflection;
using Master40.MachineLearning.DataStuctures;
using Microsoft.ML;

namespace Master40.MachineLearning
{
    public class CycleTimePredictor
    {
        private static string ModelPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).Replace("Master40.XUnitTest\\bin\\Debug\\net5.0",
            "Master40.MachineLearning\\MLModels\\ML_SdcaRegression_08916.zip"); //ML_SdcaRegression_08916.zip //ML_FastTreeTweedieRegression_08938.zip
        private static MLContext mlContext = new MLContext();

        // List to compare with the real value
        public List<float[]> predictedActualThroughputList = new List<float[]>();
        long predictedCycleTime = 1920;

        public long PredictCycleTime(SimulationKpis valuesForPrediction, int throughputPredictionAlgorithm, SimulationKpis valuesForTraining, bool trainModel)
        {
            var kpisForPredict = getReshapedKpisForPrediction(valuesForPrediction, false);

            var kpisForTraining = getReshapedKpisForPrediction(valuesForTraining, true);

            if (throughputPredictionAlgorithm == 0)
            {
                // Declare variables
                string uirWebAPI;
                string exceptionMessage;
                string webResponse;

                string trainResponse;
                string uriTrainApi;
                string trainExceptionMessage;

                var cSharpPythonRESTfulAPI = new CSharpPythonRESTfulAPI();

                // Set the UIR endpoint link. It should go to the application config file 
                uirWebAPI = "http://localhost:5000/predict/cycletime.json";
                exceptionMessage = string.Empty;

                uriTrainApi = "http://localhost:5000/train/cycletime.json";
                trainExceptionMessage = string.Empty;

                // Get web response by calling the CSharpPythonRestfulApiPredictCycleTime() method
                webResponse = cSharpPythonRESTfulAPI.CSharpPythonRestfulApiPredictCycleTime(uirWebAPI, kpisForPredict, out exceptionMessage);

                if (string.IsNullOrEmpty(exceptionMessage))
                {
                    // No errors occurred. Write the string web response     
                    Console.WriteLine(webResponse.ToString());
                    string cycleTime = JObject.Parse(webResponse)["CycleTime"].ToString();
                    double double_predictedCycleTime = double.Parse(cycleTime, System.Globalization.CultureInfo.InvariantCulture);
                    predictedCycleTime = Convert.ToInt64(double_predictedCycleTime);

                    if (trainModel)
                    {
                        trainResponse = cSharpPythonRESTfulAPI.CSharpPythonRestfulApiPredictCycleTime(uriTrainApi,
                            kpisForTraining, out trainExceptionMessage);

                        if (string.IsNullOrEmpty(trainExceptionMessage))
                        {
                            Console.WriteLine(trainResponse);
                        }
                        else
                        {
                            Console.WriteLine(trainExceptionMessage);
                        }
                    }
                }
                else
                {
                    // An error occurred. Write the exception message
                    Console.WriteLine(exceptionMessage);
                }
            }
            else if (throughputPredictionAlgorithm == 1)
            {
                ITransformer trainedModel = mlContext.Model.Load(ModelPath, out var modelInputSchema);

                // Create prediction engine related to the loaded trained model.
                var predEngine = mlContext.Model.CreatePredictionEngine<SimulationKpisReshaped, CycleTimePrediction>(trainedModel);

                var resultPrediction = predEngine.Predict(kpisForPredict);

                predictedCycleTime = (long)Math.Round(resultPrediction.CycleTime * 1.2, 0);
            }

            // Create List with predicted cycletimes for specific orders to compare later 
            predictedActualThroughputList.Add(new float[] { valuesForPrediction.OrderId, predictedCycleTime });

            return predictedCycleTime;
        }

        private SimulationKpisReshaped getReshapedKpisForPrediction(SimulationKpis kpiList, bool train)
        {
            float cycleTime;
            if (train)
            {
                cycleTime = kpiList.FinishingTime - kpiList.CreationTime;
            }
            else
            {
                cycleTime = 0;
            }
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
                CycleTime = cycleTime
            };
            return newKpi;
        }
    }
}