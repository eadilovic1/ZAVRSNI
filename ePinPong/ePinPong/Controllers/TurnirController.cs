using ePinPong.Data;
using ePinPong.Helpers;
using ePinPong.Interfaces;
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
        private readonly IMailService _mailService;
        private readonly IBracketService _bracketService;
        private readonly ILeagueStandingsService _leagueStandingsService;
        private readonly ITurnirCompletionService _turnirCompletionService;
        private readonly IAuthorizationService _authorizationService;

        public TurnirController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IMailService mailService,
            IBracketService bracketService,
            ILeagueStandingsService leagueStandingsService,
            ITurnirCompletionService turnirCompletionService,
            IAuthorizationService authorizationService)
        {
            _context = context;
            _userManager = userManager;
            _mailService = mailService;
            _bracketService = bracketService;
            _leagueStandingsService = leagueStandingsService;
            _turnirCompletionService = turnirCompletionService;
            _authorizationService = authorizationService;
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
            var isOrganizator = turnir.OrganizatorId == userId || User.IsInRole("Administrator");
            var isAdmin = User.IsInRole("Administrator");
            var isMasters = turnir.Liga != null && turnir.Kolo.HasValue && turnir.Kolo.Value == LigaTurnirHelper.GetMastersKolo(turnir.Liga);

            var ranking = _bracketService.IzracunajPlasman(turnir);
            var playerPoints = await _leagueStandingsService.GetPlayerPointsAsync(turnir);

            var viewModel = new TurnirDetailsViewModel
            {
                Turnir = turnir,
                IsRegistered = isRegistered,
                IsOrganizator = isOrganizator,
                IsAdmin = isAdmin,
                IsMasters = isMasters,
                CurrentUserId = userId,
                Ranking = ranking,
                PlayerPoints = playerPoints
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
            var dostupneLige = lige
                .Where(l => (isAdmin || l.OrganizatorId == userId) &&
                            ((ligaId.HasValue && l.ID == ligaId.Value) || LigaTurnirHelper.CanCreateRegular(l)))
                .ToList();

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
                    if (liga.OrganizatorId != userId && !User.IsInRole(AppConstants.Roles.Administrator))
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
                    await AutoRegistrirajIgraceLigeAsync(liga, turnir.ID);
                }

                // NAKON KREIRANJA - Posalji notifikacije pratiocima organizatora (analogno ePazaru!)
                var pratioci = await _context.Pracenja
                    .Where(p => p.PraceniID == turnir.OrganizatorId)
                    .Select(p => p.PratilacID)
                    .ToListAsync();

                var organizatorName = User.Identity?.Name ?? "Organizator";

                foreach (var pratilacId in pratioci)
                {
                    // 1. In-app notifikacija
                    var notifikacija = new Notifikacija
                    {
                        KorisnikId = pratilacId,
                        Sadrzaj = $"Organizator <strong>{organizatorName}</strong> je objavio novi turnir: <strong><a href='/Turnir/Details/{turnir.ID}'>{turnir.Naziv}</a></strong>!",
                        DatumKreiranja = DateTime.Now,
                        Procitana = false
                    };
                    _context.Notifikacije.Add(notifikacija);

                    // 2. Email notifikacija (putem IMailService)
                    var korisnik = await _userManager.FindByIdAsync(pratilacId);
                    if (korisnik != null && !string.IsNullOrEmpty(korisnik.Email))
                    {
                        await _mailService.SendEmailAsync(
                            korisnik.Email, 
                            "Novi turnir na ePinPong!", 
                            $"Zdravo {korisnik.Ime},<br><br>Organizator <b>{organizatorName}</b> je objavio novi turnir: <b>{turnir.Naziv}</b>.<br>Datum pocetka: {turnir.DatumPocetka.ToShortDateString()}<br><br>Prijavite se odmah na ePinPong!"
                        );
                    }
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Uspješno ste kreirali turnir!";
                return RedirectToAction("Details", "Turnir", new { id = turnir.ID });
            }

            var isAdmin = User.IsInRole("Administrator");
            var sveLige = _context.Lige.Include(l => l.Turniri).ToList();
            var dostupneLige = sveLige
                .Where(l => (isAdmin || l.OrganizatorId == userId) && ((turnir.LigaID.HasValue && l.ID == turnir.LigaID.Value) || LigaTurnirHelper.CanCreateRegular(l)))
                .ToList();
            ViewBag.Lige = dostupneLige.ToSelectList(
                l => l.ID.ToString(),
                l => l.Naziv,
                turnir.LigaID?.ToString(),
                "Ne pripada nijednoj ligi (Samostalni turnir)");

            return View(turnir);
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
            var isAdmin = User.IsInRole("Administrator");
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

            List<Liga> dostupneLige;
            if (isAdmin)
            {
                dostupneLige = sveLige
                    .Where(l => l.ID == turnir.LigaID || LigaTurnirHelper.CanCreateRegular(l))
                    .ToList();
            }
            else
            {
                // Organizator može izabrati samo ligu koju je sam organizovao i čiji broj regularnih turnira nije popunjen (ili trenutnu ligu turnira)
                dostupneLige = sveLige
                    .Where(l => l.ID == turnir.LigaID || ((l.OrganizatorId == userId || !l.Turniri.Any() || l.Turniri.Any(t => t.OrganizatorId == userId)) && LigaTurnirHelper.CanCreateRegular(l)))
                    .ToList();
            }

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
            var isAdmin = User.IsInRole("Administrator");

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
                catch (DbUpdateConcurrencyException)
                {
                    if (!TurnirExists(turnir.ID)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Details), new { id = turnir.ID });
            }

            ViewBag.StatusList = SelectListExtensions.EnumToSelectList<StatusTurnira>(turnir.Status);

            var sveLige = await _context.Lige
                .Include(l => l.Turniri)
                .ToListAsync();

            List<Liga> dostupneLige;
            if (isAdmin)
            {
                dostupneLige = sveLige
                    .Where(l => l.ID == turnir.LigaID || LigaTurnirHelper.CanCreateRegular(l))
                    .ToList();
            }
            else
            {
                dostupneLige = sveLige
                    .Where(l => l.ID == turnir.LigaID || ((l.OrganizatorId == userId || !l.Turniri.Any() || l.Turniri.Any(t => t.OrganizatorId == userId)) && LigaTurnirHelper.CanCreateRegular(l)))
                    .ToList();
            }

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

            if (!User.IsInRole(AppConstants.Roles.Administrator))
            {
                return Forbid();
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

        // POST: /Turnir/Registracija/5
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Registracija(int id)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Registracije)
                .Include(t => t.Mecevi)
                .FirstOrDefaultAsync(t => t.ID == id);

            if (turnir == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            if (turnir.Status != StatusTurnira.Planiran || turnir.Mecevi.Any())
            {
                TempData["Error"] = "Prijave su zatvorene jer su grupe već izvučene.";
                return RedirectToAction(nameof(Details), new { id = turnir.ID });
            }

            // Provjera da li je vec registrovan
            if (turnir.Registracije.Any(r => r.KorisnikID == userId))
            {
                TempData["Error"] = "Već ste prijavljeni na ovaj turnir.";
                return RedirectToAction(nameof(Details), new { id = turnir.ID });
            }

            // Provjera kapaciteta
            if (turnir.Registracije.Count >= turnir.MaxIgraca)
            {
                TempData["Error"] = "Turnir je popunjen.";
                return RedirectToAction(nameof(Details), new { id = turnir.ID });
            }

            var registracija = new Registracija
            {
                TurnirID = turnir.ID,
                KorisnikID = userId,
                DatumRegistracije = DateTime.Now,
                Odobren = true // Automatski odobrena prijava u prototipu
            };

            _context.Registracije.Add(registracija);

            // In-app notifikacija organizatoru turnira
            var igrac = await _userManager.FindByIdAsync(userId);
            var notifikacija = new Notifikacija
            {
                KorisnikId = turnir.OrganizatorId,
                Sadrzaj = $"Igrač <strong>{igrac?.Ime} {igrac?.Prezime}</strong> se prijavio na vaš turnir <strong>{turnir.Naziv}</strong>.",
                DatumKreiranja = DateTime.Now,
                Procitana = false
            };
            _context.Notifikacije.Add(notifikacija);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Uspješno ste se prijavili na turnir!";
            return RedirectToAction(nameof(Details), new { id = turnir.ID });
        }

        // POST: /Turnir/Odjava/5
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Odjava(int id)
        {
            var userId = _userManager.GetUserId(User);
            var turnir = await _context.Turniri
                .Include(t => t.Mecevi)
                .FirstOrDefaultAsync(t => t.ID == id);

            if (turnir == null) return NotFound();

            if (turnir.Status != StatusTurnira.Planiran || turnir.Mecevi.Any())
            {
                TempData["Error"] = "Odjave su zatvorene jer su grupe već izvučene.";
                return RedirectToAction(nameof(Details), new { id = id });
            }

            var registracija = await _context.Registracije
                .FirstOrDefaultAsync(r => r.TurnirID == id && r.KorisnikID == userId);

            if (registracija == null)
            {
                TempData["Error"] = "Niste prijavljeni na ovaj turnir.";
                return RedirectToAction(nameof(Details), new { id = id });
            }

            _context.Registracije.Remove(registracija);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Uspješno ste se odjavili sa turnira.";
            return RedirectToAction(nameof(Details), new { id = id });
        }

        // POST: /Turnir/DodajPostojecegIgraca
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> DodajPostojecegIgraca(int turnirId, string korisnikId)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Registracije)
                .FirstOrDefaultAsync(t => t.ID == turnirId);

            if (turnir == null) return NotFound();

            var authResult = await _authorizationService.AuthorizeAsync(User, turnir, "OrganizatorIliAdmin");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            if (turnir.Status != StatusTurnira.Planiran)
            {
                TempData["Error"] = "Igrači se mogu dodavati samo na turnire koji su u fazi planiranja.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            if (turnir.Registracije.Count >= turnir.MaxIgraca)
            {
                TempData["Error"] = "Turnir je već popunjen.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            if (turnir.Registracije.Any(r => r.KorisnikID == korisnikId))
            {
                TempData["Error"] = "Igrač je već prijavljen na ovaj turnir.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            var registracija = new Registracija
            {
                TurnirID = turnirId,
                KorisnikID = korisnikId,
                DatumRegistracije = DateTime.Now,
                Odobren = true
            };

            _context.Registracije.Add(registracija);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = turnirId });
        }

        // POST: /Turnir/DodajViseIgraca
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> DodajViseIgraca(int turnirId, List<string> korisnikIds)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Registracije)
                .FirstOrDefaultAsync(t => t.ID == turnirId);

            if (turnir == null) return NotFound();

            var authResult = await _authorizationService.AuthorizeAsync(User, turnir, "OrganizatorIliAdmin");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            if (turnir.Status != StatusTurnira.Planiran)
            {
                TempData["Error"] = "Igrači se mogu dodavati samo na turnire koji su u fazi planiranja.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            if (korisnikIds == null || !korisnikIds.Any())
            {
                TempData["Error"] = "Morate označiti bar jednog igrača za prijavu.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            var maxIgraca = turnir.MaxIgraca;
            var trenutnoPrijavljenih = turnir.Registracije.Count;
            var slobodnaMjesta = maxIgraca - trenutnoPrijavljenih;

            if (slobodnaMjesta <= 0)
            {
                TempData["Error"] = $"Turnir je već popunjen! Maksimalan kapacitet turnira je {maxIgraca} igrača.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            // Filtriraj samo one koji već nisu prijavljeni
            var vecPrijavljeniIds = turnir.Registracije.Select(r => r.KorisnikID).ToHashSet();
            var noviZaPrijavu = korisnikIds.Where(id => !vecPrijavljeniIds.Contains(id)).ToList();

            if (!noviZaPrijavu.Any())
            {
                TempData["Error"] = "Svi označeni igrači su već prijavljeni na ovaj turnir.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            int zatrazenoNovi = noviZaPrijavu.Count;
            var korisniciZaDodavanje = noviZaPrijavu.Take(slobodnaMjesta).ToList();

            var novaRegistracije = korisniciZaDodavanje.Select(kId => new Registracija
            {
                TurnirID = turnirId,
                KorisnikID = kId,
                DatumRegistracije = DateTime.Now,
                Odobren = true
            }).ToList();

            _context.Registracije.AddRange(novaRegistracije);
            await _context.SaveChangesAsync();

            int brojPrijavljenih = novaRegistracije.Count;
            int preskoceni = zatrazenoNovi - brojPrijavljenih;

            if (zatrazenoNovi > slobodnaMjesta || preskoceni > 0)
            {
                TempData["Warning"] = $"Pokušali ste dodati {zatrazenoNovi} novih igrača na turnir sa {slobodnaMjesta} slobodnih mjesta (maksimalan kapacitet: {maxIgraca}). Uspješno je dodano prvih {brojPrijavljenih} igrača, dok je {preskoceni} igrač(a) preskočeno!";
            }
            else
            {
                TempData["Success"] = $"Uspješno je prijavljeno {brojPrijavljenih} igrača na turnir!";
            }

            return RedirectToAction(nameof(Details), new { id = turnirId });
        }

        // POST: /Turnir/DodajNovogGosta
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> DodajNovogGosta(int turnirId, string ime, string prezime, string grad)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Registracije)
                .FirstOrDefaultAsync(t => t.ID == turnirId);

            if (turnir == null) return NotFound();

            var authResult = await _authorizationService.AuthorizeAsync(User, turnir, "OrganizatorIliAdmin");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            if (turnir.Status != StatusTurnira.Planiran)
            {
                TempData["Error"] = "Igrači se mogu dodavati samo na turnire koji su u fazi planiranja.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            if (turnir.Registracije.Count >= turnir.MaxIgraca)
            {
                TempData["Error"] = "Turnir je već popunjen.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            if (string.IsNullOrWhiteSpace(ime) || string.IsNullOrWhiteSpace(prezime))
            {
                TempData["Error"] = "Ime i prezime gosta su obavezni.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            // Kreiranje novog gostujućeg korisnika u bazi
            var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var gostUser = new ApplicationUser
            {
                UserName = $"gost_{uniqueId}",
                Email = $"gost_{uniqueId}@epinpong.local",
                EmailConfirmed = true,
                Ime = ime.Trim(),
                Prezime = prezime.Trim(),
                Grad = string.IsNullOrWhiteSpace(grad) ? "Nepoznato" : grad.Trim(),
                DatumRodjenja = new DateTime(1970, 1, 1),
                DatumRegistracije = DateTime.Now
            };

            var result = await _userManager.CreateAsync(gostUser);
            if (!result.Succeeded)
            {
                TempData["Error"] = "Došlo je do greške prilikom kreiranja gosta: " + string.Join(", ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            // Dodaj u ulogu Korisnik
            await _userManager.AddToRoleAsync(gostUser, "Korisnik");

            // Registruj na turnir
            var registracija = new Registracija
            {
                TurnirID = turnirId,
                KorisnikID = gostUser.Id,
                DatumRegistracije = DateTime.Now,
                Odobren = true
            };

            _context.Registracije.Add(registracija);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Gost {gostUser.Ime} {gostUser.Prezime} je uspješno kreiran i dodan na turnir!";
            return RedirectToAction(nameof(Details), new { id = turnirId });
        }

        // POST: /Turnir/UkloniIgraca
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> UkloniIgraca(int turnirId, string korisnikId)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Registracije)
                .FirstOrDefaultAsync(t => t.ID == turnirId);

            if (turnir == null) return NotFound();

            var authResult = await _authorizationService.AuthorizeAsync(User, turnir, "OrganizatorIliAdmin");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            if (turnir.Status != StatusTurnira.Planiran)
            {
                TempData["Error"] = "Igrači se mogu uklanjati samo sa turnira koji su u fazi planiranja.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            var registracija = turnir.Registracije.FirstOrDefault(r => r.KorisnikID == korisnikId);
            if (registracija != null)
            {
                _context.Registracije.Remove(registracija);
                await _context.SaveChangesAsync();
            }
            else
            {
                TempData["Error"] = "Igrač nije registrovan na ovom turniru.";
            }

            return RedirectToAction(nameof(Details), new { id = turnirId });
        }

        // POST: /Turnir/UkloniViseIgraca
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> UkloniViseIgraca(int turnirId, List<string> korisnikIds)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Registracije)
                .Include(t => t.Mecevi)
                .FirstOrDefaultAsync(t => t.ID == turnirId);

            if (turnir == null) return NotFound();

            var authResult = await _authorizationService.AuthorizeAsync(User, turnir, "OrganizatorIliAdmin");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            if (turnir.Status != StatusTurnira.Planiran || turnir.Mecevi.Any())
            {
                TempData["Error"] = "Igrači se mogu uklanjati samo sa turnira koji su u fazi planiranja.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            if (korisnikIds == null || !korisnikIds.Any())
            {
                TempData["Error"] = "Morate označiti bar jednog igrača za odjavu.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            var registracijeZaUklanjanje = turnir.Registracije
                .Where(r => korisnikIds.Contains(r.KorisnikID))
                .ToList();

            if (registracijeZaUklanjanje.Any())
            {
                int brojUklonjenih = registracijeZaUklanjanje.Count;
                _context.Registracije.RemoveRange(registracijeZaUklanjanje);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Uspješno je odjavljeno {brojUklonjenih} igrača sa turnira!";
            }
            else
            {
                TempData["Error"] = "Nijedan od označenih igrača nije pronađen na ovom turniru.";
            }

            return RedirectToAction(nameof(Details), new { id = turnirId });
        }

        // GET: /Turnir/DohvatiRankinge?sourceType=Liga&sourceId=3
        [HttpGet]
        public async Task<IActionResult> DohvatiRankinge(string sourceType, int sourceId)
        {
            var standings = new Dictionary<string, int>();

            if (sourceType == "Liga")
            {
                var liga = await _context.Lige
                    .Include(l => l.Turniri)
                        .ThenInclude(t => t.Registracije)
                            .ThenInclude(r => r.Korisnik)
                    .Include(l => l.Turniri)
                        .ThenInclude(t => t.Mecevi)
                            .ThenInclude(m => m.Igrac1)
                    .Include(l => l.Turniri)
                        .ThenInclude(t => t.Mecevi)
                            .ThenInclude(m => m.Igrac2)
                    .FirstOrDefaultAsync(l => l.ID == sourceId);

                if (liga != null)
                {
                    var finishedTournaments = liga.Turniri.Where(t => t.Status == StatusTurnira.Zavrsen).ToList();
                    foreach (var turnir in finishedTournaments)
                    {
                        var turnirSaPodacima = await _context.Turniri
                            .Include(t => t.Registracije)
                                .ThenInclude(r => r.Korisnik)
                            .Include(t => t.Mecevi)
                                .ThenInclude(m => m.Igrac1)
                            .Include(t => t.Mecevi)
                                .ThenInclude(m => m.Igrac2)
                            .FirstOrDefaultAsync(t => t.ID == turnir.ID);

                        if (turnirSaPodacima != null)
                        {
                            var plasmani = _bracketService.IzracunajPlasman(turnirSaPodacima);
                            foreach (var pl in plasmani)
                            {
                                if (standings.ContainsKey(pl.KorisnikId))
                                    standings[pl.KorisnikId] += pl.Bodovi;
                                else
                                    standings[pl.KorisnikId] = pl.Bodovi;
                            }
                        }
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

        // POST: /Turnir/SacuvajSesire
        [HttpPost]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SacuvajSesire(int turnirId, string playerPotsJson)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Registracije)
                .FirstOrDefaultAsync(t => t.ID == turnirId);

            if (turnir == null) return NotFound();

            var authResult = await _authorizationService.AuthorizeAsync(User, turnir, "OrganizatorIliAdmin");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            if (!string.IsNullOrEmpty(playerPotsJson))
            {
                try
                {
                    var playerPots = JsonSerializer.Deserialize<List<PlayerPotDto>>(playerPotsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (playerPots != null)
                    {
                        foreach (var pp in playerPots)
                        {
                            var reg = turnir.Registracije.FirstOrDefault(r => r.KorisnikID == pp.KorisnikId);
                            if (reg != null)
                            {
                                reg.Sesir = pp.Sesir;
                            }
                        }
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception)
                {
                    return BadRequest();
                }
            }

            return Ok();
        }

        private class PlayerPotDto
        {
            public string KorisnikId { get; set; } = string.Empty;
            public int Sesir { get; set; }
        }

        // POST: /Turnir/PrijaviPar
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PrijaviPar(int turnirId, string partnerId)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Registracije)
                .Include(t => t.TurnirParovi)
                .FirstOrDefaultAsync(t => t.ID == turnirId);

            if (turnir == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId == null) return Unauthorized();

            if (turnir.Status != StatusTurnira.UToku && turnir.Status != StatusTurnira.Zavrsen)
            {
                TempData["Error"] = "Prijava parova je moguća samo tokom ili nakon završetka glavnog turnira.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            // Provjera da li su oba igrača prijavljena na turnir
            var isUserRegistered = turnir.Registracije.Any(r => r.KorisnikID == currentUserId);
            var isPartnerRegistered = turnir.Registracije.Any(r => r.KorisnikID == partnerId);

            if (!isUserRegistered || !isPartnerRegistered)
            {
                TempData["Error"] = "Oba igrača moraju biti prijavljena na glavni turnir.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            if (currentUserId == partnerId)
            {
                TempData["Error"] = "Ne možete izabrati sebe za partnera.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            // Provjera da li je neko od njih već u paru
            var isUserPaired = turnir.TurnirParovi.Any(p => p.Igrac1ID == currentUserId || p.Igrac2ID == currentUserId || p.Igrac1ID == partnerId || p.Igrac2ID == partnerId);
            var isPartnerPaired = turnir.TurnirParovi.Any(p => p.Igrac1ID == partnerId || p.Igrac2ID == partnerId || p.Igrac1ID == currentUserId || p.Igrac2ID == currentUserId);

            if (isUserPaired || isPartnerPaired)
            {
                TempData["Error"] = "Jedan od igrača je već prijavljen u nekom paru.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            var noviPar = new TurnirPar
            {
                TurnirID = turnirId,
                Igrac1ID = currentUserId,
                Igrac2ID = partnerId,
                DatumPrijave = DateTime.Now
            };

            _context.TurnirParovi.Add(noviPar);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Uspješno ste prijavili par za turnir parova!";
            return RedirectToAction(nameof(Details), new { id = turnirId });
        }

        // POST: /Turnir/DodajParAdmin
        [HttpPost]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DodajParAdmin(int turnirId, string igrac1Id, string igrac2Id)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Registracije)
                .Include(t => t.TurnirParovi)
                .FirstOrDefaultAsync(t => t.ID == turnirId);

            if (turnir == null) return NotFound();

            var authResult = await _authorizationService.AuthorizeAsync(User, turnir, "OrganizatorIliAdmin");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            if (turnir.Status != StatusTurnira.UToku && turnir.Status != StatusTurnira.Zavrsen)
            {
                TempData["Error"] = "Prijava parova je moguća samo tokom ili nakon završetka glavnog turnira.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            var isIgrac1Registered = turnir.Registracije.Any(r => r.KorisnikID == igrac1Id);
            var isIgrac2Registered = turnir.Registracije.Any(r => r.KorisnikID == igrac2Id);

            if (!isIgrac1Registered || !isIgrac2Registered)
            {
                TempData["Error"] = "Oba igrača moraju biti prijavljena na glavni turnir.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            if (igrac1Id == igrac2Id)
            {
                TempData["Error"] = "Morate izabrati dva različita igrača.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            var isIgrac1Paired = turnir.TurnirParovi.Any(p => p.Igrac1ID == igrac1Id || p.Igrac2ID == igrac1Id || p.Igrac1ID == igrac2Id || p.Igrac2ID == igrac2Id);
            var isIgrac2Paired = turnir.TurnirParovi.Any(p => p.Igrac1ID == igrac2Id || p.Igrac2ID == igrac2Id || p.Igrac1ID == igrac1Id || p.Igrac2ID == igrac1Id);

            if (isIgrac1Paired || isIgrac2Paired)
            {
                TempData["Error"] = "Jedan od igrača je već prijavljen u nekom paru.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            var noviPar = new TurnirPar
            {
                TurnirID = turnirId,
                Igrac1ID = igrac1Id,
                Igrac2ID = igrac2Id,
                DatumPrijave = DateTime.Now
            };

            _context.TurnirParovi.Add(noviPar);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = turnirId });
        }

        // POST: /Turnir/UkloniPar
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UkloniPar(int turnirId, int parId)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Mecevi)
                .FirstOrDefaultAsync(t => t.ID == turnirId);

            if (turnir == null) return NotFound();

            var par = await _context.TurnirParovi.FindAsync(parId);
            if (par == null)
            {
                TempData["Error"] = "Par nije pronađen.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            var userId = _userManager.GetUserId(User);
            bool isOrganizator = turnir.OrganizatorId == userId || User.IsInRole(AppConstants.Roles.Administrator);
            bool isUserInPair = par.Igrac1ID == userId || par.Igrac2ID == userId;

            if (!isOrganizator && !isUserInPair)
            {
                return Forbid();
            }

            // Provjera da li su mečevi parova već generisani
            var imaMeceveParova = turnir.Mecevi.Any(m => m.TipMeca == TipMeca.TurnirParova);
            if (imaMeceveParova)
            {
                TempData["Error"] = "Ne možete ukloniti par jer su mečevi turnira parova već generisani.";
                return RedirectToAction(nameof(Details), new { id = turnirId });
            }

            _context.TurnirParovi.Remove(par);
            await _context.SaveChangesAsync();

            if (isUserInPair && !isOrganizator)
            {
                TempData["Success"] = "Uspješno ste odjavili svoj par sa turnira.";
            }

            return RedirectToAction(nameof(Details), new { id = turnirId });
        }

        private bool TurnirExists(int id)
        {
            return _context.Turniri.Any(e => e.ID == id);
        }
    }
}
