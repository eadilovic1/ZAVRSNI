using ePinPong.Data;
using ePinPong.Models;
using ePinPong.Models.ViewModels;
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
        private readonly ILeagueStandingsService _leagueStandingsService;
        private readonly IAuthorizationService _authorizationService;

        public LigaController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IBracketService bracketService,
            ILeagueStandingsService leagueStandingsService,
            IAuthorizationService authorizationService)
        {
            _context = context;
            _userManager = userManager;
            _bracketService = bracketService;
            _leagueStandingsService = leagueStandingsService;
            _authorizationService = authorizationService;
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
                .Include(l => l.Organizator)
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
            var standings = await _leagueStandingsService.GetLeagueTableAsync(liga);
            ViewBag.Standings = standings;
            ViewBag.ZavrseniTurniri = liga.Turniri
                .Where(t => t.Status == StatusTurnira.Zavrsen)
                .OrderBy(t => t.Kolo)
                .ToList();

            var userId = _userManager.GetUserId(User);
            ViewBag.IsAdminOrOrganizator = User.Identity?.IsAuthenticated == true && (User.IsInRole("Administrator") || (User.IsInRole("Organizator") && liga.OrganizatorId == userId));
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
                .Include(l => l.Turniri)
                .FirstOrDefaultAsync(l => l.ID == id);

            if (liga == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, liga, "OrganizatorIliAdmin");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var mastersKoloForLiga = liga.BrojRegularnihTurnira + 1;
            var completedRegularCount = await _context.Turniri
                .CountAsync(t => t.LigaID == liga.ID && t.Status == StatusTurnira.Zavrsen && t.Kolo.HasValue && t.Kolo.Value != mastersKoloForLiga);

            ViewBag.MinBrojRegularnihTurnira = Math.Max(1, completedRegularCount);
            ViewBag.IsBrojKolaLocked = LigaTurnirHelper.IsLigaOkoncana(liga);
            return View(liga);
        }

        // POST: /Liga/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Organizator")]
        public async Task<IActionResult> Edit(Liga liga)
        {
            var existingLiga = await _context.Lige
                .Include(l => l.Turniri)
                .FirstOrDefaultAsync(l => l.ID == liga.ID);

            if (existingLiga == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, existingLiga, "OrganizatorIliAdmin");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var isOkoncana = LigaTurnirHelper.IsLigaOkoncana(existingLiga);
            if (isOkoncana && liga.BrojRegularnihTurnira != existingLiga.BrojRegularnihTurnira)
            {
                ModelState.AddModelError(nameof(liga.BrojRegularnihTurnira), "Broj kola se ne može mijenjati jer su svi turniri i završni Masters ove lige već odigrani.");
                liga.BrojRegularnihTurnira = existingLiga.BrojRegularnihTurnira;
            }

            var mastersKoloForExisting = existingLiga.BrojRegularnihTurnira + 1;
            var completedRegularCount = await _context.Turniri
                .CountAsync(t => t.LigaID == existingLiga.ID && t.Status == StatusTurnira.Zavrsen && t.Kolo.HasValue && t.Kolo.Value != mastersKoloForExisting);
            var minAllowed = Math.Max(1, completedRegularCount);

            if (!isOkoncana && liga.BrojRegularnihTurnira < minAllowed)
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
                if (!isOkoncana)
                {
                    existingLiga.BrojRegularnihTurnira = liga.BrojRegularnihTurnira;
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Uspješno ste ažurirali ligu!";
                return RedirectToAction(nameof(Details), new { id = existingLiga.ID });
            }

            ViewBag.MinBrojRegularnihTurnira = minAllowed;
            ViewBag.IsBrojKolaLocked = isOkoncana;
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
                liga.OrganizatorId = _userManager.GetUserId(User);
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

            var authResult = await _authorizationService.AuthorizeAsync(User, liga, "OrganizatorIliAdmin");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            // Izračunaj datum mastersa: zadnja nedjelja u mjesecu nakon zadnjeg turnira u ligi
            var zadnjaNedjelja = LigaTurnirHelper.GetNextTurnirDatumForLiga(liga);
            var mastersStart = zadnjaNedjelja.AddHours(10);
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
            var korisniciNaRankingu = await LigaRankingHelper.GetLeagueMastersParticipantIdsAsync(_context, _bracketService, liga);
            if (!korisniciNaRankingu.Any())
            {
                return;
            }

            var postojeciIds = await _context.Registracije
                .Where(r => r.TurnirID == turnirId)
                .Select(r => r.KorisnikID)
                .ToHashSetAsync();

            var noveRegistracije = korisniciNaRankingu
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

        #endregion
    }

}
