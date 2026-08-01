using ePinPong.Data;
using ePinPong.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ePinPong.Services
{
    public static class LigaRankingHelper
    {
        public static async Task<List<string>> GetLeagueStandingsParticipantIdsAsync(ApplicationDbContext context, IBracketService bracketService, Liga liga)
        {
            if (context == null) throw new System.ArgumentNullException(nameof(context));
            if (bracketService == null) throw new System.ArgumentNullException(nameof(bracketService));
            if (liga == null) throw new System.ArgumentNullException(nameof(liga));

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

            if (!finishedRegularTurniri.Any())
            {
                return new List<string>();
            }

            var standings = new Dictionary<string, int>();
            foreach (var finishedTurnir in finishedRegularTurniri)
            {
                var plasmani = bracketService.IzracunajPlasman(finishedTurnir);
                foreach (var plasman in plasmani)
                {
                    if (string.IsNullOrEmpty(plasman.KorisnikId))
                    {
                        continue;
                    }

                    if (standings.ContainsKey(plasman.KorisnikId))
                    {
                        standings[plasman.KorisnikId] += plasman.Bodovi;
                    }
                    else
                    {
                        standings[plasman.KorisnikId] = plasman.Bodovi;
                    }
                }
            }

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
    }
}
