using Microsoft.ML.Data;

namespace Master40.MachineLearning.DataStuctures
{
    public class SimulationKpisReshaped
    {
        [LoadColumn(0)]
        public float Assembly { get; set; }

        [LoadColumn(1)]
        public float Material { get; set; }

        [LoadColumn(2)]
        public float OpenOrders { get; set; }

        [LoadColumn(3)]
        public float NewOrders { get; set; }

        [LoadColumn(4)]
        public float TotalWork { get; set; }

        [LoadColumn(5)]
        public float TotalSetup { get; set; }

        [LoadColumn(6)]
        public float SumDuration { get; set; }

        [LoadColumn(7)]
        public float SumOperations { get; set; }

        [LoadColumn(8)]
        public float ProductionOrders { get; set; }

        [LoadColumn(9)]
        public float CycleTime { get; set; }
    }
}
