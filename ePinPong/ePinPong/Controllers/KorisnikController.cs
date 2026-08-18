using ePinPong.Data;
using ePinPong.Models;
using ePinPong.Models.ViewModels;
using ePinPong.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ePinPong.Controllers
{
    public class KorisnikController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBracketService _bracketService;
        private readonly ILeagueStandingsService _leagueStandingsService;

        public KorisnikController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IBracketService bracketService, ILeagueStandingsService leagueStandingsService)
        {
            _context = context;
            _userManager = userManager;
            _bracketService = bracketService;
            _leagueStandingsService = leagueStandingsService;
        }

        // GET: /Korisnik/Index/some-guid-id
        public async Task<IActionResult> Index(string id)
        {
            if (id == BracketService.SLOBODAN) return NotFound();
            var korisnik = await _userManager.FindByIdAsync(id);
            if (korisnik == null) return NotFound();

            var turniri = await _context.Turniri
                .Where(t => t.OrganizatorId == id)
                .OrderByDescending(t => t.DatumPocetka)
                .ToListAsync();

            var registracije = await _context.Registracije
                .Include(r => r.Turnir)
                    .ThenInclude(t => t!.Registracije)
                .Include(r => r.Turnir)
                    .ThenInclude(t => t!.Mecevi)
                        .ThenInclude(m => m.Igrac1)
                .Include(r => r.Turnir)
                    .ThenInclude(t => t!.Mecevi)
                        .ThenInclude(m => m.Igrac2)
                .Include(r => r.Turnir)
                    .ThenInclude(t => t!.Mecevi)
                        .ThenInclude(m => m.MecKodovi)
                .Where(r => r.KorisnikID == id)
                .OrderByDescending(r => r.DatumRegistracije)
                .ToListAsync();

            var mecevi = await _context.Mecevi
                .Include(m => m.Turnir)
                .Include(m => m.Igrac1)
                .Include(m => m.Igrac2)
                .Where(m => m.Igrac1ID == id || m.Igrac2ID == id)
                .ToListAsync();

            // Sortiranje mečeva: predstojeći (neodigrani) prvo po vremenu uzlazno, pa odigrani po vremenu silazno
            var sortedMecevi = mecevi
                .Where(m => !m.Odigran)
                .OrderBy(m => m.VrijemeMeca)
                .Concat(mecevi.Where(m => m.Odigran).OrderByDescending(m => m.VrijemeMeca))
                .ToList();

            var brojPratilaca = await _context.Pracenja.CountAsync(p => p.PraceniID == id);
            var brojPracenih = await _context.Pracenja.CountAsync(p => p.PratilacID == id);

            var trenutniUserId = _userManager.GetUserId(User);
            var daLiPratim = false;
            if (trenutniUserId != null)
            {
                daLiPratim = await _context.Pracenja
                    .AnyAsync(p => p.PratilacID == trenutniUserId && p.PraceniID == id);
            }

            // Računanje plasmana na završenim turnirima
            var turnirRankings = new Dictionary<int, string>();
            foreach (var reg in registracije)
            {
                if (reg.Turnir != null && reg.Turnir.Status == StatusTurnira.Zavrsen)
                {
                    var plasmani = _bracketService.IzracunajPlasman(reg.Turnir);
                    var mojPlasman = plasmani.FirstOrDefault(p => p.KorisnikId == id);
                    if (mojPlasman != null)
                    {
                        turnirRankings[reg.TurnirID] = $"{mojPlasman.Pozicija}/{reg.Turnir.Registracije.Count}";
                    }
                    else
                    {
                        turnirRankings[reg.TurnirID] = "-";
                    }
                }
            }

            // Računanje plasmana u ligama koje je igrač igrao, igra ili će igrati
            var sveLige = await _context.Lige
                .Include(l => l.Turniri)
                    .ThenInclude(t => t.Registracije)
                .ToListAsync();

            var relevantneLige = sveLige
                .Where(l => l.Turniri.Any(t => t.Registracije.Any(r => r.KorisnikID == id)))
                .ToList();

            var ligeStandings = await _leagueStandingsService.GetPlayersStandingsAsync(relevantneLige, id);

            var viewModel = new KorisnikTurniriViewModel
            {
                Korisnik = korisnik,
                Turniri = turniri,
                Registracije = registracije,
                Mecevi = sortedMecevi,
                BrojPratilaca = brojPratilaca,
                BrojPracenih = brojPracenih,
                DaLiPratim = daLiPratim,
                TurnirRankings = turnirRankings,
                LigeStandings = ligeStandings
            };

            return View(viewModel);
        }
    }
}
