using Microsoft.ML.Data;
using System;

namespace Master40.MachineLearning.DataStuctures
{
    public class CycleTimePrediction
    {
        [ColumnName("Score")] public float CycleTime;

        //public static explicit operator long(CycleTimePrediction v)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
