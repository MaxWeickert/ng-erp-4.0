using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Master40.MachineLearning.DataStuctures;
using Microsoft.ML;
using Microsoft.ML.Data;
using Master40.SimulationCore.Agents;
using Microsoft.ML.Transforms;
using static Tensorflow.Binding;
using static Tensorflow.KerasApi;
using Tensorflow;
using Tensorflow.Keras;
using NumSharp;

namespace Master40.MachineLearning
{
    static class Program
    {

        static string rootDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../"));
        //static readonly string _dataPath = Path.Combine(Environment.CurrentDirectory, "Data");
        static readonly string _trainDataPath = Path.Combine(rootDir, "Data/train.csv");
        static readonly string _evalDataPath = Path.Combine(rootDir, "Data/eval.csv");
        static readonly string _modelPath = Path.Combine(rootDir, "MLModel");
        //static readonly string _modelPath = Path.Combine(rootDir, "MLModel/ML_FastTreeRegression_0965.zip");

        private static MLContext mlContext = new MLContext();

        //private static IDataView trainDataView = mlContext.Data.LoadFromTextFile<SimulationKpisReshaped>(_trainDataPath, hasHeader: true, separatorChar: ',');
        //private static IDataView evalDataView = mlContext.Data.LoadFromTextFile<SimulationKpisReshaped>(_evalDataPath, hasHeader: true, separatorChar: ',');

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
                //CycleTime = 0

/*                            Assembly = (float)18.279465,
                Material = (float)129014860,
                OpenOrders = 10,
                NewOrders = 1,
                TotalWork = (float)2.24,
                TotalSetup = (float)5.1,
                SumDuration = 147,
                SumOperation = 15,
                ProductionOrders = 3,
                CycleTime = 0*/
            };

            PredictCycleTime(newKpi);

        }

        public static void PredictCycleTime(SimulationKpisReshaped valuesForPrediction)
        {
            //var kpisForPredict = getReshapedKpisForPrediction(valuesForPrediction);

            //ITransformer tensorFlowModel = mlContext.Model.Load(_modelPath, out var modelInputSchema);

            TensorFlowModel tensorFlowModel = mlContext.Model.LoadTensorFlowModel(_modelPath);

/*            DataViewSchema schema = tensorFlowModel.GetModelSchema();

            Console.WriteLine(" =============== TensorFlow Model Schema =============== ");

            var featuresType = (VectorDataViewType)schema["features"].Type;
            Console.WriteLine($"Name: Features, Type: {featuresType.ItemType.RawType}, Size: ({featuresType.Dimensions[0]})");

            var predictionType = (VectorDataViewType)schema["Prediction/Softmax"].Type;

            Console.WriteLine($"Name: Prediction/Softmax, Type: {predictionType.ItemType.RawType}, Size: ({predictionType.Dimensions[0]})");
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();*/

/*            var predictor = mlContext.Model.CreatePredictionEngine<SimulationKpisReshaped, CycleTimePrediction>(tensorFlowModel);
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

        private static void TrainTensorFlowModel()
        {
            //parameters
            var training_steps = 1000;
            var learning_rate = 0.01f;
            var display_step = 100;

            //Load Data
            var train_data = 0;
            var eval_data = 0;

            var optimizer = keras.optimizers.SGD(learning_rate);

            // run traning for number of steps
            foreach (var step in range(1, training_steps + 1))
            {

            }

            //var train = np.
        }
    }
}
