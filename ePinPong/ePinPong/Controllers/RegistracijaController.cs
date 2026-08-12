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
    public class RegistracijaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuthorizationService _authorizationService;
        private readonly ILogger<RegistracijaController> _logger;
        private readonly INotificationService _notificationService;
        private readonly ISeedingPotsService _seedingPotsService;

        public RegistracijaController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAuthorizationService authorizationService,
            ILogger<RegistracijaController> logger,
            INotificationService notificationService,
            ISeedingPotsService seedingPotsService)
        {
            _context = context;
            _userManager = userManager;
            _authorizationService = authorizationService;
            _logger = logger;
            _notificationService = notificationService;
            _seedingPotsService = seedingPotsService;
        }

        // POST: /Registracija/Registracija/5
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
                return RedirectToAction("Details", "Turnir", new { id = turnir.ID });
            }

            if (turnir.Registracije.Any(r => r.KorisnikID == userId))
            {
                TempData["Error"] = "Već ste prijavljeni na ovaj turnir.";
                return RedirectToAction("Details", "Turnir", new { id = turnir.ID });
            }

            if (turnir.Registracije.Count >= turnir.MaxIgraca)
            {
                TempData["Error"] = "Turnir je popunjen.";
                return RedirectToAction("Details", "Turnir", new { id = turnir.ID });
            }

            var registracija = new Registracija
            {
                TurnirID = turnir.ID,
                KorisnikID = userId,
                DatumRegistracije = DateTime.Now,
                Odobren = true
            };

            _context.Registracije.Add(registracija);

            var igrac = await _userManager.FindByIdAsync(userId);
            await _notificationService.ObavijestiKorisnikaAsync(
                turnir.OrganizatorId,
                "Nova prijava na turnir",
                $"Igrač <strong>{igrac?.Ime} {igrac?.Prezime}</strong> se prijavio na vaš turnir <strong>{turnir.Naziv}</strong>.",
                posaljiEmail: false
            );

            await _context.SaveChangesAsync();

            TempData["Success"] = "Uspješno ste se prijavili na turnir!";
            return RedirectToAction("Details", "Turnir", new { id = turnir.ID });
        }

        // POST: /Registracija/Odjava/5
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
                return RedirectToAction("Details", "Turnir", new { id = id });
            }

            var registracija = await _context.Registracije
                .FirstOrDefaultAsync(r => r.TurnirID == id && r.KorisnikID == userId);

            if (registracija == null)
            {
                TempData["Error"] = "Niste prijavljeni na ovaj turnir.";
                return RedirectToAction("Details", "Turnir", new { id = id });
            }

            _context.Registracije.Remove(registracija);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Uspješno ste se odjavili sa turnira.";
            return RedirectToAction("Details", "Turnir", new { id = id });
        }

        // POST: /Registracija/DodajPostojecegIgraca
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> DodajPostojecegIgraca(int turnirId, string korisnikId)
        {
            var (turnir, errorResult) = await UcitajIProveriTurnirZaUpravljanjeIgracima(
                turnirId, false, "Igrači se mogu dodavati samo na turnire koji su u fazi planiranja.");
            if (errorResult != null) return errorResult;

            if (turnir!.Registracije.Count >= turnir.MaxIgraca)
            {
                TempData["Error"] = "Turnir je već popunjen.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            if (turnir.Registracije.Any(r => r.KorisnikID == korisnikId))
            {
                TempData["Error"] = "Igrač je već prijavljen na ovaj turnir.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
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

            return RedirectToAction("Details", "Turnir", new { id = turnirId });
        }

        // POST: /Registracija/DodajViseIgraca
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> DodajViseIgraca(int turnirId, List<string> korisnikIds)
        {
            var (turnir, errorResult) = await UcitajIProveriTurnirZaUpravljanjeIgracima(
                turnirId, false, "Igrači se mogu dodavati samo na turnire koji su u fazi planiranja.");
            if (errorResult != null) return errorResult;

            if (korisnikIds == null || !korisnikIds.Any())
            {
                TempData["Error"] = "Morate označiti bar jednog igrača za prijavu.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            var maxIgraca = turnir.MaxIgraca;
            var trenutnoPrijavljenih = turnir.Registracije.Count;
            var slobodnaMjesta = maxIgraca - trenutnoPrijavljenih;

            if (slobodnaMjesta <= 0)
            {
                TempData["Error"] = $"Turnir je već popunjen! Maksimalan kapacitet turnira je {maxIgraca} igrača.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            var vecPrijavljeniIds = turnir.Registracije.Select(r => r.KorisnikID).ToHashSet();
            var noviZaPrijavu = korisnikIds.Where(id => !vecPrijavljeniIds.Contains(id)).ToList();

            if (!noviZaPrijavu.Any())
            {
                TempData["Error"] = "Svi označeni igrači su već prijavljeni na ovaj turnir.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
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

            return RedirectToAction("Details", "Turnir", new { id = turnirId });
        }

        // POST: /Registracija/DodajNovogGosta
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> DodajNovogGosta(int turnirId, string ime, string prezime, string grad)
        {
            var (turnir, errorResult) = await UcitajIProveriTurnirZaUpravljanjeIgracima(
                turnirId, false, "Igrači se mogu dodavati samo na turnire koji su u fazi planiranja.");
            if (errorResult != null) return errorResult;

            if (turnir!.Registracije.Count >= turnir.MaxIgraca)
            {
                TempData["Error"] = "Turnir je već popunjen.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            if (string.IsNullOrWhiteSpace(ime) || string.IsNullOrWhiteSpace(prezime))
            {
                TempData["Error"] = "Ime i prezime gosta su obavezni.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

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
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            await _userManager.AddToRoleAsync(gostUser, AppConstants.Roles.Korisnik);

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
            return RedirectToAction("Details", "Turnir", new { id = turnirId });
        }

        // POST: /Registracija/UkloniIgraca
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> UkloniIgraca(int turnirId, string korisnikId)
        {
            var (turnir, errorResult) = await UcitajIProveriTurnirZaUpravljanjeIgracima(
                turnirId, false, "Igrači se mogu uklanjati samo sa turnira koji su u fazi planiranja.");
            if (errorResult != null) return errorResult;

            var registracija = turnir!.Registracije.FirstOrDefault(r => r.KorisnikID == korisnikId);
            if (registracija != null)
            {
                _context.Registracije.Remove(registracija);
                await _context.SaveChangesAsync();
            }
            else
            {
                TempData["Error"] = "Igrač nije registrovan na ovom turniru.";
            }

            return RedirectToAction("Details", "Turnir", new { id = turnirId });
        }

        // POST: /Registracija/UkloniViseIgraca
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> UkloniViseIgraca(int turnirId, List<string> korisnikIds)
        {
            var (turnir, errorResult) = await UcitajIProveriTurnirZaUpravljanjeIgracima(
                turnirId, true, "Igrači se mogu uklanjati samo sa turnira koji su u fazi planiranja.");
            if (errorResult != null) return errorResult;

            if (korisnikIds == null || !korisnikIds.Any())
            {
                TempData["Error"] = "Morate označiti bar jednog igrača za odjavu.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
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

            return RedirectToAction("Details", "Turnir", new { id = turnirId });
        }

        // POST: /Registracija/SacuvajSesire
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
            if (!authResult.Succeeded) return Forbid();

            if (!string.IsNullOrEmpty(playerPotsJson))
            {
                bool uspjelo = await _seedingPotsService.PrimijeniSesireAsync(turnir, playerPotsJson);
                if (!uspjelo)
                {
                    _logger.LogWarning("Parsiranje šešira nije uspjelo za turnir {TurnirId}.", turnirId);
                    return BadRequest();
                }
            }

            return Ok();
        }

        private async Task<(Turnir? Turnir, IActionResult? ErrorResult)> UcitajIProveriTurnirZaUpravljanjeIgracima(
            int turnirId, bool provjeriMeceve, string porukaAkoNijeDozvoljeno)
        {
            var query = _context.Turniri.Include(t => t.Registracije).AsQueryable();
            if (provjeriMeceve)
                query = query.Include(t => t.Mecevi);

            var turnir = await query.FirstOrDefaultAsync(t => t.ID == turnirId);
            if (turnir == null)
                return (null, NotFound());

            var authResult = await _authorizationService.AuthorizeAsync(User, turnir, "OrganizatorIliAdmin");
            if (!authResult.Succeeded)
                return (null, Forbid());

            bool nijeDozvoljeno = turnir.Status != StatusTurnira.Planiran || (provjeriMeceve && turnir.Mecevi.Any());
            if (nijeDozvoljeno)
            {
                TempData["Error"] = porukaAkoNijeDozvoljeno;
                return (null, RedirectToAction("Details", "Turnir", new { id = turnirId }));
            }

            return (turnir, null);
        }
    }
}