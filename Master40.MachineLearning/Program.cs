using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Master40.MachineLearning.DataStuctures;
using Microsoft.ML;
using Microsoft.ML.Data;
using Master40.SimulationCore.Agents;
using Microsoft.ML.Transforms;
using Tensorflow;

namespace Master40.MachineLearning
{
    static class Program
    {

        static string rootDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../"));
        //static readonly string _dataPath = Path.Combine(Environment.CurrentDirectory, "Data");
        static readonly string _trainDataPath = Path.Combine(rootDir, "Data/train.csv");
        static readonly string _evalDataPath = Path.Combine(rootDir, "Data/eval.csv");
        static readonly string _modelPath = Path.Combine(rootDir, "MLModel/ML_FastTreeRegression_0965.zip");

        private static MLContext mlContext = new MLContext();

        private static IDataView trainDataView = mlContext.Data.LoadFromTextFile<SimulationKpis>(_trainDataPath, hasHeader: true, separatorChar: ',');
        private static IDataView evalDataView = mlContext.Data.LoadFromTextFile<SimulationKpis>(_evalDataPath, hasHeader: true, separatorChar: ',');

        static void Main(string[] args)
        {
            var newKpi = new SimulationKpisReshaped
            {
                Assembly = (float)-2.959124,
                Material = (float)-3.104468,
                OpenOrders = (float)-0.418942,
                NewOrders = (float)-0.660784,
                TotalWork = (float)-1.314030,
                TotalSetup = (float)-1.713337,
                SumDuration = (float)-0.847003,
                SumOperation = (float)-0.792846,
                ProductionOrders = (float)-0.792846,
                CycleTime = 0

                            Assembly = (float)18.279465,
                Material = (float)129014860,
                OpenOrders = 10,
                NewOrders = 1,
                TotalWork = (float)2.24,
                TotalSetup = (float)5.1,
                SumDuration = 147,
                SumOperation = 15,
                ProductionOrders = 3,
                CycleTime = 0
            };

            //PredictCycleTime(newKpi);

        }

        public static void PredictCycleTime(SimulationKpisReshaped valuesForPrediction)
        {
            //var kpisForPredict = getReshapedKpisForPrediction(valuesForPrediction);

            ITransformer trainedModel = mlContext.Model.Load(_modelPath, out var modelInputSchema);

            TensorFlowModel tensorFlowModel = mlContext.Model.LoadTensorFlowModel(_modelPath);

            DataViewSchema schema = tensorFlowModel.GetModelSchema();

            Console.WriteLine(" =============== TensorFlow Model Schema =============== ");

            var featuresType = (VectorDataViewType)schema["Features"].Type;
            Console.WriteLine($"Name: Features, Type: {featuresType.ItemType.RawType}, Size: ({featuresType.Dimensions[0]})");

            var predictionType = (VectorDataViewType)schema["Prediction/Softmax"].Type;

            Console.WriteLine($"Name: Prediction/Softmax, Type: {predictionType.ItemType.RawType}, Size: ({predictionType.Dimensions[0]})");
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();

            /*            ITransformer trainedModel = mlContext.Model.Load(_modelPath, out var modelInputSchema);

                        var predictor = mlContext.Model.CreatePredictionEngine<SimulationKpisReshaped, CycleTimePrediction>(trainedModel);
                        var prediction = predictor.Predict(valuesForPrediction);

                        Console.WriteLine($"Predicted as: {prediction.CycleTime} ");*/
        }

        private static SimulationKpisReshaped getReshapedKpisForPrediction(SimulationKpis kpiList)
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
