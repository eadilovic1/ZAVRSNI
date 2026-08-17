using ePinPong.Data;
using ePinPong.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace ePinPong.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMailQueueService _emailQueue;

        public NotificationService(ApplicationDbContext context, IMailQueueService emailQueue)
        {
            _context = context;
            _emailQueue = emailQueue;
        }

        public async Task ObavijestiKorisnikaAsync(string userId, string naslov, string poruka, string? emailPoruka = null, bool posaljiEmail = true)
        {
            if (string.IsNullOrEmpty(userId)) return;

            var korisnik = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (korisnik != null)
            {
                await ObavijestiKorisnikaAsync(korisnik, naslov, poruka, emailPoruka, posaljiEmail);
                return;
            }

            var notifikacija = new Notifikacija
            {
                KorisnikId = userId,
                Sadrzaj = poruka,
                DatumKreiranja = DateTime.UtcNow,
                Procitana = false
            };
            _context.Notifikacije.Add(notifikacija);
        }

        public async Task ObavijestiKorisnikaAsync(ApplicationUser korisnik, string naslov, string poruka, string? emailPoruka = null, bool posaljiEmail = true)
        {
            if (korisnik == null || string.IsNullOrEmpty(korisnik.Id)) return;

            // 1. In-app notifikacija
            var notifikacija = new Notifikacija
            {
                KorisnikId = korisnik.Id,
                Sadrzaj = poruka,
                DatumKreiranja = DateTime.UtcNow,
                Procitana = false
            };
            _context.Notifikacije.Add(notifikacija);

            // 2. Email notifikacija (ne šaljemo ako je gost ili nema email)
            if (posaljiEmail && !korisnik.IsGost && !string.IsNullOrEmpty(korisnik.Email))
            {
                var mailText = emailPoruka ?? poruka;
                _emailQueue.Enqueue(korisnik.Email, naslov, mailText);
            }
        }
    }
}
