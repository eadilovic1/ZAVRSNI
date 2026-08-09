using ePinPong.Data;
using ePinPong.Models;
using ePinPong.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace ePinPong.Controllers
{
    [Authorize]
    public class PracenjeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public PracenjeController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        // POST: /Pracenje/Follow
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Follow(string praceniId)
        {
            var pratilacId = _userManager.GetUserId(User);
            if (pratilacId == null) return Unauthorized();

            if (pratilacId == praceniId)
            {
                TempData["Error"] = "Ne možete zapratiti sami sebe.";
                return RedirectToAction("Index", "Korisnik", new { id = praceniId });
            }

            // Provjera da li vec prati
            var vecPrati = await _context.Pracenja
                .AnyAsync(p => p.PratilacID == pratilacId && p.PraceniID == praceniId);

            if (vecPrati)
            {
                TempData["Error"] = "Već pratite ovog igrača.";
                return RedirectToAction("Index", "Korisnik", new { id = praceniId });
            }

            var pracenje = new Pracenje
            {
                PratilacID = pratilacId,
                PraceniID = praceniId
            };

            _context.Pracenja.Add(pracenje);

            // In-app notifikacija za pracenog korisnika
            var pratilac = await _userManager.FindByIdAsync(pratilacId);
            await _notificationService.ObavijestiKorisnikaAsync(
                praceniId,
                "Novi pratilac na ePinPong!",
                $"Igrač <strong><a href='/Korisnik/Index/{pratilacId}'>{pratilac?.Ime} {pratilac?.Prezime}</a></strong> vas je zapratio!",
                posaljiEmail: false
            );

            await _context.SaveChangesAsync();

            return RedirectToAction("Index", "Korisnik", new { id = praceniId });
        }

        // POST: /Pracenje/Unfollow
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unfollow(string praceniId)
        {
            var pratilacId = _userManager.GetUserId(User);
            if (pratilacId == null) return Unauthorized();

            var pracenje = await _context.Pracenja
                .FirstOrDefaultAsync(p => p.PratilacID == pratilacId && p.PraceniID == praceniId);

            if (pracenje != null)
            {
                _context.Pracenja.Remove(pracenje);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index", "Korisnik", new { id = praceniId });
        }
    }
}
