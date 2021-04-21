using Microsoft.ML.Data;

namespace AiProvider.DataStuctures
{
    public class SimulationKpis
    {
        public SimulationKpis(
            float time, float assembly = 0, float material = 0, 
            float openOrders = 0, float newOrders = 0, 
            float totalWork = 0, float totalSetup = 0,
            float sumDuration = 0, float sumOperations = 0, float productionOrders = 0,
            float cycleTime = 0, 
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
            CycleTime = cycleTime;
            OrderId = orderId;
            CreationTime = creationTime;
            FinishingTime = finishingTime;
        }
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
        public float CycleTime { get; set; }
        public float OrderId { get; set; }
        public float CreationTime { get; set; }
        public float FinishingTime { get; set; }
    }
}
