using System.Collections.Generic;

namespace ePinPong.Models.ViewModels
{
    public class KorisnikTurniriViewModel
    {
        public ApplicationUser Korisnik { get; set; } = null!;
        public IEnumerable<Turnir> Turniri { get; set; } = new List<Turnir>();
        public IEnumerable<Registracija> Registracije { get; set; } = new List<Registracija>();
        public IEnumerable<Mec> Mecevi { get; set; } = new List<Mec>();
        public int BrojPratilaca { get; set; }
        public int BrojPracenih { get; set; }
        public bool DaLiPratim { get; set; }

        // Novi atributi
        public Dictionary<int, string> TurnirRankings { get; set; } = new Dictionary<int, string>();
        public IEnumerable<KorisnikLigaStandingsViewModel> LigeStandings { get; set; } = new List<KorisnikLigaStandingsViewModel>();
    }

    public class KorisnikLigaStandingsViewModel
    {
        public Liga Liga { get; set; } = null!;
        public int Pozicija { get; set; }
        public int UkupnoBodova { get; set; }
        public int BrojOdigranihTurnira { get; set; }
        public int UkupnoUcesnika { get; set; }
        public bool NijeZapoceo { get; set; }
    }
}
