using ePinPong.Data;
using ePinPong.Models;
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

        public PracenjeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
            var notifikacija = new Notifikacija
            {
                KorisnikId = praceniId,
                Sadrzaj = $"Igrač <strong><a href='/Korisnik/Index/{pratilacId}'>{pratilac?.Ime} {pratilac?.Prezime}</a></strong> vas je zapratio!",
                DatumKreiranja = DateTime.Now,
                Procitana = false
            };
            _context.Notifikacije.Add(notifikacija);

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
