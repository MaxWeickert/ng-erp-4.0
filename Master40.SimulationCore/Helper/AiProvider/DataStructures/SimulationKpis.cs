using Microsoft.ML.Data;

namespace AiProvider.DataStuctures
{
    public class SimulationKpis
    {
        public SimulationKpis(
            float time, float assembly = 0, float consumable = 0, float material = 0, 
            float openOrders = 0, float newOrders = 0, 
            float totalWork = 0, float totalSetup = 0, float capabilityIdle = 0,
            float sumDuration = 0, float sumOperation = 0, float productionOrders = 0,
            float cycleTime = 0)
        {
            Time = time;
            Assembly = assembly;
            Consumable = consumable;
            Material = material;
            OpenOrders = openOrders;
            NewOrders = newOrders;
            TotalWork = totalWork;
            TotalSetup = totalSetup;
            CapabilityIdle = capabilityIdle;
            SumDuration = sumDuration;
            SumOperation = sumOperation;
            ProductionOrders = productionOrders;
            CycleTime = cycleTime;
        }
        public float Time { get; set; }
        public float Assembly { get; set; }
        public float Consumable { get; set; }
        public float Material { get; set; }
        public float OpenOrders { get; set; }
        public float NewOrders { get; set; }
        public float TotalWork { get; set; }
        public float TotalSetup { get; set; }
        public float CapabilityIdle { get; set; }
        public float SumDuration { get; set; }
        public float SumOperation { get; set; }
        public float ProductionOrders { get; set; }
        public float CycleTime { get; set; }
    }
}
