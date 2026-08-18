using System;
using System.Collections.Generic;

namespace ePinPong.Models
{
    public class Turnir
    {
        public int ID { get; set; }
        public string Naziv { get; set; } = string.Empty;
        public StatusTurnira Status { get; set; } = StatusTurnira.Planiran;
        public DateTime DatumPocetka { get; set; }
        public DateTime DatumKraja { get; set; }
        public int MaxIgraca { get; set; }
        public string Lokacija { get; set; } = string.Empty;
        public string Opis { get; set; } = string.Empty;
        public string? SlikaUrl { get; set; }

        public TipTakmicenja TipTakmicenja { get; set; } = TipTakmicenja.SinglIDubl;
        public SistemTurnira SistemTurnira { get; set; } = SistemTurnira.DoubleEliminationUtjesni;

        // Strani kljuc i organizator
        public string OrganizatorId { get; set; } = string.Empty;
        public virtual ApplicationUser? Organizator { get; set; }

        // Povezivanje sa Ligom
        public int? LigaID { get; set; }
        public virtual Liga? Liga { get; set; }
        public int? Kolo { get; set; }



        // Kolekcije
        public virtual ICollection<Registracija> Registracije { get; set; } = new List<Registracija>();
        public virtual ICollection<Mec> Mecevi { get; set; } = new List<Mec>();
        public virtual ICollection<TurnirPar> TurnirParovi { get; set; } = new List<TurnirPar>();
    }
}
