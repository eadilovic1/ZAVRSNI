using System.Collections.Generic;

namespace ePinPong.Models.ViewModels
{
    public class LigaStandingsViewModel
    {
        public ApplicationUser Korisnik { get; set; } = null!;
        public int BrojOdigranihTurnira { get; set; }
        public int UkupnoBodova { get; set; }
        public List<int> BodoviPoKolima { get; set; } = new List<int>();
    }
}