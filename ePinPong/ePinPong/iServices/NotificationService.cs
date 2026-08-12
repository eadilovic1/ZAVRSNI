using ePinPong.Data;
using ePinPong.Interfaces;
using ePinPong.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace ePinPong.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMailService _mailService;

        public NotificationService(ApplicationDbContext context, IMailService mailService)
        {
            _context = context;
            _mailService = mailService;
        }

        public async Task ObavijestiKorisnikaAsync(string userId, string naslov, string poruka, string? emailPoruka = null, bool posaljiEmail = true)
        {
            if (string.IsNullOrEmpty(userId)) return;

            if (posaljiEmail)
            {
                var korisnik = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (korisnik != null)
                {
                    await ObavijestiKorisnikaAsync(korisnik, naslov, poruka, emailPoruka, posaljiEmail);
                    return;
                }
            }

            var notifikacija = new Notifikacija
            {
                KorisnikId = userId,
                Sadrzaj = poruka,
                DatumKreiranja = DateTime.Now,
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
                DatumKreiranja = DateTime.Now,
                Procitana = false
            };
            _context.Notifikacije.Add(notifikacija);

            // 2. Email notifikacija
            if (posaljiEmail && !string.IsNullOrEmpty(korisnik.Email))
            {
                var mailText = emailPoruka ?? poruka;
                await _mailService.SendEmailAsync(korisnik.Email, naslov, mailText);
            }
        }
    }
}
