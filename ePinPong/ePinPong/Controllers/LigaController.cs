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
            ViewBag.ZavrseniTurniri = liga.Turniri
                .Where(t => t.Status == StatusTurnira.Zavrsen)
                .OrderBy(t => t.Kolo)
                .ToList();

            var userId = _userManager.GetUserId(User);
            ViewBag.IsAdminOrOrganizator = User.Identity?.IsAuthenticated == true && (User.IsInRole("Administrator") || User.IsInRole("Organizator"));
            ViewBag.BrojRegularnihTurnira = liga.BrojRegularnihTurnira;
            ViewBag.MastersKolo = LigaTurnirHelper.GetMastersKolo(liga);
            ViewBag.CanCreateRegular = LigaTurnirHelper.CanCreateRegular(liga);
            ViewBag.CanCreateMasters = LigaTurnirHelper.CanCreateMasters(liga);
            ViewBag.OdigranoRegularnih = LigaTurnirHelper.GetZavrseniRegularniTurniri(liga).Count();

            return View(liga);
        }

        // GET: /Liga/Create
        [Authorize(Roles = "Administrator,Organizator")]
        public IActionResult Create()
        {
            return View(new Liga
            {
                DatumPocetka = GetDefaultLigaStartDate()
            });
        }

        // GET: /Liga/Edit/5
        [Authorize(Roles = "Administrator,Organizator")]
        public async Task<IActionResult> Edit(int id)
        {
            var liga = await _context.Lige
                .FirstOrDefaultAsync(l => l.ID == id);

            if (liga == null)
            {
                return NotFound();
            }

            var mastersKoloForLiga = liga.BrojRegularnihTurnira + 1;
            var completedRegularCount = await _context.Turniri
                .CountAsync(t => t.LigaID == liga.ID && t.Status == StatusTurnira.Zavrsen && t.Kolo.HasValue && t.Kolo.Value != mastersKoloForLiga);

            ViewBag.MinBrojRegularnihTurnira = Math.Max(1, completedRegularCount);
            return View(liga);
        }

        // POST: /Liga/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Organizator")]
        public async Task<IActionResult> Edit(Liga liga)
        {
            var existingLiga = await _context.Lige
                .FirstOrDefaultAsync(l => l.ID == liga.ID);

            if (existingLiga == null)
            {
                return NotFound();
            }

            var mastersKoloForExisting = existingLiga.BrojRegularnihTurnira + 1;
            var completedRegularCount = await _context.Turniri
                .CountAsync(t => t.LigaID == existingLiga.ID && t.Status == StatusTurnira.Zavrsen && t.Kolo.HasValue && t.Kolo.Value != mastersKoloForExisting);
            var minAllowed = Math.Max(1, completedRegularCount);

            if (liga.BrojRegularnihTurnira < minAllowed)
            {
                ModelState.AddModelError(nameof(liga.BrojRegularnihTurnira), $"Broj regularnih turnira ne može biti manji od {minAllowed} jer je već odigrano {completedRegularCount} kola.");
            }

            if (liga.DatumPocetka.Date < DateTime.Today)
            {
                ModelState.AddModelError(nameof(liga.DatumPocetka), "Datum početka lige ne može biti u prošlosti.");
            }

            if (liga.BrojRegularnihTurnira < 1)
            {
                liga.BrojRegularnihTurnira = 1;
            }

            if (ModelState.IsValid)
            {
                existingLiga.Naziv = liga.Naziv;
                existingLiga.Opis = liga.Opis;
                existingLiga.Sezona = liga.Sezona;
                existingLiga.DatumPocetka = liga.DatumPocetka;
                existingLiga.BrojRegularnihTurnira = liga.BrojRegularnihTurnira;

                await _context.SaveChangesAsync();
                TempData["Success"] = "Uspješno ste ažurirali ligu!";
                return RedirectToAction(nameof(Details), new { id = existingLiga.ID });
            }

            ViewBag.MinBrojRegularnihTurnira = minAllowed;
            return View(existingLiga);
        }

        // POST: /Liga/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Organizator")]
        public async Task<IActionResult> Create(Liga liga, bool autoGenerisiTurnire)
        {
            if (liga.DatumPocetka == default)
            {
                liga.DatumPocetka = GetDefaultLigaStartDate();
            }

            if (liga.DatumPocetka.Date < DateTime.Today)
            {
                ModelState.AddModelError(nameof(liga.DatumPocetka), "Datum početka lige ne može biti u prošlosti.");
            }

            if (liga.BrojRegularnihTurnira < 1)
            {
                liga.BrojRegularnihTurnira = 1;
            }

            if (ModelState.IsValid)
            {
                _context.Add(liga);
                await _context.SaveChangesAsync();

                if (autoGenerisiTurnire)
                {
                    var userId = _userManager.GetUserId(User);
                    int brojKola = liga.BrojRegularnihTurnira;

                    for (int kolo = 1; kolo <= brojKola; kolo++)
                    {
                        var lastSunday = LigaTurnirHelper.GetRegularTurnirDatum(liga, kolo);

                        var turnir = new Turnir
                        {
                            Naziv = $"{liga.Naziv} - Kolo {kolo}",
                            Status = StatusTurnira.Planiran,
                            DatumPocetka = lastSunday.AddHours(10),
                            DatumKraja = lastSunday.AddHours(18),
                            MaxIgraca = 64,
                            Lokacija = "Klupska Dvorana ePinPong",
                            Opis = $"Mjesečni ligaški turnir za {liga.Naziv}. Kolo {kolo} od {brojKola}. Nakon odigravanja svih kola, najbolji igrači će se plasirati na završni Masters.",
                            LigaID = liga.ID,
                            Kolo = kolo,
                            OrganizatorId = userId ?? string.Empty,
                            SlikaUrl = "https://images.unsplash.com/photo-1534158914592-062992fbe900?q=80&w=600&auto=format&fit=crop"
                        };
                        _context.Turniri.Add(turnir);
                    }

                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "Uspješno ste kreirali ligu!";
                return RedirectToAction(nameof(Index));
            }
            return View(liga);
        }

        // POST: /Liga/CreateMasters/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Organizator")]
        public async Task<IActionResult> CreateMasters(int id)
        {
            var liga = await _context.Lige
                .Include(l => l.Turniri)
                .FirstOrDefaultAsync(l => l.ID == id);

            if (liga == null)
            {
                return NotFound();
            }

            if (!LigaTurnirHelper.CanCreateMasters(liga))
            {
                TempData["Error"] = "Masters za ovu ligu je već kreiran ili još nisu odigrana sva regularna kola.";
                return RedirectToAction(nameof(Details), new { id = liga.ID });
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var mastersStart = DateTime.Today.AddDays(7).AddHours(10);
            var mastersEnd = mastersStart.AddHours(8);

            var turnir = new Turnir
            {
                Naziv = "ZAVRŠNI MASTERS",
                Status = StatusTurnira.Planiran,
                DatumPocetka = mastersStart,
                DatumKraja = mastersEnd,
                MaxIgraca = 64,
                Lokacija = "Klupska Dvorana ePinPong",
                Opis = $"Završni Masters turnir za {liga.Naziv}.",
                LigaID = liga.ID,
                Kolo = LigaTurnirHelper.GetMastersKolo(liga),
                OrganizatorId = userId,
                SlikaUrl = "https://images.unsplash.com/photo-1534158914592-062992fbe900?q=80&w=1200&auto=format&fit=crop"
            };

            _context.Turniri.Add(turnir);
            await _context.SaveChangesAsync();

            await AutoRegistrirajIgraceLigeAsync(liga, turnir.ID);

            TempData["Success"] = "Uspješno ste kreirali završni Masters turnir.";
            return RedirectToAction("Details", "Turnir", new { id = turnir.ID });
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

        private async Task AutoRegistrirajIgraceLigeAsync(Liga liga, int turnirId)
        {
            var mastersKolo = LigaTurnirHelper.GetMastersKolo(liga);
            var regularniTurniriLiga = await _context.Turniri
                .Where(t => t.LigaID == liga.ID && t.Kolo.HasValue && t.Kolo.Value != mastersKolo)
                .Select(t => t.ID)
                .ToListAsync();

            if (!regularniTurniriLiga.Any())
            {
                return;
            }

            var korisniciIzLige = await _context.Registracije
                .Where(r => regularniTurniriLiga.Contains(r.TurnirID))
                .Select(r => r.KorisnikID)
                .Distinct()
                .ToListAsync();

            var postojeciIds = await _context.Registracije
                .Where(r => r.TurnirID == turnirId)
                .Select(r => r.KorisnikID)
                .ToHashSetAsync();

            var noveRegistracije = korisniciIzLige
                .Where(korisnikId => !postojeciIds.Contains(korisnikId))
                .Select(korisnikId => new Registracija
                {
                    TurnirID = turnirId,
                    KorisnikID = korisnikId,
                    Odobren = true,
                    DatumRegistracije = DateTime.Now,
                    Sesir = 1
                })
                .ToList();

            if (noveRegistracije.Any())
            {
                _context.Registracije.AddRange(noveRegistracije);
                await _context.SaveChangesAsync();
            }
        }

        #region Pomoćne Metode

        private DateTime GetDefaultLigaStartDate()
        {
            var today = DateTime.Today;
            var currentMonthLastSunday = GetLastSundayOfMonth(today.Year, today.Month);

            if (currentMonthLastSunday < today)
            {
                var nextMonth = new DateTime(today.Year, today.Month, 1).AddMonths(1);
                return GetLastSundayOfMonth(nextMonth.Year, nextMonth.Month);
            }

            return currentMonthLastSunday;
        }

        private static DateTime GetLastSundayOfMonth(int year, int month)
        {
            var normalizedMonth = month <= 0 ? 1 : month > 12 ? 12 : month;
            var normalizedYear = month <= 0 ? year - 1 : month > 12 ? year + 1 : year;
            var lastDay = new DateTime(normalizedYear, normalizedMonth, DateTime.DaysInMonth(normalizedYear, normalizedMonth));
            int diff = (7 + (int)lastDay.DayOfWeek - (int)DayOfWeek.Sunday) % 7;
            return lastDay.AddDays(-diff).Date;
        }

        private List<LigaStandingsViewModel> ObračunajTabeluLige(Liga liga)
        {
            var tabeleMap = new Dictionary<string, LigaStandingsViewModel>();
            var sviKorisnici = _context.Users.Where(u => u.Id != "SLOBODAN").ToList();

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
