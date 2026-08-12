using ePinPong.Data;
using ePinPong.Models;
using ePinPong.Models.ViewModels;
using System.Text.Json;

namespace ePinPong.Services
{
    public class SeedingPotsService : ISeedingPotsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SeedingPotsService> _logger;

        public SeedingPotsService(ApplicationDbContext context, ILogger<SeedingPotsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<bool> PrimijeniSesireAsync(Turnir turnir, string playerPotsJson)
        {
            try
            {
                var playerPots = JsonSerializer.Deserialize<List<PlayerPotViewModel>>(
                    playerPotsJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (playerPots != null)
                {
                    foreach (var pp in playerPots)
                    {
                        var reg = turnir.Registracije.FirstOrDefault(r => r.KorisnikID == pp.KorisnikId);
                        if (reg != null)
                        {
                            reg.Sesir = pp.Sesir;
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Neuspješno parsiranje/snimanje šešira (playerPotsJson) za turnir {TurnirId}.", turnir.ID);
                return false;
            }
        }
    }
}
