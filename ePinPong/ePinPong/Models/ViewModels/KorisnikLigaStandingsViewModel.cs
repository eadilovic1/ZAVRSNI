namespace ePinPong.Models.ViewModels
{
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
