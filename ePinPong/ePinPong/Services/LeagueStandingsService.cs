using ePinPong.Data;
using ePinPong.Models;
using ePinPong.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ePinPong.Services
{
    public class LeagueStandingsService : ILeagueStandingsService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBracketService _bracketService;

        public LeagueStandingsService(ApplicationDbContext context, IBracketService bracketService)
        {
            _context = context;
            _bracketService = bracketService;
        }

        public async Task<Dictionary<string, int>> GetPlayerPointsAsync(Turnir turnir)
        {
            var playerPoints = new Dictionary<string, int>();
            foreach (var reg in turnir.Registracije)
            {
                playerPoints[reg.KorisnikID] = 0;
            }

            var finishedTournamentsQuery = turnir.LigaID != null
                ? _context.Turniri.Where(t => t.LigaID == turnir.LigaID && t.Status == StatusTurnira.Zavrsen && t.ID != turnir.ID)
                : _context.Turniri.Where(t => t.Status == StatusTurnira.Zavrsen && t.ID != turnir.ID);

            var finishedTournaments = await finishedTournamentsQuery
                .Include(t => t.Registracije).ThenInclude(r => r.Korisnik)
                .Include(t => t.Mecevi).ThenInclude(m => m.Igrac1)
                .Include(t => t.Mecevi).ThenInclude(m => m.Igrac2)
                .ToListAsync();

            foreach (var ft in finishedTournaments)
            {
                var plasmani = _bracketService.IzracunajPlasman(ft);
                foreach (var pl in plasmani)
                {
                    if (playerPoints.ContainsKey(pl.KorisnikId))
                    {
                        playerPoints[pl.KorisnikId] += pl.Bodovi;
                    }
                }
            }

            return playerPoints;
        }

        public async Task<List<LigaStandingsViewModel>> GetLeagueTableAsync(Liga liga)
        {
            var tabeleMap = new Dictionary<string, LigaStandingsViewModel>();
            var sviKorisnici = await _context.Users.Where(u => u.Id != "SLOBODAN").ToListAsync();

            foreach (var user in sviKorisnici)
            {
                tabeleMap[user.Id] = new LigaStandingsViewModel
                {
                    Korisnik = user,
                    UkupnoBodova = 0,
                    BrojOdigranihTurnira = 0,
                    BodoviPoKolima = new List<int>()
                };
            }

            // Jedan batch upit umjesto N+1 (ranije se svaki turnir dohvatao sinhrono unutar petlje)
            var zavrseniTurniri = await _context.Turniri
                .Where(t => t.LigaID == liga.ID && t.Status == StatusTurnira.Zavrsen)
                .Include(t => t.Registracije).ThenInclude(r => r.Korisnik)
                .Include(t => t.Mecevi).ThenInclude(m => m.Igrac1)
                .Include(t => t.Mecevi).ThenInclude(m => m.Igrac2)
                .OrderBy(t => t.Kolo)
                .ToListAsync();

            foreach (var turnir in zavrseniTurniri)
            {
                var plasmani = _bracketService.IzracunajPlasman(turnir);

                foreach (var item in tabeleMap)
                {
                    var userId = item.Key;
                    var model = item.Value;
                    var plasmanInfo = plasmani.FirstOrDefault(p => p.KorisnikId == userId);
                    if (plasmanInfo != null)
                    {
                        model.UkupnoBodova += plasmanInfo.Bodovi;
                        model.BrojOdigranihTurnira++;
                        model.BodoviPoKolima.Add(plasmanInfo.Bodovi);
                    }
                    else
                    {
                        model.BodoviPoKolima.Add(0);
                    }
                }
            }

            return tabeleMap.Values
                .Where(v => v.BrojOdigranihTurnira > 0)
                .OrderByDescending(v => v.UkupnoBodova)
                .ToList();
        }

        public async Task<KorisnikLigaStandingsViewModel> GetPlayerStandingAsync(Liga liga, string korisnikId)
        {
            var standings = await GetLeagueTableAsync(liga);
            var index = standings.FindIndex(s => s.Korisnik.Id == korisnikId);

            if (index >= 0)
            {
                var mojStanding = standings[index];
                return new KorisnikLigaStandingsViewModel
                {
                    Liga = liga,
                    Pozicija = index + 1,
                    UkupnoBodova = mojStanding.UkupnoBodova,
                    BrojOdigranihTurnira = mojStanding.BrojOdigranihTurnira,
                    UkupnoUcesnika = standings.Count,
                    NijeZapoceo = false
                };
            }

            return new KorisnikLigaStandingsViewModel
            {
                Liga = liga,
                Pozicija = 0,
                UkupnoBodova = 0,
                BrojOdigranihTurnira = 0,
                UkupnoUcesnika = standings.Count,
                NijeZapoceo = true
            };
        }
    }
}