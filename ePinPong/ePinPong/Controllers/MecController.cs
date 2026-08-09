using ePinPong.Data;
using ePinPong.Interfaces;
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
using System.Text.Json;
using System.Threading.Tasks;

namespace ePinPong.Controllers
{
    public class MecController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBracketService _bracketService;
        private readonly IMailService _mailService;
        private readonly ILeagueStandingsService _leagueStandingsService;
        private readonly ITurnirCompletionService _turnirCompletionService;
        private readonly IAuthorizationService _authorizationService;
        private readonly ILogger<MecController> _logger;

        public MecController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IBracketService bracketService,
            IMailService mailService,
            ILeagueStandingsService leagueStandingsService,
            ITurnirCompletionService turnirCompletionService,
            IAuthorizationService authorizationService,
            ILogger<MecController> logger)
        {
            _context = context;
            _userManager = userManager;
            _bracketService = bracketService;
            _mailService = mailService;
            _leagueStandingsService = leagueStandingsService;
            _turnirCompletionService = turnirCompletionService;
            _authorizationService = authorizationService;
            _logger = logger;
        }

        // POST: /Mec/GenerirajBracket/5
        [HttpPost]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> GenerirajBracket(int turnirId, string? playerPotsJson)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Registracije)
                .Include(t => t.Liga)
                .FirstOrDefaultAsync(t => t.ID == turnirId);

            if (turnir == null) return NotFound();

            var authResult = await _authorizationService.AuthorizeAsync(User, turnir, "OrganizatorIliAdmin");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var isMasters = turnir.Liga != null && turnir.Kolo.HasValue && turnir.Kolo.Value == LigaTurnirHelper.GetMastersKolo(turnir.Liga);
            int count = turnir.Registracije.Count;
            if (count < 3)
            {
                TempData["Error"] = "Broj igrača mora biti najmanje 3.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            // Snimi ili auto-generiši šešire
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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Neuspješno parsiranje/snimanje šešira (playerPotsJson) za turnir {TurnirId}, prelazim na automatski raspored.", turnirId);
                    if (!isMasters)
                    {
                        await AutoRasporediSesire(turnir);
                    }
                }
            }
            else if (!isMasters)
            {
                // Ako nema poslanih šešira (npr. direktan klik bez drag/drop-a),
                // provjeri jesu li već raspoređeni. Ako su svi na 1 (default), pokreni automatsku raspodjelu.
                bool sviDefault = turnir.Registracije.All(r => r.Sesir == 1);
                if (sviDefault)
                {
                    await AutoRasporediSesire(turnir);
                }
            }

            // Obriši stare mečeve ako postoje (samo singles mečeve, ne parove!)
            var stariMecevi = _context.Mecevi.Where(m => m.TurnirID == turnirId && m.TipMeca != TipMeca.TurnirParova);
            _context.Mecevi.RemoveRange(stariMecevi);

            var igracIds = turnir.Registracije.Select(r => r.KorisnikID).ToList();
            if (isMasters)
            {
                igracIds = await LigaRankingHelper.GetMastersOrderedParticipantIdsAsync(_context, _bracketService, turnir);
            }

            // Generiši samo grupnu fazu
            var meceviGrupneFaze = _bracketService.GenerirajGrupe(turnir, igracIds, isMasters);

            _context.Mecevi.AddRange(meceviGrupneFaze);
            turnir.Status = StatusTurnira.UToku; // Turnir počinje
            await _context.SaveChangesAsync();

            // Slanje obavještenja svim igračima na turniru
            foreach (var registracija in turnir.Registracije)
            {
                var notifikacija = new Notifikacija
                {
                    KorisnikId = registracija.KorisnikID,
                    Sadrzaj = $"Raspored mečeva za turnir <strong>{turnir.Naziv}</strong> je generisan! Grupna faza je počela.",
                    DatumKreiranja = DateTime.Now,
                    Procitana = false
                };
                _context.Notifikacije.Add(notifikacija);

                var igrac = await _userManager.FindByIdAsync(registracija.KorisnikID);
                if (igrac != null && !string.IsNullOrEmpty(igrac.Email))
                {
                    await _mailService.SendEmailAsync(
                        igrac.Email, 
                        "Počeo turnir na ePinPong!", 
                        $"Zdravo {igrac.Ime},<br><br>Raspored za turnir <b>{turnir.Naziv}</b> je generisan. Turnir počinje grupnom fazom."
                    );
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Grupna faza je uspješno generisana!";
            return RedirectToAction("Details", "Turnir", new { id = turnirId, tab = "groups-tab" });
        }

        // GET: /Mec/UnosRezultata/5
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> UnosRezultata(int id)
        {
            var mec = await _context.Mecevi
                .Include(m => m.Turnir)
                .Include(m => m.Igrac1)
                .Include(m => m.Igrac2)
                .Include(m => m.Igrac1Partner)
                .Include(m => m.Igrac2Partner)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (mec == null) return NotFound();

            var authResult = await _authorizationService.AuthorizeAsync(User, mec, "OrganizatorIliAdmin");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            return View(mec);
        }

        // POST: /Mec/UnosRezultata/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> UnosRezultata(int id, int poeniIgrac1, int poeniIgrac2)
        {
            var mec = await _context.Mecevi
                .Include(m => m.Turnir)
                .Include(m => m.Igrac1)
                .Include(m => m.Igrac2)
                .Include(m => m.Igrac1Partner)
                .Include(m => m.Igrac2Partner)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (mec == null) return NotFound();

            var authResult = await _authorizationService.AuthorizeAsync(User, mec, "OrganizatorIliAdmin");
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            // Validacija da meč ide do tačno 3 osvojena seta
            if (!((poeniIgrac1 == Mec.SETOVA_ZA_POBJEDU && poeniIgrac2 >= 0 && poeniIgrac2 <= Mec.SETOVA_ZA_POBJEDU - 1) || (poeniIgrac2 == Mec.SETOVA_ZA_POBJEDU && poeniIgrac1 >= 0 && poeniIgrac1 <= Mec.SETOVA_ZA_POBJEDU - 1)))
            {
                TempData["Error"] = "Rezultat meča mora biti do 3 dobijena seta (npr. 3:0, 3:1, 3:2).";
                // Preusmjeri na detalje turnira umjesto na posebnu stranicu, 
                // kako bi modal tok na stranici turnira ostao neprekinut
                return RedirectToAction("Details", "Turnir", new { id = mec.TurnirID });
            }

            mec.PoeniIgrac1 = poeniIgrac1;
            mec.PoeniIgrac2 = poeniIgrac2;
            mec.Odigran = true;
            // Propagira pobjednika i gubitnika dalje (za Završnicu, Razigravanje, Utješni i TurnirParova)
            if (mec.TipMeca == TipMeca.Zavrsnica || mec.TipMeca == TipMeca.Razigravanje || mec.TipMeca == TipMeca.Utjesni || mec.TipMeca == TipMeca.TurnirParova)
            {
                string winnerId = poeniIgrac1 == 3 ? mec.Igrac1ID! : mec.Igrac2ID!;
                string loserId  = poeniIgrac1 == 3 ? mec.Igrac2ID! : mec.Igrac1ID!;
                string? winnerPartnerId = poeniIgrac1 == 3 ? mec.Igrac1PartnerID : mec.Igrac2PartnerID;
                string? loserPartnerId  = poeniIgrac1 == 3 ? mec.Igrac2PartnerID : mec.Igrac1PartnerID;

                // Pobjednik ide u sljedeći meč(eve)
                if (!string.IsNullOrEmpty(mec.WinnerNextMatchCode))
                {
                    var destinations = mec.WinnerNextMatchCode.Split(';');
                    foreach (var dest in destinations)
                    {
                        if (string.IsNullOrEmpty(dest)) continue;
                        var parts = dest.Split(':');
                        string targetCode = parts[0];
                        int slot = (parts.Length > 1 && int.TryParse(parts[1], out int s)) ? s : (mec.WinnerNextMatchSlot ?? 1);

                        var sljedeciMec = await _context.Mecevi.FirstOrDefaultAsync(m => m.TurnirID == mec.TurnirID && m.MatchCode == targetCode);
                        if (sljedeciMec != null)
                        {
                            if (slot == 1)
                            {
                                sljedeciMec.Igrac1ID = winnerId;
                                sljedeciMec.Igrac1PartnerID = winnerPartnerId;
                            }
                            else
                            {
                                sljedeciMec.Igrac2ID = winnerId;
                                sljedeciMec.Igrac2PartnerID = winnerPartnerId;
                            }
                        }
                    }
                }

                // Gubitnik ide u razigravanje / sljedeći meč(eve)
                if (!string.IsNullOrEmpty(mec.LoserNextMatchCode))
                {
                    var destinations = mec.LoserNextMatchCode.Split(';');
                    foreach (var dest in destinations)
                    {
                        if (string.IsNullOrEmpty(dest)) continue;
                        var parts = dest.Split(':');
                        string targetCode = parts[0];
                        int slot = (parts.Length > 1 && int.TryParse(parts[1], out int s)) ? s : (mec.LoserNextMatchSlot ?? 1);

                        var sljedeciMec = await _context.Mecevi.FirstOrDefaultAsync(m => m.TurnirID == mec.TurnirID && m.MatchCode == targetCode);
                        if (sljedeciMec != null)
                        {
                            if (slot == 1)
                            {
                                sljedeciMec.Igrac1ID = loserId;
                                sljedeciMec.Igrac1PartnerID = loserPartnerId;
                            }
                            else
                            {
                                sljedeciMec.Igrac2ID = loserId;
                                sljedeciMec.Igrac2PartnerID = loserPartnerId;
                            }
                        }
                    }
                }

                // Provjeri: ako su svi mečevi neke runde gotovi (glavni ili utješni turnir), generiši odgovarajući plasman/razigravanje
                if (mec.TipMeca == TipMeca.Zavrsnica || mec.TipMeca == TipMeca.Utjesni || mec.TipMeca == TipMeca.Razigravanje)
                {
                    await ProvjeriIGenerirajRazigravanja(mec.TurnirID);
                }

                // Propagiraj BYE (Slobodan) prolaze za sve mečeve koji su dobili protivnika
                var sviMeceviTurnira = await _context.Mecevi.Where(m => m.TurnirID == mec.TurnirID).ToListAsync();
                _bracketService.PropagirajBye(sviMeceviTurnira);
            }

            await _context.SaveChangesAsync();

            // POSALJI OBAVJEŠTENJE IGRAČIMA (in-app i email)
            var obavjestenjeTekst = $"Uneseni su rezultati meča između <strong>{mec.Igrac1?.Ime}</strong> i <strong>{mec.Igrac2?.Ime}</strong>: {poeniIgrac1} - {poeniIgrac2}.";
            
            if (mec.Igrac1ID != null)
            {
                _context.Notifikacije.Add(new Notifikacija
                {
                    KorisnikId = mec.Igrac1ID,
                    Sadrzaj = obavjestenjeTekst,
                    DatumKreiranja = DateTime.Now
                });

                if (mec.Igrac1 != null && !string.IsNullOrEmpty(mec.Igrac1.Email))
                {
                    await _mailService.SendEmailAsync(mec.Igrac1.Email, "Novi rezultati meča", $"Zdravo {mec.Igrac1.Ime},<br><br>Organizator je unio rezultat vašeg meča: {mec.Igrac1.Ime} {poeniIgrac1} : {poeniIgrac2} {mec.Igrac2?.Ime}.<br>Posjetite ePinPong.");
                }
            }

            if (mec.Igrac2ID != null)
            {
                _context.Notifikacije.Add(new Notifikacija
                {
                    KorisnikId = mec.Igrac2ID,
                    Sadrzaj = obavjestenjeTekst,
                    DatumKreiranja = DateTime.Now
                });

                if (mec.Igrac2 != null && !string.IsNullOrEmpty(mec.Igrac2.Email))
                {
                    await _mailService.SendEmailAsync(mec.Igrac2.Email, "Novi rezultati meča", $"Zdravo {mec.Igrac2.Ime},<br><br>Organizator je unio rezultat vašeg meča: {mec.Igrac1?.Ime} {poeniIgrac1} : {poeniIgrac2} {mec.Igrac2.Ime}.<br>Posjetite ePinPong.");
                }
            }

            await _context.SaveChangesAsync();

            // Provjera da li su svi mečevi odigrani - ako jesu, zatvori turnir i proglasi pobjednika
            if (mec.TipMeca != TipMeca.TurnirParova)
            {
                var turnir = await _context.Turniri
                    .Include(t => t.Liga)
                    .Include(t => t.Mecevi)
                    .FirstOrDefaultAsync(t => t.ID == mec.TurnirID);

                if (turnir != null)
                {
                    if (_turnirCompletionService.EvaluateAndCloseIfFinished(turnir))
                    {
                        await _context.SaveChangesAsync();
                    }
                }
            }

            return RedirectToAction("Details", "Turnir", new { id = mec.TurnirID });
        }

        // POST: /Mec/GenerirajPlasman
        [HttpPost]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> GenerirajPlasman(int turnirId, int plL, int plR)
        {
            var turnir = await _context.Turniri
                .Include(t => t.Registracije)
                .FirstOrDefaultAsync(t => t.ID == turnirId);

            if (turnir == null) return NotFound();

            var authResult = await _authorizationService.AuthorizeAsync(User, turnir, "OrganizatorIliAdmin");
            if (!authResult.Succeeded)
                return Forbid();

            var sviMecevi = await _context.Mecevi.Where(m => m.TurnirID == turnirId).ToListAsync();

            // Provjeri da faza još nije generisana
            string prefiks = $"PL_{plL}_{plR}_R1_M";
            if (sviMecevi.Any(m => m.MatchCode.StartsWith(prefiks)))
            {
                TempData["Error"] = $"Razigravanje za mjesta {plL}–{plR} je već generisano.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            // Prikupi gubitnike iz odgovarajuće runde Z_ mečeva
            int ukupnoIgraca = plR - plL + 1; // koliko treba gubitnika

            var zMecevi = sviMecevi.Where(m => m.TipMeca == TipMeca.Zavrsnica && m.MatchCode.StartsWith("Z_")).ToList();
            var zPoRundama = zMecevi.GroupBy(m => m.Runda).OrderBy(g => g.Key).ToList();

            // Nađi rundu koja ima točno ukupnoIgraca mečeva
            var ciljnaRunda = zPoRundama.FirstOrDefault(g => g.Count() == ukupnoIgraca);
            if (ciljnaRunda == null || !ciljnaRunda.All(m => m.Odigran))
            {
                TempData["Error"] = $"Svi mečevi odgovarajuće runde moraju biti odigrani prije generisanja razigravanja za mjesta {plL}–{plR}.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            // Izvuci gubitnike
            var gubitnici = new List<string?>();
            foreach (var zm in ciljnaRunda)
            {
                string? loserId = null;
                if (zm.Igrac1ID != null && zm.Igrac2ID != null)
                {
                    loserId = (zm.PoeniIgrac1 ?? 0) >= 3 ? zm.Igrac2ID : zm.Igrac1ID;
                }
                loserId ??= BracketService.SLOBODAN;
                gubitnici.Add(loserId);
            }

            if (gubitnici.Count != ukupnoIgraca)
            {
                TempData["Error"] = $"Nije moguće odrediti gubitnike za razigravanje {plL}–{plR}. Provjeri odigrane mečeve.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            await DbSeeder.EnsureSlobodanUserExistsAsync(_context);
            var noviMecevi = _bracketService.GenerirajPlasmanFazu(turnir, plL, plR, gubitnici, sviMecevi);
            if (noviMecevi.Any())
            {
                _context.Mecevi.AddRange(noviMecevi);
                await _context.SaveChangesAsync();
            }
            else
            {
                TempData["Error"] = "Došlo je do greške pri generisanju razigravanja.";
            }

            return RedirectToAction("Details", "Turnir", new { id = turnirId });
        }

        private async Task ProvjeriIGenerirajRazigravanja(int turnirId)
        {
            var turnir = await _context.Turniri.FindAsync(turnirId);
            if (turnir == null || turnir.SistemTurnira == SistemTurnira.SingleElimination) return;

            var sviMecevi = await _context.Mecevi.Where(m => m.TurnirID == turnirId).ToListAsync();
            
            // 1. Provjeri i generiši razigravanja za glavnu završnicu (Z_ i PL_)
            var zMecevi = sviMecevi.Where(m => (m.TipMeca == TipMeca.Zavrsnica || m.TipMeca == TipMeca.Razigravanje) && (m.MatchCode.StartsWith("Z_") || m.MatchCode.StartsWith("PL_"))).ToList();
            await GenerisiRazigravanjaZaSkupinu(turnir, sviMecevi, zMecevi, isUtjesni: false);

            // 2. Provjeri i generiši razigravanja za utješni turnir (UT_R i UT_PL_)
            if (turnir.SistemTurnira == SistemTurnira.DoubleEliminationUtjesni)
            {
                var utMecevi = sviMecevi.Where(m => m.TipMeca == TipMeca.Utjesni && (m.MatchCode.StartsWith("UT_PL_") || (m.MatchCode.StartsWith("UT_R") && !m.MatchCode.StartsWith("UT_RR_")))).ToList();
                await GenerisiRazigravanjaZaSkupinu(turnir, sviMecevi, utMecevi, isUtjesni: true);
            }

            await _context.SaveChangesAsync();
        }

        private async Task GenerisiRazigravanjaZaSkupinu(Turnir turnir, List<Mec> sviMecevi, List<Mec> meceviSkupine, bool isUtjesni)
        {
            var poRangeu = meceviSkupine.GroupBy(m => m.PlacingRange).ToList();

            foreach (var rangeGroup in poRangeu)
            {
                int groupL = 1;
                if (!string.IsNullOrEmpty(rangeGroup.Key))
                {
                    var parts = rangeGroup.Key.Split('-');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int gl))
                    {
                        groupL = gl;
                    }
                }

                var poRundama = rangeGroup.GroupBy(m => m.Runda).OrderBy(g => g.Key).ToList();
                foreach (var rundaGroup in poRundama)
                {
                    var meceviRunde = rundaGroup.ToList();
                    int M = meceviRunde.Count;
                    if (M <= 1) continue; // Finale ili meč sa 1 utakmicom

                    int relL = M + 1;
                    int relR = 2 * M;

                    int L = (groupL - 1) + relL;
                    int R = (groupL - 1) + relR;

                    if (!meceviRunde.All(m => m.Odigran)) continue;

                    string codePrefix = isUtjesni ? "UT_PL" : "PL";
                    string searchPrefix = $"{codePrefix}_{L}_{R}_R1_M";
                    if (sviMecevi.Any(m => m.MatchCode.StartsWith(searchPrefix))) continue;

                    var gubitnici = new List<string?>();
                    foreach (var zm in meceviRunde)
                    {
                        string? loserId = null;
                        if (zm.Igrac1ID != null && zm.Igrac2ID != null)
                        {
                            loserId = (zm.PoeniIgrac1 ?? 0) >= 3 ? zm.Igrac2ID : zm.Igrac1ID;
                        }
                        loserId ??= BracketService.SLOBODAN;
                        gubitnici.Add(loserId);
                    }

                    if (gubitnici.Count != M) continue;

                    await DbSeeder.EnsureSlobodanUserExistsAsync(_context);
                    var noviMecevi = _bracketService.GenerirajPlasmanFazu(turnir, L, R, gubitnici, sviMecevi, isUtjesni);
                    if (noviMecevi.Any())
                    {
                        _context.Mecevi.AddRange(noviMecevi);
                        sviMecevi.AddRange(noviMecevi);
                    }
                }
            }
        }

        // POST: /Mec/GenerirajZavrsnicu
        [HttpPost]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> GenerirajZavrsnicu(int turnirId)
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

            var sviMecevi = await _context.Mecevi.Where(m => m.TurnirID == turnirId).ToListAsync();
            var grupniMecevi = sviMecevi.Where(m => m.TipMeca == TipMeca.GrupnaFaza).ToList();
            var imaZavrsnicu = sviMecevi.Any(m => m.TipMeca == TipMeca.Zavrsnica);

            if (!grupniMecevi.All(m => m.Odigran))
            {
                TempData["Error"] = "Svi mečevi grupne faze moraju biti odigrani prije generisanja završnice.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            if (imaZavrsnicu)
            {
                TempData["Error"] = "Završnica je već generisana.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            await DbSeeder.EnsureSlobodanUserExistsAsync(_context);
            var meceviZavrsnice = _bracketService.GenerirajZavrsnicu(turnir, grupniMecevi);
            if (meceviZavrsnice.Any())
            {
                _context.Mecevi.AddRange(meceviZavrsnice);
                await _context.SaveChangesAsync();
                await ProvjeriIGenerirajRazigravanja(turnirId);
            }
            else
            {
                TempData["Error"] = "Došlo je do greške prilikom generisanja parova završnice.";
            }

            return RedirectToAction("Details", "Turnir", new { id = turnirId });
        }


        private async Task AutoRasporediSesire(Turnir turnir)
        {
            var registrations = turnir.Registracije.ToList();
            int N = registrations.Count;
            if (N < 3) return;

            var points = await _leagueStandingsService.GetPlayerPointsAsync(turnir);

            var sortedRegs = registrations
                .OrderByDescending(r => points.ContainsKey(r.KorisnikID) ? points[r.KorisnikID] : 0)
                .ThenBy(r => r.DatumRegistracije)
                .ToList();

            int x = 0;
            int y = 0;
            if (N == 5)
            {
                x = 1;
                y = 0;
            }
            else
            {
                for (int candX = N / 4; candX >= 0; candX--)
                {
                    int ostalo = N - (candX * 4);
                    if (ostalo % 3 == 0)
                    {
                        x = candX;
                        y = ostalo / 3;
                        break;
                    }
                }
            }
            int G = (N == 5) ? 1 : (x + y);

            for (int i = 0; i < N; i++)
            {
                if (i < G)
                    sortedRegs[i].Sesir = 1;
                else if (i < 2 * G)
                    sortedRegs[i].Sesir = 2;
                else if (i < 3 * G)
                    sortedRegs[i].Sesir = 3;
                else
                    sortedRegs[i].Sesir = 4;
            }
            await _context.SaveChangesAsync();
        }

        // POST: /Mec/GenerirajTurnirParova/5
        [HttpPost]
        [Authorize(Roles = AppConstants.Roles.AdministratorOrOrganizator)]
        public async Task<IActionResult> GenerirajTurnirParova(int turnirId)
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

            if (turnir.Status != StatusTurnira.Zavrsen)
            {
                TempData["Error"] = "Mečevi parova se mogu generisati tek po završetku glavnog turnira.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            int count = turnir.TurnirParovi.Count;
            if (count < 2)
            {
                TempData["Error"] = "Za generisanje turnira parova potrebna su najmanje 2 para.";
                return RedirectToAction("Details", "Turnir", new { id = turnirId });
            }

            // Obriši stare mečeve parova ako postoje
            var stariMeceviParova = _context.Mecevi.Where(m => m.TurnirID == turnirId && m.TipMeca == TipMeca.TurnirParova);
            _context.Mecevi.RemoveRange(stariMeceviParova);

            // Generiši turnir parova
            await DbSeeder.EnsureSlobodanUserExistsAsync(_context);
            var meceviParova = _bracketService.GenerirajTurnirParova(turnir, turnir.TurnirParovi.ToList());

            _context.Mecevi.AddRange(meceviParova);
            await _context.SaveChangesAsync();

            // Slanje obavještenja igračima koji su prijavljeni u parovima
            var registrovaniKorisnikIdsInPairs = turnir.TurnirParovi.SelectMany(p => new[] { p.Igrac1ID, p.Igrac2ID }).Distinct().ToList();
            foreach (var igracId in registrovaniKorisnikIdsInPairs)
            {
                var notifikacija = new Notifikacija
                {
                    KorisnikId = igracId,
                    Sadrzaj = $"Raspored za <strong>Turnir Parova</strong> u sklopu turnira <strong>{turnir.Naziv}</strong> je generisan!",
                    DatumKreiranja = DateTime.Now,
                    Procitana = false
                };
                _context.Notifikacije.Add(notifikacija);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction("Details", "Turnir", new { id = turnirId, tab = "doubles-tab" });
        }
    }
}
