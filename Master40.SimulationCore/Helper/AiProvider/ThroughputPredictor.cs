using AiProvider.DataStuctures;
using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using Keras.Models;
using Master40.SimulationCore.Agents;
using Numpy;

namespace Master40.SimulationCore.Helper.AiProvider
{
    public class ThroughputPredictor
    {
        public ThroughputPredictor(){}

        private static string ModelPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).Replace("Master40.XUnitTest\\bin\\Debug\\net5.0",
            "Master40.SimulationCore\\Helper\\AiProvider\\MLModel\\MLModel_OLS.zip");
        private static MLContext mlContext = new MLContext();

        private static string kerasModelPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).Replace("Master40.XUnitTest\\bin\\Debug\\net5.0",
            "Master40.SimulationCore\\Helper\\AiProvider\\MLModelNN");

        private static BaseModel model;

        public static void LoadModel()
        {
            model = Sequential.LoadModel(kerasModelPath);
            model.LoadWeight(kerasModelPath + "\\simpleModelCheckpoint.h5");
        }


        private List<float[]> predictedActualThroughputList = new List<float[]>();

        public long PredictThroughput(List<SimulationKpis> valuesForPrediction, Agent agent)
        {
            var predictedThroughput = PredictWithNeuralNetwork(valuesForPrediction, agent);
            /*            var kpisForPredict = getReshapedKpisForPrediction(valuesForPrediction);

                        ITransformer trainedModel = mlContext.Model.Load(ModelPath, out var modelInputSchema);

                        // Create prediction engine related to the loaded trained model.
                        var predEngine = mlContext.Model.CreatePredictionEngine<SimulationKpisReshaped, CycleTimePrediction>(trainedModel);

                        var resultPrediction = predEngine.Predict(kpisForPredict);


                        // Compare actual Value and predicted Value
            *//*            if (predictedActualThroughputList.Count == 0)
                        {
                            predictedActualThroughputList.Add(new float[] { valuesForPrediction.Last().Time, resultPrediction.CycleTime, 0 });
                        }
                        else
                        {
                            predictedActualThroughputList.Last()[2] =
                                valuesForPrediction.Find(v => v.Time == predictedActualThroughputList.Last()[0] + 480).CycleTime;
                            agent.DebugMessage(JsonConvert.SerializeObject(predictedActualThroughputList.Last()), CustomLogger.AIPREDICTIONS, LogLevel.Info);
                            var newEntry = new float[] {valuesForPrediction.Last().Time, resultPrediction.CycleTime, 0};
                            predictedActualThroughputList.Add(newEntry);
                        }*//*

                        return (long)Math.Round(resultPrediction.CycleTime, 0);*/
            return predictedThroughput;
        }

        private long PredictWithNeuralNetwork(List<SimulationKpis> valuesForPrediction, Agent agent)
        {
            return 10160;
            if (valuesForPrediction.Any())
            {
                NDarray array = np.array(new double[,,]
                {
                    {
                        {
                            valuesForPrediction.Last().Assembly,
                            valuesForPrediction.Last().Material,
                            valuesForPrediction.Last().OpenOrders,
                            valuesForPrediction.Last().NewOrders,
                            valuesForPrediction.Last().TotalWork,
                            valuesForPrediction.Last().TotalSetup,
                            valuesForPrediction.Last().SumDuration,
                            valuesForPrediction.Last().SumOperations,
                            valuesForPrediction.Last().ProductionOrders,
                            valuesForPrediction.Last().CycleTime
                        }
                    }
                });

                //model.LoadWeight(kerasModelPath + "\\model_checkpoint.h5");
                var predictionData = model.Predict(array);

                // Compare actual Value and predicted Value
                if (predictedActualThroughputList.Count == 0)
                {
                    predictedActualThroughputList.Add(new float[]
                        {valuesForPrediction.Last().Time, predictionData.GetData<float>()[0], 0});
                }
                else
                {
                    predictedActualThroughputList.Last()[2] =
                        valuesForPrediction.Find(v => v.Time == predictedActualThroughputList.Last()[0] + 480)
                            .CycleTime;
                    //agent.DebugMessage(JsonConvert.SerializeObject(predictedActualThroughputList.Last()), CustomLogger.AIPREDICTIONS, LogLevel.Info);
                    var newEntry = new float[] {valuesForPrediction.Last().Time, predictionData.GetData<float>()[0], 0};
                    predictedActualThroughputList.Add(newEntry);
                }

                return (long) Math.Round(predictionData.GetData<float>()[0], 0);
            }
            else
            {
                return 1920;
            }
        }

        /*        private SimulationKpisReshaped getReshapedKpisForPrediction(List<SimulationKpis> kpiList)
                {
                    var newKpi = new SimulationKpisReshaped
                    {
                        Assembly_t0 = kpiList.Last().Assembly,
                        Assembly_t1 = kpiList[^2].Assembly,
                        Assembly_t2 = kpiList[^3].Assembly,
        *//*                Consumab_t0 = kpiList.Last().Consumabl,
                        Consumab_t1 = kpiList[^2].Consumabl,
                        Consumab_t2 = kpiList[^3].Consumalb,
                        CycleTime_t0 = 0,
                        CycleTime_t1 = kpiList.Last().CycleTime,
                        CycleTime_t2 = kpiList[^2].CycleTime,
                        InDueTotal_t0 = kpiList.Last().InDueTotal,
                        InDueTotal_t1 = kpiList[^2].InDueTotal,
                        InDueTotal_t2 = kpiList[^3].InDueTotal,
                        Lateness_t0 = kpiList.Last().Lateness,
                        Lateness_t1 = kpiList[^2].Lateness,
                        Lateness_t2 = kpiList[^3].Lateness,
                        Material_t0 = kpiList.Last().Material,
                        Material_t1 = kpiList[^2].Material,
                        Material_t2 = kpiList[^3].Material,
                        Total_t0 = kpiList.Last().Total,
                        Total_t1 = kpiList[^2].Total,
                        Total_t2 = kpiList[^3].Total*//*
                    };
                    return newKpi;
                }*/
    }
}