using ePinPong.Data;
using ePinPong.Models;
using ePinPong.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ePinPong.Controllers
{
    public class LigaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBracketService _bracketService;

        public LigaController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IBracketService bracketService)
        {
            _context = context;
            _userManager = userManager;
            _bracketService = bracketService;
        }

        // GET: /Liga
        public async Task<IActionResult> Index()
        {
            var lige = await _context.Lige
                .Include(l => l.Turniri)
                .ToListAsync();
            return View(lige);
        }

        // GET: /Liga/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var liga = await _context.Lige
                .Include(l => l.Turniri)
                    .ThenInclude(t => t.Registracije)
                .Include(l => l.Turniri)
                    .ThenInclude(t => t.Mecevi)
                .FirstOrDefaultAsync(l => l.ID == id);

            if (liga == null)
            {
                return NotFound();
            }

            // Izračunaj tabelu Lige (Leaderboard)
            var standings = ObračunajTabeluLige(liga);
            ViewBag.Standings = standings;

            var userId = _userManager.GetUserId(User);
            ViewBag.IsAdminOrOrganizator = User.Identity?.IsAuthenticated == true && (User.IsInRole("Administrator") || User.IsInRole("Organizator"));

            return View(liga);
        }

        // GET: /Liga/Create
        [Authorize(Roles = "Administrator,Organizator")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Liga/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Organizator")]
        public async Task<IActionResult> Create(Liga liga, bool autoGenerisiTurnire)
        {
            if (ModelState.IsValid)
            {
                _context.Add(liga);
                await _context.SaveChangesAsync();

                if (autoGenerisiTurnire)
                {
                    var userId = _userManager.GetUserId(User);
                    DateTime tempDate = liga.DatumPocetka;

                    for (int kolo = 1; kolo <= 9; kolo++)
                    {
                        DateTime lastSunday = GetLastSundayOfMonth(tempDate.Year, tempDate.Month);
                        
                        var turnir = new Turnir
                        {
                            Naziv = $"{liga.Naziv} - Kolo {kolo}",
                            Status = StatusTurnira.Planiran,
                            DatumPocetka = lastSunday.AddHours(10), // Početak u 10:00h
                            DatumKraja = lastSunday.AddHours(18),
                            MaxIgraca = 64,
                            Lokacija = "Klupska Dvorana ePinPong",
                            Opis = $"Mjesečni ligaški turnir za {liga.Naziv}. Kolo {kolo} od 9. Nakon odigravanja svih kola, najbolji igrači će se plasirati na završni Masters.",
                            LigaID = liga.ID,
                            Kolo = kolo,
                            OrganizatorId = userId ?? string.Empty,
                            SlikaUrl = "https://images.unsplash.com/photo-1534158914592-062992fbe900?q=80&w=600&auto=format&fit=crop"
                        };
                        _context.Turniri.Add(turnir);
                        
                        // Pomjeri na sljedeći mjesec
                        tempDate = tempDate.AddMonths(1);
                    }

                    // Završni Masters
                    DateTime mastersSunday = GetLastSundayOfMonth(tempDate.Year, tempDate.Month);
                    var masters = new Turnir
                    {
                        Naziv = $"{liga.Naziv} - Završni Masters",
                        Status = StatusTurnira.Planiran,
                        DatumPocetka = mastersSunday.AddHours(10),
                        DatumKraja = mastersSunday.AddHours(18),
                        MaxIgraca = 64,
                        Lokacija = "Centralna Dvorana ePinPong",
                        Opis = $"Završni Masters turnir sezone za {liga.Naziv}. Najbolji igrači se bore za titulu šampiona!",
                        LigaID = liga.ID,
                        Kolo = 10, // Kolo 10 je završnica
                        OrganizatorId = userId ?? string.Empty,
                        SlikaUrl = "https://images.unsplash.com/photo-1609710223516-9cf611da2629?q=80&w=600&auto=format&fit=crop"
                    };
                    _context.Turniri.Add(masters);
                    
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "Uspješno ste kreirali ligu!";
                return RedirectToAction(nameof(Index));
            }
            return View(liga);
        }

        // POST: /Liga/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var liga = await _context.Lige
                .Include(l => l.Turniri)
                .FirstOrDefaultAsync(l => l.ID == id);
            if (liga != null)
            {
                if (liga.Turniri != null && liga.Turniri.Any())
                {
                    _context.Turniri.RemoveRange(liga.Turniri);
                }
                _context.Lige.Remove(liga);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Liga i svi njeni turniri su uspješno obrisani!";
            }
            return RedirectToAction(nameof(Index));
        }

        #region Pomoćne Metode

        private DateTime GetLastSundayOfMonth(int year, int month)
        {
            DateTime lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            int diff = (7 + (int)lastDay.DayOfWeek - (int)DayOfWeek.Sunday) % 7;
            return lastDay.AddDays(-diff);
        }

        private List<LigaStandingsViewModel> ObračunajTabeluLige(Liga liga)
        {
            var tabeleMap = new Dictionary<string, LigaStandingsViewModel>();
            var sviKorisnici = _context.Users.ToList();

            // Filtriraj turnire koji su završeni u okviru lige
            var zavrseniTurniri = liga.Turniri.Where(t => t.Status == StatusTurnira.Zavrsen).OrderBy(t => t.Kolo).ToList();

            foreach (var user in sviKorisnici)
            {
                tabeleMap[user.Id] = new LigaStandingsViewModel
                {
                    Korisnik = user,
                    UkupnoBodova = 0,
                    BrojOdigranihTurnira = 0,
                    BodoviPoKolima = new List<int>()
                };
            }

            foreach (var turnir in zavrseniTurniri)
            {
                var turnirSaPodacima = _context.Turniri
                    .Include(t => t.Registracije)
                        .ThenInclude(r => r.Korisnik)
                    .Include(t => t.Mecevi)
                        .ThenInclude(m => m.Igrac1)
                    .Include(t => t.Mecevi)
                        .ThenInclude(m => m.Igrac2)
                    .FirstOrDefault(t => t.ID == turnir.ID);

                if (turnirSaPodacima != null)
                {
                    var plasmani = _bracketService.IzracunajPlasman(turnirSaPodacima);

                    foreach (var item in tabeleMap)
                    {
                        string userId = item.Key;
                        var model = item.Value;

                        var plasmanInfo = plasmani.FirstOrDefault(p => p.KorisnikId == userId);
                        if (plasmanInfo != null)
                        {
                            model.UkupnoBodova += plasmanInfo.Bodovi;
                            model.BrojOdigranihTurnira++;
                            model.BodoviPoKolima.Add(plasmanInfo.Bodovi);
                        }
                        else
                        {
                            model.BodoviPoKolima.Add(0); // Nije učestvovao
                        }
                    }
                }
                else
                {
                    foreach (var item in tabeleMap)
                    {
                        item.Value.BodoviPoKolima.Add(0);
                    }
                }
            }

            return tabeleMap.Values
                .Where(v => v.BrojOdigranihTurnira > 0)
                .OrderByDescending(v => v.UkupnoBodova)
                .ToList();
        }

        #endregion
    }

    public class LigaStandingsViewModel
    {
        public ApplicationUser Korisnik { get; set; } = null!;
        public int BrojOdigranihTurnira { get; set; }
        public int UkupnoBodova { get; set; }
        public List<int> BodoviPoKolima { get; set; } = new List<int>();
    }
}
