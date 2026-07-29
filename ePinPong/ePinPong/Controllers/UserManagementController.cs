using ePinPong.Data;
using ePinPong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ePinPong.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class UserManagementController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public UserManagementController(
            UserManager<ApplicationUser> userManager, 
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // GET: /UserManagement
        public async Task<IActionResult> Index()
        {
            var korisnici = await _userManager.Users.Where(u => u.Id != "SLOBODAN").ToListAsync();
            var korisniciUloge = new List<UserRoleViewModel>();

            foreach (var user in korisnici)
            {
                var uloge = await _userManager.GetRolesAsync(user);
                korisniciUloge.Add(new UserRoleViewModel
                {
                    UserId = user.Id,
                    Ime = user.Ime,
                    Prezime = user.Prezime,
                    Email = user.Email ?? string.Empty,
                    Grad = user.Grad,
                    Uloge = uloge.ToList(),
                    IsGost = user.IsGost
                });
            }

            return View(korisniciUloge);
        }

        // GET: /UserManagement/EditRoles/5
        public async Task<IActionResult> EditRoles(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var userUloge = await _userManager.GetRolesAsync(user);
            var sveUloge = await _roleManager.Roles.Select(r => r.Name).ToListAsync();

            ViewBag.UserId = id;
            ViewBag.UserName = $"{user.Ime} {user.Prezime}";
            ViewBag.UserRoles = userUloge;
            ViewBag.AllRoles = sveUloge;

            return View();
        }

        // POST: /UserManagement/EditRoles/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRoles(string id, List<string> odabraneUloge)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var uloge = await _userManager.GetRolesAsync(user);
            
            // Ukloni stare uloge
            var result = await _userManager.RemoveFromRolesAsync(user, uloge);
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Greška prilikom uklanjanja starih uloga.");
                return View();
            }

            // Dodaj nove uloge
            if (odabraneUloge != null && odabraneUloge.Any())
            {
                result = await _userManager.AddToRolesAsync(user, odabraneUloge);
                if (!result.Succeeded)
                {
                    ModelState.AddModelError("", "Greška prilikom dodavanja novih uloga.");
                    return View();
                }
            }

            TempData["Success"] = "Uloge su uspješno ažurirane!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /UserManagement/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Kaskadno čišćenje (analogno ePazaru!)
            // 1. Obriši registracije
            var registracije = _context.Registracije.Where(r => r.KorisnikID == id);
            _context.Registracije.RemoveRange(registracije);

            // 2. Obriši mečeve u kojima je korisnik igrao
            var mecevi = _context.Mecevi.Where(m => m.Igrac1ID == id || m.Igrac2ID == id);
            _context.Mecevi.RemoveRange(mecevi);

            // 3. Obriši notifikacije korisnika
            var notifikacije = _context.Notifikacije.Where(n => n.KorisnikId == id);
            _context.Notifikacije.RemoveRange(notifikacije);

            // 4. Obriši praćenja
            var pracenja = _context.Pracenja.Where(p => p.PratilacID == id || p.PraceniID == id);
            _context.Pracenja.RemoveRange(pracenja);

            // 5. Obriši turnire koje je organizovao
            var turniri = _context.Turniri.Where(t => t.OrganizatorId == id);
            _context.Turniri.RemoveRange(turniri);

            await _context.SaveChangesAsync();

            // Obriši samog korisnika iz Identity
            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Korisnički račun je uspješno obrisan.";
            }
            else
            {
                TempData["Error"] = "Greška prilikom brisanja korisnika.";
            }

            return RedirectToAction(nameof(Index));
        }
    }

    public class UserRoleViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Ime { get; set; } = string.Empty;
        public string Prezime { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Grad { get; set; } = string.Empty;
        public List<string> Uloge { get; set; } = new List<string>();
        public bool IsGost { get; set; }
    }
}
