using ePinPong.Models;
using ePinPong.Models.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ePinPong.Services
{
    public interface ILeagueStandingsService
    {
        // Zamjenjuje TurnirController.GetLeaguePointsForTurnirAsync i MecController.GetLeaguePointsForTurnirAsync
        Task<Dictionary<string, int>> GetPlayerPointsAsync(Turnir turnir);

        // Zamjenjuje LigaController.ObračunajTabeluLige
        Task<List<LigaStandingsViewModel>> GetLeagueTableAsync(Liga liga);

        // Zamjenjuje inline logiku u KorisnikController.Index
        Task<KorisnikLigaStandingsViewModel> GetPlayerStandingAsync(Liga liga, string korisnikId);

        // Batch dohvat plasmana za igrača kroz više liga
        Task<List<KorisnikLigaStandingsViewModel>> GetPlayersStandingsAsync(List<Liga> lige, string korisnikId);
    }
}