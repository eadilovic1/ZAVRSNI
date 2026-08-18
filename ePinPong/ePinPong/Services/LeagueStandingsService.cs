using ePinPong.Data;
using ePinPong.Helpers;
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
        private readonly IStandingsCalculationService _standingsCalculationService;

        public LeagueStandingsService(ApplicationDbContext context, IStandingsCalculationService standingsCalculationService)
        {
            _context = context;
            _standingsCalculationService = standingsCalculationService;
        }

        public async Task<Dictionary<string, int>> GetPlayerPointsAsync(Turnir turnir)
        {
            var finishedTournamentsQuery = turnir.LigaID != null
                ? _context.Turniri.Where(t => t.LigaID == turnir.LigaID && t.Status == StatusTurnira.Zavrsen && t.ID != turnir.ID)
                : _context.Turniri.Where(t => t.Status == StatusTurnira.Zavrsen && t.ID != turnir.ID);

            var finishedTournaments = await finishedTournamentsQuery
                .Include(t => t.Registracije).ThenInclude(r => r.Korisnik)
                .Include(t => t.Mecevi).ThenInclude(m => m.Igrac1)
                .Include(t => t.Mecevi).ThenInclude(m => m.Igrac2)
                .Include(t => t.Mecevi).ThenInclude(m => m.MecKodovi)
                .ToListAsync();

            var playerPoints = LigaRankingHelper.IzracunajBodovePoKorisniku(finishedTournaments, _standingsCalculationService);

            if (turnir.Registracije != null)
            {
                foreach (var reg in turnir.Registracije)
                {
                    if (!playerPoints.ContainsKey(reg.KorisnikID))
                    {
                        playerPoints[reg.KorisnikID] = 0;
                    }
                }
            }

            return playerPoints;
        }

        private List<LigaStandingsViewModel> IzracunajTabeluLige(List<Turnir> zavrseniTurniri)
        {
            var tabeleMap = new Dictionary<string, LigaStandingsViewModel>();

            // Populate tabeleMap dynamically using users from registrations of finished tournaments in this league
            foreach (var turnir in zavrseniTurniri)
            {
                foreach (var reg in turnir.Registracije)
                {
                    if (reg.Korisnik != null && reg.KorisnikID != BracketService.SLOBODAN && !tabeleMap.ContainsKey(reg.KorisnikID))
                    {
                        tabeleMap[reg.KorisnikID] = new LigaStandingsViewModel
                        {
                            Korisnik = reg.Korisnik,
                            UkupnoBodova = 0,
                            BrojOdigranihTurnira = 0,
                            BodoviPoKolima = new List<int>()
                        };
                    }
                }
            }

            foreach (var turnir in zavrseniTurniri)
            {
                var plasmani = _standingsCalculationService.IzracunajPlasman(turnir);

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

        public async Task<List<LigaStandingsViewModel>> GetLeagueTableAsync(Liga liga)
        {
            var zavrseniTurniri = await _context.Turniri
                .Where(t => t.LigaID == liga.ID && t.Status == StatusTurnira.Zavrsen)
                .Include(t => t.Registracije).ThenInclude(r => r.Korisnik)
                .Include(t => t.Mecevi).ThenInclude(m => m.Igrac1)
                .Include(t => t.Mecevi).ThenInclude(m => m.Igrac2)
                .Include(t => t.Mecevi).ThenInclude(m => m.MecKodovi)
                .OrderBy(t => t.Kolo)
                .ToListAsync();

            return IzracunajTabeluLige(zavrseniTurniri);
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

        public async Task<List<KorisnikLigaStandingsViewModel>> GetPlayersStandingsAsync(List<Liga> lige, string korisnikId)
        {
            if (lige == null || !lige.Any()) return new List<KorisnikLigaStandingsViewModel>();

            var ligaIds = lige.Select(l => l.ID).ToList();

            // Load all completed tournaments for these leagues in a single batch query!
            var allFinishedTournaments = await _context.Turniri
                .Where(t => t.LigaID.HasValue && ligaIds.Contains(t.LigaID.Value) && t.Status == StatusTurnira.Zavrsen)
                .Include(t => t.Registracije).ThenInclude(r => r.Korisnik)
                .Include(t => t.Mecevi).ThenInclude(m => m.Igrac1)
                .Include(t => t.Mecevi).ThenInclude(m => m.Igrac2)
                .Include(t => t.Mecevi).ThenInclude(m => m.MecKodovi)
                .OrderBy(t => t.Kolo)
                .ToListAsync();

            var tournamentsByLeague = allFinishedTournaments
                .GroupBy(t => t.LigaID!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<KorisnikLigaStandingsViewModel>();

            foreach (var liga in lige)
            {
                tournamentsByLeague.TryGetValue(liga.ID, out var leagueTournaments);
                leagueTournaments ??= new List<Turnir>();

                var standings = IzracunajTabeluLige(leagueTournaments);
                var index = standings.FindIndex(s => s.Korisnik.Id == korisnikId);

                if (index >= 0)
                {
                    var mojStanding = standings[index];
                    result.Add(new KorisnikLigaStandingsViewModel
                    {
                        Liga = liga,
                        Pozicija = index + 1,
                        UkupnoBodova = mojStanding.UkupnoBodova,
                        BrojOdigranihTurnira = mojStanding.BrojOdigranihTurnira,
                        UkupnoUcesnika = standings.Count,
                        NijeZapoceo = false
                    });
                }
                else
                {
                    result.Add(new KorisnikLigaStandingsViewModel
                    {
                        Liga = liga,
                        Pozicija = 0,
                        UkupnoBodova = 0,
                        BrojOdigranihTurnira = 0,
                        UkupnoUcesnika = standings.Count,
                        NijeZapoceo = true
                    });
                }
            }

            return result;
        }
    }
}