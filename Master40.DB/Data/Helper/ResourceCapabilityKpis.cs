using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Master40.DB.Data.Helper
{
    public class ResourceCapabilityKpis
    {
        public double time;
        public double value;
        public int type;
        public string name;

        public ResourceCapabilityKpis(double time, string name, double value, int type)
        {
            this.time = time;
            this.name = name;
            this.value = value;
            this.type = type;
        }
    }
}
