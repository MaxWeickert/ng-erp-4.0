using Microsoft.ML.Data;

namespace Master40.MachineLearning.DataStuctures
{
    public class SimulationKpisReshaped
    {
        public float Assembly { get; set; }
        public float Material { get; set; }
        public float OpenOrders { get; set; }
        public float NewOrders { get; set; }
        public float TotalWork { get; set; }
        public float TotalSetup { get; set; }
        public float SumDuration { get; set; }
        public float SumOperation { get; set; }
        public float ProductionOrders { get; set; }
        public float CycleTime { get; set; }
    }
}
