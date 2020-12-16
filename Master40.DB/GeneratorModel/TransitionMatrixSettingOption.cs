using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Master40.DB.GeneratorModel
{
    public class TransitionMatrixSettingOption
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public virtual ICollection<TransitionMatrixSettingConfiguration> SettingConfigurations { get; set; }
    }
}