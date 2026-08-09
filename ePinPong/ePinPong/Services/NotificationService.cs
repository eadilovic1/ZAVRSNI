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

        public async Task ObavijestiKorisnikaAsync(string userId, string naslov, string poruka, bool posaljiEmail = true)
        {
            if (string.IsNullOrEmpty(userId)) return;

            // 1. In-app notifikacija
            var notifikacija = new Notifikacija
            {
                KorisnikId = userId,
                Sadrzaj = poruka,
                DatumKreiranja = DateTime.Now,
                Procitana = false
            };
            _context.Notifikacije.Add(notifikacija);

            // 2. Email notifikacija
            if (posaljiEmail)
            {
                var korisnik = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (korisnik != null && !string.IsNullOrEmpty(korisnik.Email))
                {
                    await _mailService.SendEmailAsync(korisnik.Email, naslov, poruka);
                }
            }
        }
    }
}
