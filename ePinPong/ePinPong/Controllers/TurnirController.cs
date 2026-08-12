using ePinPong.Data;
using ePinPong.Helpers;
using ePinPong.Models;
using ePinPong.Models.ViewModels;
using ePinPong.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ePinPong.Controllers
{
    public class TurnirController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBracketService _bracketService;
        private readonly ILeagueStandingsService _leagueStandingsService;
        private readonly ITurnirCompletionService _turnirCompletionService;
        private readonly IAuthorizationService _authorizationService;
        private readonly ILogger<TurnirController> _logger;
        private readonly IMastersRegistrationService _mastersRegistrationService;
        private readonly IStandingsCalculationService _standingsCalculationService;
        private readonly ITurnirCreatedNotifierService _turnirCreatedNotifier;


        public TurnirController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IBracketService bracketService,
            ILeagueStandingsService leagueStandingsService,
            ITurnirCompletionService turnirCompletionService,
            IAuthorizationService authorizationService,
            ILogger<TurnirController> logger,
            IMastersRegistrationService mastersRegistrationService,
            IStandingsCalculationService standingsCalculationService,
            ITurnirCreatedNotifierService turnirCreatedNotifier)
        {
            _context = context;
            _userManager = userManager;
            _bracketService = bracketService;
            _leagueStandingsService = leagueStandingsService;
            _turnirCompletionService = turnirCompletionService;
            _authorizationService = authorizationService;
            _logger = logger;
            _mastersRegistrationService = mastersRegistrationService;
            _standingsCalculationService = standingsCalculationService;
            _turnirCreatedNotifier = turnirCreatedNotifier;
        }

        // GET: /Turnir/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Organizator)
                .Include(t => t.Liga)
                .Include(t => t.Mecevi)
                    .ThenInclude(m => m.Igrac1)
                .Include(t => t.Mecevi)
                    .ThenInclude(m => m.Igrac2)
                .Include(t => t.Mecevi)
                    .ThenInclude(m => m.Igrac1Partner)
                .Include(t => t.Mecevi)
                    .ThenInclude(m => m.Igrac2Partner)
                .Include(t => t.Registracije)
                    .ThenInclude(r => r.Korisnik)
                .Include(t => t.TurnirParovi)
                    .ThenInclude(tp => tp.Igrac1)
                .Include(t => t.TurnirParovi)
                    .ThenInclude(tp => tp.Igrac2)
                .FirstOrDefaultAsync(t => t.ID == id);

            if (turnir == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            var meceviList = turnir.Mecevi.ToList();
            _bracketService.PropagirajBye(meceviList);

            _turnirCompletionService.EvaluateAndCloseIfFinished(turnir);

            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync();
            }

            var isRegistered = turnir.Registracije.Any(r => r.KorisnikID == userId);
            var isOrganizator = turnir.OrganizatorId == userId || User.IsInRole(AppConstants.Roles.Administrator);
            var isAdmin = User.IsInRole(AppConstants.Roles.Administrator);
            var isMasters = turnir.Liga != null && turnir.Kolo.HasValue && turnir.Kolo.Value == LigaTurnirHelper.GetMastersKolo(turnir.Liga);

            var ranking = _bracketService.IzracunajPlasman(turnir);
            var playerPoints = await _leagueStandingsService.GetPlayerPointsAsync(turnir);
            var groupStandings     = _standingsCalculationService.IzracunajTabeleGrupa(turnir);
            var pairGroupStandings = _standingsCalculationService.IzracunajTabeleGrupaParova(turnir);

            var viewModel = new TurnirDetailsViewModel
            {
                Turnir = turnir,
                IsRegistered = isRegistered,
                IsOrganizator = isOrganizator,
                IsAdmin = isAdmin,
                IsMasters = isMasters,
                CurrentUserId = userId,
                Ranking = ranking,
                PlayerPoints = playerPoints,
                GroupStandings     = groupStandings,
                PairGroupStandings = pairGroupStandings
            };

            if (isOrganizator && turnir.Status == StatusTurnira.Planiran)
            {
                var registrovaniKorisnikIds = turnir.Registracije.Select(r => r.KorisnikID).ToList();
                var slobodniKorisnici = await _userManager.Users
                    .Where(u => u.Id != BracketService.SLOBODAN && !registrovaniKorisnikIds.Contains(u.Id))
                    .ToListAsync();

                viewModel.SlobodniKorisnici = slobodniKorisnici.ToSelectList(
                    u => u.Id,
                    u => $"{(u.IsGost ? "[Gost] " : "")}{u.Ime} {u.Prezime} ({u.Grad})"
                );

                var sveLige = await _context.Lige.ToListAsync();
                viewModel.LigeZaSeeding = sveLige.ToSelectList(l => l.ID.ToString(), l => l.Naziv);

                var zavrseniTurniri = await _context.Turniri
                    .Where(t => t.Status == StatusTurnira.Zavrsen)
                    .ToListAsync();
                viewModel.TurniriZaSeeding = zavrseniTurniri.ToSelectList(t => t.ID.ToString(), t => t.Naziv);
            }

            return View(viewModel);
        }

        // GET: /Turnir/Create
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public IActionResult Create(int? ligaId = null)
        {
            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole(AppConstants.Roles.Administrator);

            var lige = _context.Lige.Include(l => l.Turniri).ToList();
            var dostupneLige = GetDostupneLige(lige, isAdmin, userId, ligaId);

            ViewBag.Lige = dostupneLige.ToSelectList(
                l => l.ID.ToString(),
                l => l.Naziv,
                optionLabel: "Ne pripada nijednoj ligi (Samostalni turnir)");

            var standaloneSunday = LigaTurnirHelper.GetDefaultStandaloneTurnirDatum();
            var standaloneStartStr = standaloneSunday.AddHours(9).AddMinutes(30).ToString("yyyy-MM-ddTHH:mm");
            var standaloneEndStr = standaloneSunday.AddHours(15).ToString("yyyy-MM-ddTHH:mm");

            var ligaDates = new Dictionary<string, object>
            {
                { "", new { start = standaloneStartStr, end = standaloneEndStr } }
            };

            foreach (var l in lige)
            {
                var lSunday = LigaTurnirHelper.GetNextTurnirDatumForLiga(l);
                var lStart = lSunday.AddHours(9).AddMinutes(30).ToString("yyyy-MM-ddTHH:mm");
                var lEnd = lSunday.AddHours(15).ToString("yyyy-MM-ddTHH:mm");
                ligaDates[l.ID.ToString()] = new { start = lStart, end = lEnd };
            }

            ViewBag.LigaDatesJson = System.Text.Json.JsonSerializer.Serialize(ligaDates);

            var model = new Turnir
            {
                DatumPocetka = standaloneSunday.Date.AddHours(9).AddMinutes(30),
                DatumKraja   = standaloneSunday.Date.AddHours(15)
            };

            if (ligaId.HasValue)
            {
                model.LigaID = ligaId;
                ViewBag.LockedLigaId = ligaId.Value;
                var liga = lige.FirstOrDefault(l => l.ID == ligaId.Value);
                ViewBag.LockedLigaNaziv = liga?.Naziv ?? "Odabrana liga";

                if (liga != null)
                {
                    ViewBag.CanCreateRegular = LigaTurnirHelper.CanCreateRegular(liga);
                    ViewBag.CanCreateMasters = LigaTurnirHelper.CanCreateMasters(liga);
                    ViewBag.BrojRegularnihTurnira = liga.BrojRegularnihTurnira;

                    var plannedDate = LigaTurnirHelper.GetNextTurnirDatumForLiga(liga);
                    model.DatumPocetka = plannedDate.AddHours(9).AddMinutes(30);
                    model.DatumKraja = plannedDate.AddHours(15);

                    var nextRegularKolo = LigaTurnirHelper.GetSljedeceKolo(liga);
                    if (nextRegularKolo.HasValue && LigaTurnirHelper.CanCreateRegular(liga))
                    {
                        model.Kolo = nextRegularKolo.Value;
                    }

                    if (!LigaTurnirHelper.CanCreateAnyTurnir(liga))
                    {
                        ViewBag.CreateMode = "Blocked";
                        ViewBag.CreateModeMessage = "U ovoj ligi više nije moguće kreirati nove turnire.";
                    }
                    else if (LigaTurnirHelper.CanCreateMasters(liga))
                    {
                        ViewBag.CreateMode = "Masters";
                        ViewBag.CreateModeMessage = "Ovo će biti završni Masters turnir za ligu.";
                        ViewBag.MastersKolo = LigaTurnirHelper.GetMastersKolo(liga);
                    }
                    else
                    {
                        ViewBag.CreateMode = "Regular";
                        ViewBag.CreateModeMessage = "Ovo će biti regularni turnir u ligi.";
                    }
                }
            }

            return View(model);
        }

        // POST: /Turnir/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> Create(Turnir turnir)
        {
            var userId = _userManager.GetUserId(User);
            if (userId != null)
            {
                turnir.OrganizatorId = userId;
            }

            // Uklonimo provjeru validnosti za navigaciona svojstva koja se popunjavaju naknadno
            ModelState.Remove("Organizator");
            ModelState.Remove("OrganizatorId");
            ModelState.Remove("Liga");

            if (turnir.DatumPocetka.Date < DateTime.Today)
            {
                ModelState.AddModelError(nameof(turnir.DatumPocetka), "Datum početka turnira ne može biti u prošlosti.");
            }

            if (turnir.DatumKraja.Date < turnir.DatumPocetka.Date)
            {
                ModelState.AddModelError(nameof(turnir.DatumKraja), "Datum završetka turnira ne može biti prije datuma početka.");
            }

            if (ModelState.IsValid)
            {
                var liga = turnir.LigaID.HasValue
                    ? await _context.Lige.FirstOrDefaultAsync(l => l.ID == turnir.LigaID.Value)
                    : null;
                var isMastersRequest = false;

                if (liga != null)
                {
                    var authResult = await _authorizationService.AuthorizeAsync(User, liga, "OrganizatorIliAdmin");
                    if (!authResult.Succeeded)
                    {
                        TempData["Error"] = "Možete kreirati turnir samo u ligi koju ste vi organizovali.";
                        return RedirectToAction("Details", "Liga", new { id = liga.ID });
                    }

                    isMastersRequest = turnir.Kolo.HasValue && turnir.Kolo.Value == LigaTurnirHelper.GetMastersKolo(liga);
                    var canCreateRegular = LigaTurnirHelper.CanCreateRegular(liga);
                    var canCreateMasters = LigaTurnirHelper.CanCreateMasters(liga);

                    if (!canCreateRegular && !canCreateMasters)
                    {
                        TempData["Error"] = "U ovoj ligi više nije moguće kreirati turnire.";
                        return RedirectToAction("Details", "Liga", new { id = liga.ID });
                    }

                    if (!isMastersRequest && !canCreateRegular)
                    {
                        TempData["Error"] = "U ovoj ligi više nisu dostupni regularni turniri. Može se kreirati samo Masters.";
                        return RedirectToAction("Details", "Liga", new { id = liga.ID });
                    }

                    if (isMastersRequest && !canCreateMasters)
                    {
                        TempData["Error"] = "Masters za ovu ligu je već kreiran ili još nisu odigrana sva regularna kola.";
                        return RedirectToAction("Details", "Liga", new { id = liga.ID });
                    }

                    if (isMastersRequest)
                    {
                        turnir.Kolo = LigaTurnirHelper.GetMastersKolo(liga);
                        if (string.IsNullOrWhiteSpace(turnir.Naziv))
                        {
                            turnir.Naziv = $"{liga.Naziv} - Masters";
                        }
                        if (string.IsNullOrWhiteSpace(turnir.Opis))
                        {
                            turnir.Opis = $"Završni Masters turnir za {liga.Naziv}.";
                        }
                    }
                    else
                    {
                        var nextRegularKolo = LigaTurnirHelper.GetSljedeceKolo(liga);
                        if (nextRegularKolo.HasValue)
                        {
                            turnir.Kolo = nextRegularKolo;
                        }
                        else
                        {
                            TempData["Error"] = "Nema više dostupnih regularnih kola za ovu ligu.";
                            return RedirectToAction("Details", "Liga", new { id = liga.ID });
                        }

                        if (string.IsNullOrWhiteSpace(turnir.Naziv))
                        {
                            turnir.Naziv = $"{liga.Naziv} - Kolo {turnir.Kolo}";
                        }
                    }
                }

                if (string.IsNullOrEmpty(turnir.SlikaUrl))
                {
                    turnir.SlikaUrl = AppConstants.DefaultTurnirSlikaUrl;
                }
                turnir.Status = StatusTurnira.Planiran;
                _context.Add(turnir);
                await _context.SaveChangesAsync();

                if (liga != null && isMastersRequest)
                {
                    await _mastersRegistrationService.AutoRegistrirajIgraceLigeAsync(liga, turnir.ID);
                }

                // NAKON KREIRANJA - Posalji notifikacije pratiocima organizatora
                var organizatorName = User.Identity?.Name ?? "Organizator";
                await _turnirCreatedNotifier.ObavijestiPratioceAsync(turnir, organizatorName);

                TempData["Success"] = "Uspješno ste kreirali turnir!";
                return RedirectToAction("Details", "Turnir", new { id = turnir.ID });
            }

            var isAdmin = User.IsInRole(AppConstants.Roles.Administrator);
            var sveLige = _context.Lige.Include(l => l.Turniri).ToList();
            var dostupneLige = GetDostupneLige(sveLige, isAdmin, userId, turnir.LigaID);
            ViewBag.Lige = dostupneLige.ToSelectList(
                l => l.ID.ToString(),
                l => l.Naziv,
                turnir.LigaID?.ToString(),
                "Ne pripada nijednoj ligi (Samostalni turnir)");

            return View(turnir);
        }


        // GET: /Turnir/Edit/5
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> Edit(int id)
        {
            var turnir = await _context.Turniri.FindAsync(id);
            if (turnir == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole(AppConstants.Roles.Administrator);
            var authResult = await _authorizationService.AuthorizeAsync(User, turnir, "OrganizatorIliAdmin");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var returnUrl = Request.Query["returnUrl"].ToString();
            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                returnUrl = Request.Headers["Referer"].ToString();
            }

            if (string.IsNullOrWhiteSpace(returnUrl) && turnir.LigaID.HasValue)
            {
                returnUrl = Url.Action("Details", "Liga", new { id = turnir.LigaID.Value });
            }

            ViewBag.ReturnUrl = returnUrl;

            ViewBag.StatusList = SelectListExtensions.EnumToSelectList<StatusTurnira>(turnir.Status);

            var sveLige = await _context.Lige
                .Include(l => l.Turniri)
                .ToListAsync();

            var dostupneLige = GetDostupneLige(sveLige, isAdmin, userId, turnir.LigaID);

            ViewBag.Lige = dostupneLige.ToSelectList(
                l => l.ID.ToString(),
                l => l.Naziv,
                turnir.LigaID?.ToString(),
                "Ne pripada nijednoj ligi (Samostalni turnir)");

            var odabranaLiga = turnir.LigaID.HasValue ? sveLige.FirstOrDefault(l => l.ID == turnir.LigaID.Value) : null;
            ViewBag.IsMasters = odabranaLiga != null && turnir.Kolo.HasValue && turnir.Kolo.Value == LigaTurnirHelper.GetMastersKolo(odabranaLiga);

            return View(turnir);
        }

        // POST: /Turnir/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> Edit(int id, Turnir turnir)
        {
            if (id != turnir.ID)
            {
                return NotFound();
            }

            var postojeci = await _context.Turniri.AsNoTracking().FirstOrDefaultAsync(t => t.ID == id);
            if (postojeci == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var isAdmin = User.IsInRole(AppConstants.Roles.Administrator);

            var authResult = await _authorizationService.AuthorizeAsync(User, postojeci, "OrganizatorIliAdmin");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            // Ako organizator uređuje završen turnir, samo liga se smije mijenjati
            if (!isAdmin && postojeci.Status == StatusTurnira.Zavrsen)
            {
                turnir.Naziv = postojeci.Naziv;
                turnir.Status = postojeci.Status;
                turnir.MaxIgraca = postojeci.MaxIgraca;
                turnir.DatumPocetka = postojeci.DatumPocetka;
                turnir.DatumKraja = postojeci.DatumKraja;
                turnir.Lokacija = postojeci.Lokacija;
                turnir.Opis = postojeci.Opis;
                turnir.SlikaUrl = postojeci.SlikaUrl;
                turnir.TipTakmicenja = postojeci.TipTakmicenja;
                turnir.SistemTurnira = postojeci.SistemTurnira;
            }

            // Validacija: Nova liga ne smije imati popunjen broj regularnih turnira
            if (turnir.LigaID.HasValue && turnir.LigaID != postojeci.LigaID)
            {
                var odabranaLigaObj = await _context.Lige
                    .Include(l => l.Turniri)
                    .FirstOrDefaultAsync(l => l.ID == turnir.LigaID.Value);

                if (odabranaLigaObj != null)
                {
                    if (!isAdmin)
                    {
                        bool jeIstiOrganizator = odabranaLigaObj.OrganizatorId == userId || !odabranaLigaObj.Turniri.Any() || odabranaLigaObj.Turniri.Any(t => t.OrganizatorId == userId);
                        if (!jeIstiOrganizator)
                        {
                            ModelState.AddModelError("LigaID", "Možete izabrati samo ligu koju ste vi organizovali.");
                        }
                    }

                    if (!LigaTurnirHelper.CanCreateRegular(odabranaLigaObj))
                    {
                        ModelState.AddModelError("LigaID", $"U ligi '{odabranaLigaObj.Naziv}' je već kreiran maksimalan broj regularnih turnira ({odabranaLigaObj.BrojRegularnihTurnira}). Ova liga se ne može izabrati.");
                    }
                }
            }

            // Očuvaj organizatora i plasmane
            turnir.OrganizatorId = postojeci.OrganizatorId;
            turnir.PobjednikID = postojeci.PobjednikID;
            turnir.DrugoplasiraniID = postojeci.DrugoplasiraniID;
            turnir.TrecaplasiraniID = postojeci.TrecaplasiraniID;

            if (!turnir.LigaID.HasValue)
            {
                turnir.Kolo = null;
            }
            else if (postojeci.Kolo.HasValue && postojeci.LigaID == turnir.LigaID)
            {
                turnir.Kolo = postojeci.Kolo;
            }

            ModelState.Remove("Organizator");
            ModelState.Remove("OrganizatorId");
            ModelState.Remove("Liga");

            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(turnir.SlikaUrl))
                {
                    turnir.SlikaUrl = AppConstants.DefaultTurnirSlikaUrl;
                }
                try
                {
                    _context.Update(turnir);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogWarning(ex, "Concurrency konflikt prilikom ažuriranja turnira {TurnirId}.", turnir.ID);
                    if (!TurnirExists(turnir.ID)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Details), new { id = turnir.ID });
            }

            ViewBag.StatusList = SelectListExtensions.EnumToSelectList<StatusTurnira>(turnir.Status);

            var sveLige = await _context.Lige
                .Include(l => l.Turniri)
                .ToListAsync();

            var dostupneLige = GetDostupneLige(sveLige, isAdmin, userId, turnir.LigaID);

            ViewBag.Lige = dostupneLige.ToSelectList(
                l => l.ID.ToString(),
                l => l.Naziv,
                turnir.LigaID?.ToString(),
                "Ne pripada nijednoj ligi (Samostalni turnir)");

            var postojecaLiga = turnir.LigaID.HasValue ? sveLige.FirstOrDefault(l => l.ID == turnir.LigaID.Value) : null;
            ViewBag.IsMasters = postojecaLiga != null && turnir.Kolo.HasValue && turnir.Kolo.Value == LigaTurnirHelper.GetMastersKolo(postojecaLiga);

            return View(turnir);
        }

        // POST: /Turnir/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppConstants.Roles.Administrator)]
        public async Task<IActionResult> DeleteConfirmed(int id, string? returnUrl = null)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Registracije)
                .Include(t => t.Mecevi)
                .Include(t => t.TurnirParovi)
                .FirstOrDefaultAsync(t => t.ID == id);

            if (turnir == null)
            {
                return NotFound();
            }

            if (turnir.Registracije.Any())
            {
                _context.Registracije.RemoveRange(turnir.Registracije);
            }

            if (turnir.Mecevi.Any())
            {
                _context.Mecevi.RemoveRange(turnir.Mecevi);
            }

            if (turnir.TurnirParovi.Any())
            {
                _context.TurnirParovi.RemoveRange(turnir.TurnirParovi);
            }

            _context.Turniri.Remove(turnir);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Turnir je uspješno obrisan!";

            if (string.IsNullOrWhiteSpace(returnUrl))
            {
                returnUrl = Request.Headers["Referer"].ToString();
            }

            if (string.IsNullOrWhiteSpace(returnUrl) && turnir.LigaID.HasValue)
            {
                returnUrl = Url.Action("Details", "Liga", new { id = turnir.LigaID.Value });
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        // GET: /Turnir/DohvatiRankinge?sourceType=Liga&sourceId=3
        [HttpGet]
        public async Task<IActionResult> DohvatiRankinge(string sourceType, int sourceId)
        {
            var standings = new Dictionary<string, int>();

            if (sourceType == "Liga")
            {
                var liga = await _context.Lige.FirstOrDefaultAsync(l => l.ID == sourceId);

                if (liga != null)
                {
                    var tabela = await _leagueStandingsService.GetLeagueTableAsync(liga);
                    foreach (var red in tabela)
                    {
                        standings[red.Korisnik.Id] = red.UkupnoBodova;
                    }
                }
            }
            else if (sourceType == "Turnir")
            {
                var turnir = await _context.Turniri
                    .Include(t => t.Registracije)
                        .ThenInclude(r => r.Korisnik)
                    .Include(t => t.Mecevi)
                        .ThenInclude(m => m.Igrac1)
                    .Include(t => t.Mecevi)
                        .ThenInclude(m => m.Igrac2)
                    .FirstOrDefaultAsync(t => t.ID == sourceId);

                if (turnir != null)
                {
                    var plasmani = _bracketService.IzracunajPlasman(turnir);
                    foreach (var pl in plasmani)
                    {
                        standings[pl.KorisnikId] = pl.Bodovi;
                    }
                }
            }

            return Json(standings);
        }


        private static List<Liga> GetDostupneLige(List<Liga> sveLige, bool isAdmin, string? userId, int? trenutnaLigaId = null)
        {
            if (isAdmin)
            {
                return sveLige
                    .Where(l => l.ID == trenutnaLigaId || LigaTurnirHelper.CanCreateRegular(l))
                    .ToList();
            }
            return sveLige
                .Where(l => l.ID == trenutnaLigaId ||
                    ((l.OrganizatorId == userId || !l.Turniri.Any() || l.Turniri.Any(t => t.OrganizatorId == userId))
                     && LigaTurnirHelper.CanCreateRegular(l)))
                .ToList();
        }

        private bool TurnirExists(int id)
        {
            return _context.Turniri.Any(e => e.ID == id);
        }
    }
}
