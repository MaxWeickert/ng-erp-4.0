using AiProvider.DataStuctures;
using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
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
        private static readonly double[] MEAN = { 8.38349267e+01, 7.14052754e+08, 5.58996266e+00, 9.59101222e+00, 2.21157340e+02, 2.19141673e+01, 4.38283347e+00, 3.19661680e+03 };
        private static readonly double[] STD = { 2.11527778e+01, 1.80308080e+08, 2.40437363e+00, 2.29533925e+00, 8.77253571e+01, 8.74016707e+00, 1.74803341e+00, 1.65355304e+03 };
        private NDarray array;
        public static void LoadModel()
        {
            model = BaseModel.LoadModel(kerasModelPath, compile: true);
            model.LoadWeight(kerasModelPath + "\\simpleModelCheckpoint.h5");
        }


        private List<float[]> predictedActualThroughputList = new List<float[]>();

        public long PredictThroughput(SimulationKpis valuesForPrediction, Agent agent)
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

        private long PredictWithNeuralNetwork(SimulationKpis valuesForPrediction, Agent agent)
        {
            //return 10160;
            array = np.array(new double[,,]
            {
                {
                    {
                        valuesForPrediction.Assembly,
                        valuesForPrediction.Material,
                        //valuesForPrediction.OpenOrders,
                        //valuesForPrediction.NewOrders,
                        valuesForPrediction.TotalWork,
                        valuesForPrediction.TotalSetup,
                        valuesForPrediction.SumDuration,
                        valuesForPrediction.SumOperations,
                        valuesForPrediction.ProductionOrders
                    }
                }
            });

            var normalizedArray = (array - MEAN[..^1])/STD[..^1];

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
            

            //var predictionData = model.PredictOnBatch(normalizedArray);
            //var predictedThroughput = predictionData.GetData<float>()[0];
            //var denormalizedThroughput = predictedThroughput * STD[^1] + MEAN[^1];

            double denormalizedThroughput;
            try
            {
                var predictionData = model.PredictOnBatch(normalizedArray);
                var predictedThroughput = predictionData.GetData<float>()[0];
                denormalizedThroughput = predictedThroughput * STD[^1] + MEAN[^1];
            }
            catch(Exception e)
            {
                Console.WriteLine("{0} Exception caught.", e);
                denormalizedThroughput = 10160;
            }

            return (long)Math.Round(denormalizedThroughput, 0);
        }

        public static void TrainNeuralNetwork(List<SimulationKpis> valuesForTraining)
        {
            NDarray xArray = np.array(new double[,] { });
            NDarray yArray = np.array(new double[,] { });
            valuesForTraining.ForEach(v =>
            {
                np.append(xArray, new double[,]{{
                    v.Assembly,
                    v.Material,
                    v.OpenOrders,
                    v.NewOrders,
                    v.TotalWork,
                    v.TotalSetup,
                    v.SumDuration,
                    v.SumOperations,
                    v.ProductionOrders
                }});
        //np.append(xArray, np.array(new double[,] {
                //{
                //    v.Assembly,
                //    v.Material,
                //    v.OpenOrders,
                //    v.NewOrders,
                //    v.TotalWork,
                //    v.TotalSetup,
                //    v.SumDuration,
                //    v.SumOperations,
                //    v.ProductionOrders
                //}}));
                //np.append(yArray, new double[,]{{v.CycleTime}});
            });

            NDarray normalizedX = (xArray - MEAN) / STD;
            NDarray normalizedY = (yArray - MEAN.Last()) / STD.Last();

            model.Fit(normalizedX, normalizedY, epochs: 20, verbose: 1);
            model.Save(kerasModelPath);
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