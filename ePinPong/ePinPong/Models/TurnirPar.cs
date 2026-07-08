using System;

namespace ePinPong.Models
{
    public class TurnirPar
    {
        public int ID { get; set; }

        public int TurnirID { get; set; }
        public virtual Turnir? Turnir { get; set; }

        public string Igrac1ID { get; set; } = string.Empty;
        public virtual ApplicationUser? Igrac1 { get; set; }

        public string Igrac2ID { get; set; } = string.Empty;
        public virtual ApplicationUser? Igrac2 { get; set; }

        public DateTime DatumPrijave { get; set; } = DateTime.Now;
    }
}
