using System;
using System.Collections.Generic;

namespace ePinPong.Models
{
    public class Liga
    {
        public int ID { get; set; }
        public string Naziv { get; set; } = string.Empty;
        public string Opis { get; set; } = string.Empty;
        public string Sezona { get; set; } = string.Empty;
        public DateTime DatumPocetka { get; set; } = DateTime.Today;
        public int BrojRegularnihTurnira { get; set; } = 1;

        // Relacija sa turnirima
        public virtual ICollection<Turnir> Turniri { get; set; } = new List<Turnir>();
    }
}
