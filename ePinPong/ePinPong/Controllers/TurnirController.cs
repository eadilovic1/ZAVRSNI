using ePinPong.Data;
using ePinPong.Interfaces;
using ePinPong.Models;
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

        public TurnirController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IMailService mailService, IBracketService bracketService)
        {
            _context = context;
            _userManager = userManager;
            _mailService = mailService;
            _bracketService = bracketService;
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
            ViewBag.IsRegistered = turnir.Registracije.Any(r => r.KorisnikID == userId);
            ViewBag.CurrentUserId = userId;
            var isOrganizator = turnir.OrganizatorId == userId || User.IsInRole("Administrator");
            ViewBag.IsOrganizator = isOrganizator;

            // Auto-riješi eventualne zapete BYE mečeve (npr. Slobodan vs pravi igrač u razigravanju)
            var meceviList = turnir.Mecevi.ToList();
            _bracketService.PropagirajBye(meceviList);
            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync();
            }

            ViewBag.Ranking = _bracketService.IzracunajPlasman(turnir);

            // Učitavanje bodova za seeding
            ViewBag.PlayerPoints = await GetLeaguePointsForTurnirAsync(turnir);

            if (isOrganizator && turnir.Status == StatusTurnira.Planiran)
            {
                var registrovaniKorisnikIds = turnir.Registracije.Select(r => r.KorisnikID).ToList();
                var slobodniKorisnici = await _userManager.Users
                    .Where(u => u.Id != "SLOBODAN" && !registrovaniKorisnikIds.Contains(u.Id))
                    .ToListAsync();

                ViewBag.SlobodniKorisnici = slobodniKorisnici.Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = $"{(u.IsGost ? "[Gost] " : "")}{u.Ime} {u.Prezime} ({u.Grad})"
                }).ToList();

                // Lige za seeding
                var sveLige = await _context.Lige.ToListAsync();
                ViewBag.LigeZaSeeding = sveLige.Select(l => new SelectListItem
                {
                    Value = l.ID.ToString(),
                    Text = l.Naziv
                }).ToList();

                // Završeni turniri za seeding
                var zavrseniTurniri = await _context.Turniri
                    .Where(t => t.Status == StatusTurnira.Zavrsen)
                    .ToListAsync();
                ViewBag.TurniriZaSeeding = zavrseniTurniri.Select(t => new SelectListItem
                {
                    Value = t.ID.ToString(),
                    Text = t.Naziv
                }).ToList();
            }

            return View(turnir);
        }

        // GET: /Turnir/Create
        [Authorize(Roles = "Administrator,Organizator")]
        public IActionResult Create(int? ligaId = null)
        {
            var lige = _context.Lige.ToList();
            var ligeSelectList = lige.Select(l => new SelectListItem { Value = l.ID.ToString(), Text = l.Naziv }).ToList();
            ligeSelectList.Insert(0, new SelectListItem { Value = "", Text = "Ne pripada nijednoj ligi (Samostalni turnir)" });
            ViewBag.Lige = ligeSelectList;

            // Defaultni datumi: posljednja nedjelja u tekućem mjesecu
            var now = DateTime.Now;
            var lastDay = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month));
            int dayOfWeek = (int)lastDay.DayOfWeek; // Sunday = 0
            var lastSunday = lastDay.AddDays(-dayOfWeek);

            var model = new Turnir
            {
                DatumPocetka = lastSunday.Date.AddHours(9).AddMinutes(30),
                DatumKraja   = lastSunday.Date.AddHours(15)
            };

            if (ligaId.HasValue)
            {
                model.LigaID = ligaId;
                ViewBag.LockedLigaId = ligaId.Value;
                var liga = _context.Lige.Find(ligaId.Value);
                ViewBag.LockedLigaNaziv = liga?.Naziv ?? "Odabrana liga";
            }

            return View(model);
        }

        // POST: /Turnir/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Organizator")]
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

            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(turnir.SlikaUrl))
                {
                    turnir.SlikaUrl = "https://images.unsplash.com/photo-1534158914592-062992fbe900?q=80&w=1200&auto=format&fit=crop";
                }
                turnir.Status = StatusTurnira.Planiran;
                _context.Add(turnir);
                await _context.SaveChangesAsync();

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
                return RedirectToAction("Index", "Home");
            }

            var sveLige = _context.Lige.ToList();
            var sveLigeSelectList = sveLige.Select(l => new SelectListItem { Value = l.ID.ToString(), Text = l.Naziv }).ToList();
            sveLigeSelectList.Insert(0, new SelectListItem { Value = "", Text = "Ne pripada nijednoj ligi (Samostalni turnir)" });
            ViewBag.Lige = sveLigeSelectList;

            return View(turnir);
        }

        // GET: /Turnir/Edit/5
        [Authorize(Roles = "Administrator,Organizator")]
        public async Task<IActionResult> Edit(int id)
        {
            var turnir = await _context.Turniri.FindAsync(id);
            if (turnir == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            if (turnir.OrganizatorId != userId && !User.IsInRole("Administrator"))
            {
                return Forbid();
            }

            ViewBag.StatusList = Enum.GetValues(typeof(StatusTurnira))
                .Cast<StatusTurnira>()
                .Select(s => new SelectListItem { Value = s.ToString(), Text = s.ToString(), Selected = turnir.Status == s });

            var lige = await _context.Lige.ToListAsync();
            var ligeSelectList = lige.Select(l => new SelectListItem { Value = l.ID.ToString(), Text = l.Naziv, Selected = turnir.LigaID == l.ID }).ToList();
            ligeSelectList.Insert(0, new SelectListItem { Value = "", Text = "Ne pripada nijednoj ligi (Samostalni turnir)" });
            ViewBag.Lige = ligeSelectList;

            return View(turnir);
        }

        // POST: /Turnir/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Organizator")]
        public async Task<IActionResult> Edit(int id, Turnir turnir)
        {
            if (id != turnir.ID)
            {
                return NotFound();
            }

            var postojeci = await _context.Turniri.AsNoTracking().FirstOrDefaultAsync(t => t.ID == id);
            if (postojeci == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (postojeci.OrganizatorId != userId && !User.IsInRole("Administrator"))
            {
                return Forbid();
            }

            // Očuvaj organizatora
            turnir.OrganizatorId = postojeci.OrganizatorId;

            ModelState.Remove("Organizator");
            ModelState.Remove("OrganizatorId");
            ModelState.Remove("Liga");

            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(turnir.SlikaUrl))
                {
                    turnir.SlikaUrl = "https://images.unsplash.com/photo-1534158914592-062992fbe900?q=80&w=1200&auto=format&fit=crop";
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

            ViewBag.StatusList = Enum.GetValues(typeof(StatusTurnira))
                .Cast<StatusTurnira>()
                .Select(s => new SelectListItem { Value = s.ToString(), Text = s.ToString(), Selected = turnir.Status == s });

            var sveLige = await _context.Lige.ToListAsync();
            var sveLigeSelectList = sveLige.Select(l => new SelectListItem { Value = l.ID.ToString(), Text = l.Naziv, Selected = turnir.LigaID == l.ID }).ToList();
            sveLigeSelectList.Insert(0, new SelectListItem { Value = "", Text = "Ne pripada nijednoj ligi (Samostalni turnir)" });
            ViewBag.Lige = sveLigeSelectList;

            return View(turnir);
        }

        // POST: /Turnir/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var turnir = await _context.Turniri.FindAsync(id);
            if (turnir != null)
            {
                _context.Turniri.Remove(turnir);
                await _context.SaveChangesAsync();
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
        [Authorize(Roles = "Administrator,Organizator")]
        public async Task<IActionResult> DodajPostojecegIgraca(int turnirId, string korisnikId)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Registracije)
                .FirstOrDefaultAsync(t => t.ID == turnirId);

            if (turnir == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (turnir.OrganizatorId != userId && !User.IsInRole("Administrator"))
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

            TempData["Success"] = "Igrač je uspješno dodan na turnir!";
            return RedirectToAction(nameof(Details), new { id = turnirId });
        }

        // POST: /Turnir/DodajNovogGosta
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,Organizator")]
        public async Task<IActionResult> DodajNovogGosta(int turnirId, string ime, string prezime, string grad)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Registracije)
                .FirstOrDefaultAsync(t => t.ID == turnirId);

            if (turnir == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (turnir.OrganizatorId != userId && !User.IsInRole("Administrator"))
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
        [Authorize(Roles = "Administrator,Organizator")]
        public async Task<IActionResult> UkloniIgraca(int turnirId, string korisnikId)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Registracije)
                .FirstOrDefaultAsync(t => t.ID == turnirId);

            if (turnir == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (turnir.OrganizatorId != userId && !User.IsInRole("Administrator"))
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
                TempData["Success"] = "Igrač je uspješno uklonjen sa turnira!";
            }
            else
            {
                TempData["Error"] = "Igrač nije registrovan na ovom turniru.";
            }

            return RedirectToAction(nameof(Details), new { id = turnirId });
        }

        private async Task<Dictionary<string, int>> GetLeaguePointsForTurnirAsync(Turnir turnir)
        {
            var playerPoints = new Dictionary<string, int>();
            foreach (var reg in turnir.Registracije)
            {
                playerPoints[reg.KorisnikID] = 0;
            }

            if (turnir.LigaID != null)
            {
                // Get all completed tournaments in the league
                var finishedTournaments = await _context.Turniri
                    .Where(t => t.LigaID == turnir.LigaID && t.Status == StatusTurnira.Zavrsen && t.ID != turnir.ID)
                    .Include(t => t.Registracije)
                        .ThenInclude(r => r.Korisnik)
                    .Include(t => t.Mecevi)
                        .ThenInclude(m => m.Igrac1)
                    .Include(t => t.Mecevi)
                        .ThenInclude(m => m.Igrac2)
                    .ToListAsync();

                foreach (var ft in finishedTournaments)
                {
                    var plasmani = _bracketService.IzracunajPlasman(ft);
                    foreach (var pl in plasmani)
                    {
                        if (playerPoints.ContainsKey(pl.KorisnikId))
                        {
                            playerPoints[pl.KorisnikId] += pl.Bodovi;
                        }
                    }
                }
            }
            else
            {
                // Standalone: global points across all completed tournaments
                var finishedTournaments = await _context.Turniri
                    .Where(t => t.Status == StatusTurnira.Zavrsen && t.ID != turnir.ID)
                    .Include(t => t.Registracije)
                        .ThenInclude(r => r.Korisnik)
                    .Include(t => t.Mecevi)
                        .ThenInclude(m => m.Igrac1)
                    .Include(t => t.Mecevi)
                        .ThenInclude(m => m.Igrac2)
                    .ToListAsync();

                foreach (var ft in finishedTournaments)
                {
                    var plasmani = _bracketService.IzracunajPlasman(ft);
                    foreach (var pl in plasmani)
                    {
                        if (playerPoints.ContainsKey(pl.KorisnikId))
                        {
                            playerPoints[pl.KorisnikId] += pl.Bodovi;
                        }
                    }
                }
            }

            return playerPoints;
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
        [Authorize(Roles = "Administrator,Organizator")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SacuvajSesire(int turnirId, string playerPotsJson)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Registracije)
                .FirstOrDefaultAsync(t => t.ID == turnirId);

            if (turnir == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (turnir.OrganizatorId != userId && !User.IsInRole("Administrator"))
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
        [Authorize(Roles = "Administrator,Organizator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DodajParAdmin(int turnirId, string igrac1Id, string igrac2Id)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Registracije)
                .Include(t => t.TurnirParovi)
                .FirstOrDefaultAsync(t => t.ID == turnirId);

            if (turnir == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (turnir.OrganizatorId != userId && !User.IsInRole("Administrator"))
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

            TempData["Success"] = "Par je uspješno kreiran i dodan na turnir!";
            return RedirectToAction(nameof(Details), new { id = turnirId });
        }

        // POST: /Turnir/UkloniPar
        [HttpPost]
        [Authorize(Roles = "Administrator,Organizator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UkloniPar(int turnirId, int parId)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Mecevi)
                .FirstOrDefaultAsync(t => t.ID == turnirId);

            if (turnir == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (turnir.OrganizatorId != userId && !User.IsInRole("Administrator"))
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

            var par = await _context.TurnirParovi.FindAsync(parId);
            if (par != null)
            {
                _context.TurnirParovi.Remove(par);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Par je uspješno uklonjen sa turnira.";
            }
            else
            {
                TempData["Error"] = "Par nije pronađen.";
            }

            return RedirectToAction(nameof(Details), new { id = turnirId });
        }

        private bool TurnirExists(int id)
        {
            return _context.Turniri.Any(e => e.ID == id);
        }
    }
}
