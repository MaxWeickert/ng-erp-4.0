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
using Newtonsoft.Json;

namespace Master40.SimulationCore.Helper.AiProvider
{
    public class ThroughputPredictor
    {
        public ThroughputPredictor(){}

        private static string ModelPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).Replace("Master40.XUnitTest\\bin\\Debug\\net5.0",
            "Master40.SimulationCore\\Helper\\AiProvider\\MLModel\\ML_FastTreeRegression_0965.zip");
        private static MLContext mlContext = new MLContext();

        //private static string kerasModelPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location).Replace("Master40.XUnitTest\\bin\\Debug\\net5.0",
        //    "Master40.SimulationCore\\Helper\\AiProvider\\MLModelNN");

        private static BaseModel model;

        /*public static void LoadModel()
        {
            model = Sequential.LoadModel(kerasModelPath);
            model.LoadWeight(kerasModelPath + "\\simpleModelCheckpoint.h5");
        }*/


        public List<float[]> predictedActualThroughputList = new List<float[]>();

        public long PredictThroughput(SimulationKpis valuesForPrediction, Agent agent)
        {
            //var predictedThroughput = PredictWithNeuralNetwork(valuesForPrediction, agent);
            var kpisForPredict = getReshapedKpisForPrediction(valuesForPrediction);

            ITransformer trainedModel = mlContext.Model.Load(ModelPath, out var modelInputSchema);

            // Create prediction engine related to the loaded trained model.
            var predEngine = mlContext.Model.CreatePredictionEngine<SimulationKpisReshaped, CycleTimePrediction>(trainedModel);

            var resultPrediction = predEngine.Predict(kpisForPredict);


            // Compare actual Value and predicted Value
            //if (predictedActualThroughputList.Count == 0)
            //{
                predictedActualThroughputList.Add(new float[] { valuesForPrediction.OrderId, (long)Math.Round(resultPrediction.CycleTime, 0) });
/*            }
            else
            {
                *//*                predictedActualThroughputList.Last()[2] =
                                    valuesForPrediction.Find(v => v.Time == predictedActualThroughputList.Last()[0] + 480).CycleTime;
                                agent.DebugMessage(JsonConvert.SerializeObject(predictedActualThroughputList.Last()), CustomLogger.AIPREDICTIONS, LogLevel.Info);
                                var newEntry = new float[] { valuesForPrediction.Last().Time, resultPrediction.CycleTime, 0 };
                                predictedActualThroughputList.Add(newEntry);*//*
                predictedActualThroughputList.Last()[2] = valuesForPrediction.CycleTime;
                //agent.DebugMessage(JsonConvert.SerializeObject(predictedActualThroughputList.Last()), CustomLogger.AIPREDICTIONS, LogLevel.Info);
                var newEntry = new float[] { valuesForPrediction.OrderId, (long)Math.Round(resultPrediction.CycleTime, 0) };
                predictedActualThroughputList.Add(newEntry);
            }*/

            return (long)Math.Round(resultPrediction.CycleTime, 0);
            //return predictedThroughput;
        }

/*        public <List>[]float getPredictedList()
        {

        }*/

        private long PredictWithNeuralNetwork(SimulationKpis valuesForPrediction, Agent agent)
        {
            return 10160;
            NDarray array = np.array(new double[,,]
            {
                {
                    {
                        valuesForPrediction.Assembly,
                        valuesForPrediction.Material,
                        valuesForPrediction.OpenOrders,
                        valuesForPrediction.NewOrders,
                        valuesForPrediction.TotalWork,
                        valuesForPrediction.TotalSetup,
                        valuesForPrediction.SumDuration,
                        valuesForPrediction.SumOperations,
                        valuesForPrediction.ProductionOrders,
                        valuesForPrediction.CycleTime
                    }
                }
            });
            
            var predictionData = model.Predict(array);

            // Compare actual Value and predicted Value
            //if (predictedActualThroughputList.Count == 0)
            //{
            //    predictedActualThroughputList.Add(new float[]
            //        {valuesForPrediction.Last().Time, predictionData.GetData<float>()[0], 0});
            //}
            //else
            //{
            //    predictedActualThroughputList.Last()[2] =
            //        valuesForPrediction.Find(v => v.Time == predictedActualThroughputList.Last()[0] + 480)
            //            .CycleTime;
            //    //agent.DebugMessage(JsonConvert.SerializeObject(predictedActualThroughputList.Last()), CustomLogger.AIPREDICTIONS, LogLevel.Info);
            //    var newEntry = new float[] {valuesForPrediction.Time, predictionData.GetData<float>()[0], 0};
            //    predictedActualThroughputList.Add(newEntry);
            //}

            return (long) Math.Round(predictionData.GetData<float>()[0], 0);
        }

        private void trainNeuralNetwork()
        {

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
                SumOperation = kpiList.SumOperations,
                ProductionOrders = kpiList.ProductionOrders,
                CycleTime = 0
            };
            return newKpi;
        }
    }
}