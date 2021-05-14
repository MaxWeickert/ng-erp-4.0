using Microsoft.ML.Data;
using CsvHelper.Configuration.Attributes;

namespace AiProvider.DataStuctures
{
    public class SimulationKpis
    {
        public SimulationKpis(
            float time, float assembly = 0, float material = 0, 
            float openOrders = -1, float newOrders = -1, 
            float totalWork = 0, float totalSetup = 0,
            float sumDuration = 0, float sumOperations = 0, float productionOrders = 0,
            float cycleTime = 0, float predCycleTime = 0, 
            float orderId = 0, float creationTime = 0, float finishingTime = 0)
        {
            Time = time;
            Assembly = assembly;
            Material = material;
            OpenOrders = openOrders;
            NewOrders = newOrders;
            TotalWork = totalWork;
            TotalSetup = totalSetup;
            SumDuration = sumDuration;
            SumOperations = sumOperations;
            ProductionOrders = productionOrders;
            OrderId = orderId;
            CreationTime = creationTime;
            FinishingTime = finishingTime;
            CycleTime = cycleTime;
            PredCycleTime = predCycleTime;
        }
        [Ignore]
        public float Time { get; set; }
        public float Assembly { get; set; }
        public float Material { get; set; }
        public float OpenOrders { get; set; }
        public float NewOrders { get; set; }
        public float TotalWork { get; set; }
        public float TotalSetup { get; set; }
        public float SumDuration { get; set; }
        public float SumOperations { get; set; }
        public float ProductionOrders { get; set; }
        [Ignore]
        public float OrderId { get; set; }
        [Ignore]
        public float CreationTime { get; set; }
        [Ignore]
        public float FinishingTime { get; set; }
        [Ignore]
        public float PredCycleTime { get; set; }
        public float CycleTime { get; set; }
    }
}
