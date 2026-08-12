using ePinPong.Data;
using ePinPong.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Prefix = ePinPong.AppConstants.MatchCodePrefixes;

namespace ePinPong.Services
{
    /// <summary>
    /// SRP — odgovoran isključivo za <b>propagaciju rezultata</b> tokom trajanja turnira:
    /// propagacija pobjednika u sljedeći meč, automatsko generisanje razigravanja
    /// za plasman i provjera BYE prolaza nakon svakog odigranog meča.
    /// Kreiranje mečeva/rundi delegira na <see cref="IBracketGenerationService"/>.
    /// </summary>
    public class BracketPropagationService : IBracketPropagationService
    {
        private readonly ApplicationDbContext    _context;
        private readonly IBracketGenerationService _generation;

        public BracketPropagationService(
            ApplicationDbContext context,
            IBracketGenerationService generation)
        {
            _context    = context;
            _generation = generation;
        }

        /// <inheritdoc/>
        public async Task PropagirajPobjednikaAsync(Mec mec)
        {
            if (mec.TipMeca != TipMeca.Zavrsnica    &&
                mec.TipMeca != TipMeca.Razigravanje &&
                mec.TipMeca != TipMeca.Utjesni      &&
                mec.TipMeca != TipMeca.TurnirParova)
                return;

            int poeniIgrac1 = mec.PoeniIgrac1 ?? 0;
            int poeniIgrac2 = mec.PoeniIgrac2 ?? 0;

            string  winnerId        = poeniIgrac1 == 3 ? mec.Igrac1ID! : mec.Igrac2ID!;
            string  loserId         = poeniIgrac1 == 3 ? mec.Igrac2ID! : mec.Igrac1ID!;
            string? winnerPartnerId = poeniIgrac1 == 3 ? mec.Igrac1PartnerID : mec.Igrac2PartnerID;
            string? loserPartnerId  = poeniIgrac1 == 3 ? mec.Igrac2PartnerID : mec.Igrac1PartnerID;

            // Pobjednik → sljedeći meč(evi)
            await PropagujNaSljedeciMecAsync(mec, mec.WinnerNextMatchCode, mec.WinnerNextMatchSlot,
                winnerId, winnerPartnerId);

            // Gubitnik → razigravanje / sljedeći meč(evi)
            await PropagujNaSljedeciMecAsync(mec, mec.LoserNextMatchCode, mec.LoserNextMatchSlot,
                loserId, loserPartnerId);

            // Provjeri treba li generisati nova razigravanja za plasman
            if (mec.TipMeca == TipMeca.Zavrsnica  ||
                mec.TipMeca == TipMeca.Utjesni    ||
                mec.TipMeca == TipMeca.Razigravanje)
            {
                await ProvjeriIGenerirajRazigravanjaAsync(mec.TurnirID);
            }

            // Propagiraj BYE prolaze za sve mečeve turnira koji su dobili novog igrača
            var sviMecevi = await _context.Mecevi
                .Where(m => m.TurnirID == mec.TurnirID).ToListAsync();
            _generation.PropagirajBye(sviMecevi);
        }

        /// <inheritdoc/>
        public async Task ProvjeriIGenerirajRazigravanjaAsync(int turnirId)
        {
            var turnir = await _context.Turniri.FindAsync(turnirId);
            if (turnir == null || turnir.SistemTurnira == SistemTurnira.SingleElimination) return;

            var sviMecevi = await _context.Mecevi
                .Where(m => m.TurnirID == turnirId).ToListAsync();

            // 1. Razigravanja za glavnu završnicu (Z_ i PL_)
            var zMecevi = sviMecevi.Where(m =>
                (m.TipMeca == TipMeca.Zavrsnica || m.TipMeca == TipMeca.Razigravanje) &&
                (m.MatchCode.StartsWith(Prefix.Zavrsnica) || m.MatchCode.StartsWith(Prefix.Placement)))
                .ToList();
            await GenerisiRazigravanjaZaSkupinuAsync(turnir, sviMecevi, zMecevi, isUtjesni: false);

            // 2. Razigravanja za utješni bracket (UT_R i UT_PL_)
            if (turnir.SistemTurnira == SistemTurnira.DoubleEliminationUtjesni)
            {
                var utMecevi = sviMecevi.Where(m =>
                    m.TipMeca == TipMeca.Utjesni &&
                    (m.MatchCode.StartsWith(Prefix.UtjesniPlacement) ||
                     (m.MatchCode.StartsWith(Prefix.Utjesni + "R") &&
                      !m.MatchCode.StartsWith(Prefix.UtjesniRoundRobin))))
                    .ToList();
                await GenerisiRazigravanjaZaSkupinuAsync(turnir, sviMecevi, utMecevi, isUtjesni: true);
            }

            await _context.SaveChangesAsync();
        }

        /// <inheritdoc/>
        public async Task<(bool Success, string ErrorMessage)> GenerisiPlasmanZaRangeAsync(
            Turnir turnir, int plL, int plR)
        {
            var sviMecevi = await _context.Mecevi
                .Where(m => m.TurnirID == turnir.ID).ToListAsync();

            string prefiks = $"{Prefix.Placement}{plL}_{plR}_R1_M";
            if (sviMecevi.Any(m => m.MatchCode.StartsWith(prefiks)))
                return (false, $"Razigravanje za mjesta {plL}–{plR} je već generisano.");

            int ukupnoIgraca = plR - plL + 1;
            var zMecevi      = sviMecevi
                .Where(m => m.TipMeca == TipMeca.Zavrsnica && m.MatchCode.StartsWith(Prefix.Zavrsnica))
                .ToList();

            var ciljnaRunda = zMecevi
                .GroupBy(m => m.Runda).OrderBy(g => g.Key)
                .FirstOrDefault(g => g.Count() == ukupnoIgraca);

            if (ciljnaRunda == null || !ciljnaRunda.All(m => m.Odigran))
                return (false,
                    $"Svi mečevi odgovarajuće runde moraju biti odigrani prije generisanja razigravanja za mjesta {plL}–{plR}.");

            var gubitnici = ciljnaRunda
                .Select(zm => zm.GubitnikId ?? BracketService.SLOBODAN).ToList();

            if (gubitnici.Count != ukupnoIgraca)
                return (false,
                    $"Nije moguće odrediti gubitnike za razigravanje {plL}–{plR}. Provjeri odigrane mečeve.");

            await DbSeeder.EnsureSlobodanUserExistsAsync(_context);
            var noviMecevi = _generation.GenerirajPlasmanFazu(turnir, plL, plR, gubitnici, sviMecevi);

            if (!noviMecevi.Any())
                return (false, "Došlo je do greške pri generisanju razigravanja.");

            _context.Mecevi.AddRange(noviMecevi);
            await _context.SaveChangesAsync();
            return (true, string.Empty);
        }

        // ── Privatni helperi ───────────────────────────────────────────────

        /// <summary>
        /// Propaguje igrača u jedan ili više sljedećih mečeva (format: "Code" ili "Code:slot" ili "C1:s1;C2:s2").
        /// </summary>
        private async Task PropagujNaSljedeciMecAsync(
            Mec mec, string? nextMatchCode, int? defaultSlot,
            string playerId, string? partnerId)
        {
            if (string.IsNullOrEmpty(nextMatchCode)) return;

            foreach (var dest in nextMatchCode.Split(';'))
            {
                if (string.IsNullOrEmpty(dest)) continue;
                var parts      = dest.Split(':');
                string code    = parts[0];
                int slot       = parts.Length > 1 && int.TryParse(parts[1], out int s)
                    ? s : (defaultSlot ?? 1);

                var sljedeci = await _context.Mecevi.FirstOrDefaultAsync(
                    m => m.TurnirID == mec.TurnirID && m.MatchCode == code);
                if (sljedeci == null) continue;

                if (slot == 1)
                {
                    sljedeci.Igrac1ID = playerId; sljedeci.Igrac1PartnerID = partnerId;
                }
                else
                {
                    sljedeci.Igrac2ID = playerId; sljedeci.Igrac2PartnerID = partnerId;
                }
            }
        }

        /// <summary>
        /// Za svaku rundi u skupini mečeva provjeri jesu li svi odigrani
        /// i generiše odgovarajuće razigravanje za plasman ako već ne postoji.
        /// </summary>
        private async Task GenerisiRazigravanjaZaSkupinuAsync(
            Turnir turnir, List<Mec> sviMecevi, List<Mec> meceviSkupine, bool isUtjesni)
        {
            string codePrefix = isUtjesni
                ? Prefix.UtjesniPlacement.TrimEnd('_')
                : Prefix.Placement.TrimEnd('_');

            foreach (var rangeGroup in meceviSkupine.GroupBy(m => m.PlacingRange))
            {
                int groupL = 1;
                if (!string.IsNullOrEmpty(rangeGroup.Key))
                {
                    var parts = rangeGroup.Key.Split('-');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int gl)) groupL = gl;
                }

                foreach (var rundaGroup in rangeGroup.GroupBy(m => m.Runda).OrderBy(g => g.Key))
                {
                    var meceviRunde = rundaGroup.ToList();
                    int roundCount  = meceviRunde.Count;
                    if (roundCount <= 1) continue; // Finale ili samo 1 meč — preskočiti

                    int L = (groupL - 1) + roundCount + 1;
                    int R = (groupL - 1) + 2 * roundCount;

                    if (!meceviRunde.All(m => m.Odigran)) continue;

                    string searchPrefix = $"{codePrefix}_{L}_{R}_R1_M";
                    if (sviMecevi.Any(m => m.MatchCode.StartsWith(searchPrefix))) continue;

                    var gubitnici = meceviRunde
                        .Select(zm => zm.GubitnikId ?? BracketService.SLOBODAN).ToList();
                    if (gubitnici.Count != roundCount) continue;

                    await DbSeeder.EnsureSlobodanUserExistsAsync(_context);
                    var noviMecevi = _generation.GenerirajPlasmanFazu(
                        turnir, L, R, gubitnici, sviMecevi, isUtjesni);
                    if (noviMecevi.Any())
                    {
                        _context.Mecevi.AddRange(noviMecevi);
                        sviMecevi.AddRange(noviMecevi);
                    }
                }
            }
        }
    }
}
