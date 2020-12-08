using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Master40.DB.GeneratorModel
{
    public class EdgeWeightRoundMode
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Name { get; set; }
        public virtual ICollection<BillOfMaterialInput> BomInputs { get; set; }
    }
}