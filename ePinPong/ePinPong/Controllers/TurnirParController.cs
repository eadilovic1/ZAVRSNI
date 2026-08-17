using ePinPong.Data;
using ePinPong.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ePinPong.Controllers
{
    public class TurnirParController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuthorizationService _authorizationService;

        public TurnirParController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAuthorizationService authorizationService)
        {
            _context = context;
            _userManager = userManager;
            _authorizationService = authorizationService;
        }

        // POST: /TurnirPar/PrijaviPar
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

            if (currentUserId == partnerId)
            {
                TempData["Error"] = "Ne možete izabrati sebe za partnera.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            var (uspjeh, greska) = await ValidirajIKreirajPar(turnir, currentUserId, partnerId);
            if (!uspjeh)
            {
                TempData["Error"] = greska;
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            TempData["Success"] = "Uspješno ste prijavili par za turnir parova!";
            return RedirectToAction("Details", "Turnir", new { id = turnirId });
        }

        // POST: /TurnirPar/DodajParAdmin
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
            if (!authResult.Succeeded) return Forbid();

            if (igrac1Id == igrac2Id)
            {
                TempData["Error"] = "Morate izabrati dva različita igrača.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            var (uspjeh, greska) = await ValidirajIKreirajPar(turnir, igrac1Id, igrac2Id);
            if (!uspjeh)
            {
                TempData["Error"] = greska;
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            return RedirectToAction("Details", "Turnir", new { id = turnirId });
        }

        // POST: /TurnirPar/UkloniPar
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
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            var userId = _userManager.GetUserId(User);
            bool isOrganizator = turnir.OrganizatorId == userId || User.IsInRole(AppConstants.Roles.Administrator);
            bool isUserInPair = par.Igrac1ID == userId || par.Igrac2ID == userId;

            if (!isOrganizator && !isUserInPair)
            {
                return Forbid();
            }

            var imaMeceveParova = turnir.Mecevi.Any(m => m.TipMeca == TipMeca.TurnirParova);
            if (imaMeceveParova)
            {
                TempData["Error"] = "Ne možete ukloniti par jer su mečevi turnira parova već generisani.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            _context.TurnirParovi.Remove(par);
            await _context.SaveChangesAsync();

            if (isUserInPair && !isOrganizator)
            {
                TempData["Success"] = "Uspješno ste odjavili svoj par sa turnira.";
            }

            return RedirectToAction("Details", "Turnir", new { id = turnirId });
        }

        private async Task<(bool Uspjeh, string? Greska)> ValidirajIKreirajPar(Turnir turnir, string igrac1Id, string igrac2Id)
        {
            if (turnir.Status != StatusTurnira.UToku && turnir.Status != StatusTurnira.Zavrsen)
                return (false, "Prijava parova je moguća samo tokom ili nakon završetka glavnog turnira.");

            var isIgrac1Registered = turnir.Registracije.Any(r => r.KorisnikID == igrac1Id);
            var isIgrac2Registered = turnir.Registracije.Any(r => r.KorisnikID == igrac2Id);
            if (!isIgrac1Registered || !isIgrac2Registered)
                return (false, "Oba igrača moraju biti prijavljena na glavni turnir.");

            bool jeNekoVecUParu = turnir.TurnirParovi.Any(p =>
                p.Igrac1ID == igrac1Id || p.Igrac2ID == igrac1Id ||
                p.Igrac1ID == igrac2Id || p.Igrac2ID == igrac2Id);
            if (jeNekoVecUParu)
                return (false, "Jedan od igrača je već prijavljen u nekom paru.");

            var noviPar = new TurnirPar
            {
                TurnirID = turnir.ID,
                Igrac1ID = igrac1Id,
                Igrac2ID = igrac2Id,
                DatumPrijave = DateTime.UtcNow
            };
            _context.TurnirParovi.Add(noviPar);
            await _context.SaveChangesAsync();

            return (true, null);
        }
    }
}