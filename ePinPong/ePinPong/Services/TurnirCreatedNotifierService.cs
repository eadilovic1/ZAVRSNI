using ePinPong.Data;
using ePinPong.Models;
using Microsoft.EntityFrameworkCore;

namespace ePinPong.Services
{
    public class TurnirCreatedNotifierService : ITurnirCreatedNotifierService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public TurnirCreatedNotifierService(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task ObavijestiPratioceAsync(Turnir turnir, string organizatorName)
        {
            var pratioci = await _context.Pracenja
                .Where(p => p.PraceniID == turnir.OrganizatorId)
                .Include(p => p.Pratilac)
                .Select(p => p.Pratilac)
                .Where(u => u != null)
                .ToListAsync();

            foreach (var korisnik in pratioci)
            {
                string? emailBody = !string.IsNullOrEmpty(korisnik!.Email)
                    ? $"Zdravo {korisnik.Ime},<br><br>Organizator <b>{organizatorName}</b> je objavio novi turnir: <b>{turnir.Naziv}</b>.<br>Datum pocetka: {turnir.DatumPocetka.ToShortDateString()}<br><br>Prijavite se odmah na ePinPong!"
                    : null;

                await _notificationService.ObavijestiKorisnikaAsync(
                    korisnik,
                    "Novi turnir na ePinPong!",
                    $"Organizator <strong>{organizatorName}</strong> je objavio novi turnir: <strong><a href='/Turnir/Details/{turnir.ID}'>{turnir.Naziv}</a></strong>!",
                    emailBody,
                    posaljiEmail: true
                );
            }

            await _context.SaveChangesAsync();
        }
    }
}
