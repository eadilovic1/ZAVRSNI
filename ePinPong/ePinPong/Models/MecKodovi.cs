using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ePinPong.Models
{
    public class MecKodovi
    {
        [Key]
        [ForeignKey(nameof(Mec))]
        public int MecID { get; set; }
        public virtual Mec? Mec { get; set; }

        public string? WinnerNextMatchCode { get; set; }
        public string? LoserNextMatchCode { get; set; }
        public int? WinnerNextMatchSlot { get; set; }
        public int? LoserNextMatchSlot { get; set; }
    }
}
