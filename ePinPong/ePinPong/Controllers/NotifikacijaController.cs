using ePinPong.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using ePinPong.Models;

namespace ePinPong.Controllers
{
    [Authorize]
    public class NotifikacijaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotifikacijaController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Notifikacija
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var notifikacije = await _context.Notifikacije
                .Where(n => n.KorisnikId == userId)
                .OrderByDescending(n => n.DatumKreiranja)
                .ToListAsync();

            return View(notifikacije);
        }

        // POST: /Notifikacija/MarkAsRead/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = _userManager.GetUserId(User);
            var notifikacija = await _context.Notifikacije
                .FirstOrDefaultAsync(n => n.ID == id && n.KorisnikId == userId);

            if (notifikacija != null)
            {
                notifikacija.Procitana = true;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Notifikacija/MarkAllAsRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var neprocitane = await _context.Notifikacije
                .Where(n => n.KorisnikId == userId && !n.Procitana)
                .ToListAsync();

            foreach (var n in neprocitane)
            {
                n.Procitana = true;
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
