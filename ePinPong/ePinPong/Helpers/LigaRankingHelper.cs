using ePinPong.Data;
using ePinPong.Models;
using ePinPong.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ePinPong.Helpers
{
    public static class LigaRankingHelper
    {
        public static async Task<List<string>> GetLeagueStandingsParticipantIdsAsync(ApplicationDbContext context, IBracketService bracketService, Liga liga)
        {
            if (context == null) throw new System.ArgumentNullException(nameof(context));
            if (bracketService == null) throw new System.ArgumentNullException(nameof(bracketService));
            if (liga == null) throw new System.ArgumentNullException(nameof(liga));

            var standings = await GetPointsPerPlayerAsync(context, bracketService, liga);

            return standings.Keys
                .OrderByDescending(id => standings[id])
                .ToList();
        }

        public static async Task<HashSet<string>> GetLeagueMastersParticipantIdsAsync(ApplicationDbContext context, IBracketService bracketService, Liga liga)
        {
            if (context == null) throw new System.ArgumentNullException(nameof(context));
            if (bracketService == null) throw new System.ArgumentNullException(nameof(bracketService));
            if (liga == null) throw new System.ArgumentNullException(nameof(liga));

            var mastersKolo = LigaTurnirHelper.GetMastersKolo(liga);

            var registriraniKorisnici = await context.Registracije
                .Where(r => r.Turnir.LigaID == liga.ID
                            && (r.Turnir.Kolo == null || r.Turnir.Kolo.Value != mastersKolo))
                .Select(r => r.KorisnikID)
                .Where(id => !string.IsNullOrEmpty(id) && id != BracketService.SLOBODAN)
                .Distinct()
                .ToListAsync();

            var standingsParticipantIds = await GetLeagueStandingsParticipantIdsAsync(context, bracketService, liga);

            return registriraniKorisnici
                .Concat(standingsParticipantIds)
                .Where(id => !string.IsNullOrEmpty(id) && id != BracketService.SLOBODAN)
                .ToHashSet();
        }

        public static async Task<List<string>> GetMastersOrderedParticipantIdsAsync(ApplicationDbContext context, IBracketService bracketService, Turnir mastersTurnir)
        {
            if (context == null) throw new System.ArgumentNullException(nameof(context));
            if (bracketService == null) throw new System.ArgumentNullException(nameof(bracketService));
            if (mastersTurnir == null) throw new System.ArgumentNullException(nameof(mastersTurnir));

            if (!mastersTurnir.LigaID.HasValue)
            {
                return mastersTurnir.Registracije
                    .Where(r => !string.IsNullOrEmpty(r.KorisnikID) && r.KorisnikID != BracketService.SLOBODAN)
                    .Select(r => r.KorisnikID!)                    
                    .ToList();
            }

            var liga = await context.Lige.FirstOrDefaultAsync(l => l.ID == mastersTurnir.LigaID.Value);
            if (liga == null)
            {
                return mastersTurnir.Registracije
                    .Where(r => !string.IsNullOrEmpty(r.KorisnikID) && r.KorisnikID != BracketService.SLOBODAN)
                    .Select(r => r.KorisnikID!)                    
                    .ToList();
            }

            var points = await GetPointsPerPlayerAsync(context, bracketService, liga);

            var registrations = mastersTurnir.Registracije
                .Where(r => !string.IsNullOrEmpty(r.KorisnikID) && r.KorisnikID != BracketService.SLOBODAN)
                .Select(r => new
                {
                    r.KorisnikID,
                    r.DatumRegistracije
                })
                .ToList();

            return registrations
                .OrderByDescending(r => points.TryGetValue(r.KorisnikID, out var pts) ? pts : 0)
                .ThenBy(r => points.ContainsKey(r.KorisnikID) ? 0 : 1)
                .ThenBy(r => r.DatumRegistracije)
                .Select(r => r.KorisnikID!)                
                .ToList();
        }

        public static Dictionary<string, int> IzracunajBodovePoKorisniku(
            IEnumerable<Turnir> finishedTournaments,
            IStandingsCalculationService calculationService)
        {
            return IzracunajBodovePoKorisniku(finishedTournaments, calculationService.IzracunajPlasman);
        }

        public static Dictionary<string, int> IzracunajBodovePoKorisniku(
            IEnumerable<Turnir> finishedTournaments,
            IBracketService bracketService)
        {
            return IzracunajBodovePoKorisniku(finishedTournaments, bracketService.IzracunajPlasman);
        }

        public static Dictionary<string, int> IzracunajBodovePoKorisniku(
            IEnumerable<Turnir> finishedTournaments,
            System.Func<Turnir, List<Models.ViewModels.TurnirPlasmanViewModel>> plasmanCalculator)
        {
            var points = new Dictionary<string, int>();
            foreach (var ft in finishedTournaments)
            {
                var plasmani = plasmanCalculator(ft);
                foreach (var plasman in plasmani)
                {
                    if (string.IsNullOrEmpty(plasman.KorisnikId) || plasman.KorisnikId == BracketService.SLOBODAN)
                    {
                        continue;
                    }

                    if (points.ContainsKey(plasman.KorisnikId))
                    {
                        points[plasman.KorisnikId] += plasman.Bodovi;
                    }
                    else
                    {
                        points[plasman.KorisnikId] = plasman.Bodovi;
                    }
                }
            }
            return points;
        }

        private static async Task<Dictionary<string, int>> GetPointsPerPlayerAsync(ApplicationDbContext context, IBracketService bracketService, Liga liga)
        {
            var mastersKolo = LigaTurnirHelper.GetMastersKolo(liga);
            var finishedRegularTurniri = await context.Turniri
                .Include(t => t.Registracije)
                    .ThenInclude(r => r.Korisnik)
                .Include(t => t.Mecevi)
                    .ThenInclude(m => m.Igrac1)
                .Include(t => t.Mecevi)
                    .ThenInclude(m => m.Igrac2)
                .Where(t => t.LigaID == liga.ID && t.Kolo.HasValue && t.Kolo.Value != mastersKolo && t.Status == StatusTurnira.Zavrsen)
                .ToListAsync();

            return IzracunajBodovePoKorisniku(finishedRegularTurniri, bracketService);
        }
    }
}
