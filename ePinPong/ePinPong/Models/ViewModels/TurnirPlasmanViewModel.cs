using ePinPong.Models;

namespace ePinPong.Models.ViewModels
{
    public class TurnirPlasmanViewModel
    {
        public string KorisnikId { get; set; } = string.Empty;
        public ApplicationUser? Korisnik { get; set; }
        public string ImePrezime { get; set; } = string.Empty;
        public string Grad { get; set; } = string.Empty;
        public bool IsGost { get; set; }
        public int Pozicija { get; set; }
        public int Bodovi { get; set; }
        public string DetaljPozicije { get; set; } = string.Empty;
    }
}
