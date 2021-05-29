using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
//using Common;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;
using Master40.MachineLearning.DataStuctures;
using Master40.MachineLearning.Helper;
using static Master40.MachineLearning.Helper.ProgressHandler;

namespace Master40.MachineLearning
{
    //Execute Master40.MachineLearning to run AutoML Experiment

    static class TrainAutoML
    {
        private static string rootDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../"));

        // Path of the trained models
        private static string ModelPath = Path.Combine(rootDir, "MLModels/");
        private static string trainDataPath = Path.Combine(rootDir, "Data/train.csv");

        private static MLContext mlContext = new MLContext();

        private static IDataView trainDataView = mlContext.Data.LoadFromTextFile<SimulationKpisReshaped>(trainDataPath, hasHeader: true, separatorChar: ',');

        private static string targetColumnName = "CycleTime"; // Target Column; Variable which should be forecasted

        // Experiment Time for AutoMLExperiment
        private static uint experimentTime = 300; // If > 300 RunAutoMLExperiment will sometimes not finish

        static void Main(string[] args)
        {
            // Run an AutoML experiment on the dataset.
            var experimentResult = RunAutoMLExperiment(mlContext);

            // Save / persist the best model to a .ZIP file. !This will override the actual .ZIP file
            SaveModel(mlContext, experimentResult.BestRun.Model, experimentResult.BestRun.TrainerName, experimentResult.BestRun.ValidationMetrics.RSquared); 

            Console.WriteLine("Press any key to exit..");
            Console.ReadLine();
        }


        private static ExperimentResult<RegressionMetrics> RunAutoMLExperiment(MLContext mlContext)
        {
            // Display first few rows of the training data
            ConsoleHelper.ShowDataViewInConsole(mlContext, trainDataView);

            // Initialize our user-defined progress handler that AutoML will 
            // invoke after each model it produces and evaluates.
            var progressHandler = new RegressionExperimentProgressHandler();

            // Run AutoML regression experiment
            ConsoleHelper.ConsoleWriteHeader("=============== Training the model ===============");
            Console.WriteLine($"Running AutoML regression experiment for {experimentTime} seconds...");
            ExperimentResult<RegressionMetrics> experimentResult = mlContext.Auto()
                .CreateRegressionExperiment(experimentTime)
                .Execute(trainDataView, targetColumnName, progressHandler: progressHandler);

            // Print top models found by AutoML
            Console.WriteLine();
            PrintTopModels(experimentResult);

            return experimentResult;
        }

        private static void SaveModel(MLContext mlContext, ITransformer model, string trainerName, double rSquared)
        {
            rSquared = Math.Round(rSquared, 4);
            var rSquared_string = rSquared.ToString().Replace(",", string.Empty);

            ModelPath = ModelPath + "ML_" + trainerName + "_" + rSquared_string + ".zip";
            ConsoleHelper.ConsoleWriteHeader("=============== Saving the model ===============");
            mlContext.Model.Save(model, trainDataView.Schema, ModelPath);
            Console.WriteLine($"The model is saved to {ModelPath}");
        }

        private static void PrintTopModels(ExperimentResult<RegressionMetrics> experimentResult)
        {
            // Get top few runs ranked by R-Squared.
            // R-Squared is a metric to maximize, so OrderByDescending() is correct.
            // For RMSE and other regression metrics, OrderByAscending() is correct.
            var topRuns = experimentResult.RunDetails
                .Where(r => r.ValidationMetrics != null && !double.IsNaN(r.ValidationMetrics.RSquared))
                .OrderByDescending(r => r.ValidationMetrics.RSquared).Take(3);

            Console.WriteLine("Top models ranked by R-Squared --");
            ConsoleHelper.PrintRegressionMetricsHeader();
            for (var i = 0; i < topRuns.Count(); i++)
            {
                var run = topRuns.ElementAt(i);
                ConsoleHelper.PrintIterationMetrics(i + 1, run.TrainerName, run.ValidationMetrics, run.RuntimeInSeconds);
            }
        }
    }
}
