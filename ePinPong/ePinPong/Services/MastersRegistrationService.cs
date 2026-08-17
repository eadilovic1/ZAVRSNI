using ePinPong.Data;
using ePinPong.Helpers;
using ePinPong.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ePinPong.Services
{
    public class MastersRegistrationService : IMastersRegistrationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IBracketService _bracketService;

        public MastersRegistrationService(ApplicationDbContext context, IBracketService bracketService)
        {
            _context = context;
            _bracketService = bracketService;
        }

        public async Task AutoRegistrirajIgraceLigeAsync(Liga liga, int turnirId)
        {
            var korisniciNaRankingu = await LigaRankingHelper.GetLeagueMastersParticipantIdsAsync(_context, _bracketService, liga);
            if (!korisniciNaRankingu.Any())
            {
                return;
            }

            var postojeciIds = await _context.Registracije
                .Where(r => r.TurnirID == turnirId)
                .Select(r => r.KorisnikID)
                .ToHashSetAsync();

            var noveRegistracije = korisniciNaRankingu
                .Where(korisnikId => !postojeciIds.Contains(korisnikId))
                .Select(korisnikId => new Registracija
                {
                    TurnirID = turnirId,
                    KorisnikID = korisnikId,
                    Odobren = true,
                    DatumRegistracije = DateTime.UtcNow,
                    Sesir = 1
                })
                .ToList();

            if (noveRegistracije.Any())
            {
                _context.Registracije.AddRange(noveRegistracije);
                await _context.SaveChangesAsync();
            }
        }
    }
}