using System.ComponentModel.DataAnnotations.Schema;

namespace Master40.DB.GeneratorModel
{
    public class TransitionMatrixSettingConfiguration
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int SettingOptionId { get; set; }
        public int TransitionMatrixId { get; set; }
        public double SettingValue { get; set; }
        public TransitionMatrixSettingOption SettingOption { get; set; }
        public TransitionMatrixInput TransitionMatrix { get; set; }
    }
}