using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Master40.DB.Data.Helper
{
    public class ThroughputParameter
    {
        public int ArticleId { get; set; }
        public int OperationCount { get; set; }
        public int Duration { get; set; }
        public int ProductionOrderCount { get; set; }
    }
}
