using ePinPong.Interfaces;
using ePinPong.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Prefix = ePinPong.AppConstants.MatchCodePrefixes;

namespace ePinPong.Services
{
    /// <summary>
    /// SRP — odgovoran isključivo za <b>kreiranje</b> mečeva i rundi.
    /// Propagacija pobjednika i BYE napredaka tokom trajanja turnira
    /// nalazi se u <see cref="BracketPropagationService"/>.
    /// Context u Strategy paternu za generisanje završnice.
    /// </summary>
    public class BracketGenerationService : IBracketGenerationService
    {
        private readonly IRandomProvider _rng;
        private readonly IReadOnlyDictionary<SistemTurnira, IBracketDrawStrategy> _strategije;

        private const string SLOBODAN = BracketService.SLOBODAN;

        /// <summary>
        /// Gornja granica iteracija u <see cref="PropagirajBye"/> while-petlji.
        /// BYE propagacija je iterativna jer jedan BYE može kaskadno otključati drugi
        /// (npr. ako su oba igrača u narednom meču SLOBODAN, taj meč se tada automatski
        /// odigrava i tako dalje). Maksimalan broj rundi u bracketu je log₂(128) = 7,
        /// pa je 500 konzervativna sigurnosna granica koja sprječava beskonačnu petlju
        /// u slučaju neispravnih MatchCode referenci (kružnih veza).
        /// </summary>
        private const int MaxByePropagationIterations = 500;

        /// <summary>Vraća <c>true</c> ako je <paramref name="id"/> SLOBODAN (BYE) igrač.</summary>
        public static bool JeSlobodan(string? id) => id == SLOBODAN;

        public BracketGenerationService(
            IRandomProvider rng,
            IEnumerable<IBracketDrawStrategy> strategije)
        {
            _rng = rng;
            _strategije = strategije.ToDictionary(s => s.Sistem);
        }

        // ════════════════════════════════════════════════════════════════════
        // PRIVATNE HELPER METODE — SLOT/NEXT-MATCH MATEMATIKA
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Indeks sljedećeg meča u koji pobjednik/gubitnik ulazi.
        /// Npr. meč M1 i M2 → oba idu u M1 sljedeće runde; M3 i M4 → M2 itd.
        /// </summary>
        private static int NextMatchIndex(int m) => (m + 1) / 2;

        /// <summary>
        /// Slot (1 ili 2 — Igrac1 ili Igrac2) koji pobjednik/gubitnik zauzima
        /// u sljedećem meču na osnovu parnog/neparnog broja trenutnog meča.
        /// </summary>
        private static int NextMatchSlot(int m) => m % 2 != 0 ? 1 : 2;

        // ════════════════════════════════════════════════════════════════════
        // PRIVATNA HELPER METODA — PARSIRANJE DESTINACIJA
        // Podržava format "MatchCode", "MatchCode:slot" i "Code1:s1;Code2:s2".
        // ════════════════════════════════════════════════════════════════════

        private static IEnumerable<(string Code, int Slot)> ParseDestinations(
            string? nextMatchCode, int? defaultSlot)
        {
            if (string.IsNullOrEmpty(nextMatchCode)) yield break;
            foreach (var dest in nextMatchCode.Split(';'))
            {
                if (string.IsNullOrEmpty(dest)) continue;
                var parts = dest.Split(':');
                int slot = parts.Length > 1 && int.TryParse(parts[1], out int s)
                    ? s : (defaultSlot ?? 1);
                yield return (parts[0], slot);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAJEDNIČKI SINGLE-ELIMINATION GENERATOR
        // ════════════════════════════════════════════════════════════════════

        public static List<Mec> GenerirajSingleEliminationRunde(
            Turnir turnir,
            int S,
            string matchCodePrefix,
            TipMeca tip,
            string placingRange,
            Func<int, int, DateTime> timeBuilder,
            Func<int, int, string?>? winnerNextCodeBuilder,
            Func<int, int, string?>? loserNextCodeBuilder = null)
        {
            var mecevi = new List<Mec>();
            int roundsCount = (int)Math.Round(Math.Log2(S));

            for (int r = 1; r <= roundsCount; r++)
            {
                int matchesInRound = S / (int)Math.Pow(2, r);
                for (int m = 1; m <= matchesInRound; m++)
                {
                    string? winnerNext = null;
                    int?    winnerSlot = null;
                    string? loserNext  = null;
                    int?    loserSlot  = null;

                    if (r < roundsCount)
                    {
                        int nextM = NextMatchIndex(m);
                        int slot  = NextMatchSlot(m);

                        winnerNext = winnerNextCodeBuilder?.Invoke(r + 1, nextM);
                        winnerSlot = winnerNext != null ? slot : (int?)null;

                        loserNext  = loserNextCodeBuilder?.Invoke(r, nextM);
                        loserSlot  = loserNext  != null ? slot : (int?)null;
                    }

                    mecevi.Add(new Mec
                    {
                        TurnirID            = turnir.ID,
                        MatchCode           = $"{matchCodePrefix}R{r}_M{m}",
                        Runda               = r,
                        Odigran             = false,
                        TipMeca             = tip,
                        PlacingRange        = placingRange,
                        VrijemeMeca         = timeBuilder(r, m),
                        WinnerNextMatchCode = winnerNext,
                        WinnerNextMatchSlot = winnerSlot,
                        LoserNextMatchCode  = loserNext,
                        LoserNextMatchSlot  = loserSlot
                    });
                }
            }

            return mecevi;
        }

        // ════════════════════════════════════════════════════════════════════
        // ROUND-ROBIN GENERATOR
        // ════════════════════════════════════════════════════════════════════

        public static void GenerirajRoundRobin(
            Turnir turnir,
            List<Mec> mecevi,
            int startPos,
            List<(string? Id, string? PartnerId)> ucesnici,
            DateTime startTime,
            TipMeca tip,
            string codePrefix,
            string? nazivGrupe = null)
        {
            int endPos = startPos + ucesnici.Count - 1;
            string placingRange = $"{startPos}-{endPos}";
            int matchNum = 1;

            for (int i = 0; i < ucesnici.Count; i++)
            for (int j = i + 1; j < ucesnici.Count; j++)
            {
                mecevi.Add(new Mec
                {
                    TurnirID        = turnir.ID,
                    MatchCode       = $"{codePrefix}_{startPos}_{endPos}_M{matchNum}",
                    Runda           = 1,
                    Odigran         = false,
                    TipMeca         = tip,
                    PlacingRange    = placingRange,
                    NazivGrupe      = nazivGrupe,
                    VrijemeMeca     = startTime.AddHours(matchNum - 1),
                    Igrac1ID        = ucesnici[i].Id,
                    Igrac1PartnerID = ucesnici[i].PartnerId,
                    Igrac2ID        = ucesnici[j].Id,
                    Igrac2PartnerID = ucesnici[j].PartnerId
                });
                matchNum++;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // UTJEŠNI BRACKET (statički helper)
        // ════════════════════════════════════════════════════════════════════

        public static void GenerirajUtjesniBracketStatic(
            Turnir turnir, List<Mec> mecevi,
            int S_ut, List<string?> igraciBaza, DateTime startTime)
        {
            var rounds = GenerirajSingleEliminationRunde(
                turnir, S_ut,
                matchCodePrefix:       Prefix.Utjesni,
                tip:                   TipMeca.Utjesni,
                placingRange:          $"1-{S_ut}",
                timeBuilder:           (r, m) => startTime.AddDays(r - 1).AddHours(m),
                winnerNextCodeBuilder: (nextR, nextM) => $"{Prefix.Utjesni}R{nextR}_M{nextM}",
                loserNextCodeBuilder:  (r, nextM) =>
                {
                    int plL = S_ut / (int)Math.Pow(2, r) + 1;
                    int plR = S_ut / (int)Math.Pow(2, r - 1);
                    return $"{Prefix.UtjesniPlacement}{plL}_{plR}_R1_M{nextM}";
                });

            var byCode = rounds.ToDictionary(m => m.MatchCode);
            for (int i = 0; i < S_ut / 2; i++)
            {
                if (byCode.TryGetValue($"{Prefix.Utjesni}R1_M{i + 1}", out var mec))
                {
                    mec.Igrac1ID = igraciBaza[2 * i];
                    mec.Igrac2ID = igraciBaza[2 * i + 1];
                }
            }

            mecevi.AddRange(rounds);
        }

        // ════════════════════════════════════════════════════════════════════
        // JAVNE METODE — GENERACIJA TURNIRSKIH FAZA
        // ════════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public List<Mec> GenerirajZavrsnicu(Turnir turnir, List<Mec> meceviGrupneFaze)
        {
            if (!_strategije.TryGetValue(turnir.SistemTurnira, out var strategija))
                throw new InvalidOperationException(
                    $"Nema registrovane strategije za sistem turnira '{turnir.SistemTurnira}'.");

            return strategija.GenerirajZavrsnicu(turnir, meceviGrupneFaze);
        }

        /// <inheritdoc/>
        public List<Mec> GenerirajTurnirParova(Turnir turnir, List<TurnirPar> parovi)
        {
            var mecevi = new List<Mec>();
            int N = parovi.Count;
            if (N < 2) return mecevi;

            DateTime pocetak = turnir.Mecevi?.Any() == true
                ? turnir.Mecevi.Max(m => m.VrijemeMeca).AddHours(2)
                : DateTime.Now;

            var shuffledParovi = parovi.OrderBy(_ => _rng.Next()).ToList();

            // ── Round-robin za 3 para ─────────────────────────────────────
            if (N == 3)
            {
                var ucesnici = shuffledParovi
                    .Select(p => (p.Igrac1ID, (string?)p.Igrac2ID))
                    .ToList();
                GenerirajRoundRobin(
                    turnir, mecevi, startPos: 1, ucesnici,
                    pocetak, TipMeca.TurnirParova,
                    Prefix.ParTurnirRoundRobin.TrimEnd('_'),
                    nazivGrupe: "Grupa A (Parovi)");
                return mecevi;
            }

            // ── Single-elimination za N ≥ 4 ──────────────────────────────
            int S = 2;
            while (S < N) S *= 2;

            var pairKeys   = Enumerable.Range(0, N).Select(i => (string?)i.ToString()).ToList();
            var distributed = BracketDrawService.RasporediSaSlobodanom(pairKeys, S, _rng.Next);

            mecevi.AddRange(GenerirajSingleEliminationRunde(
                turnir, S,
                matchCodePrefix:       Prefix.ParTurnir,
                tip:                   TipMeca.TurnirParova,
                placingRange:          $"1-{S}",
                timeBuilder:           (r, m) => pocetak.AddDays(r - 1).AddHours(m * 2),
                winnerNextCodeBuilder: (nextR, nextM) => $"{Prefix.ParTurnir}R{nextR}_M{nextM}",
                loserNextCodeBuilder:  null));

            // Dodijeli parove R1 mečevima (O(1) dict)
            var byCode = mecevi.ToDictionary(m => m.MatchCode);
            for (int i = 0; i < S / 2; i++)
            {
                if (!byCode.TryGetValue($"{Prefix.ParTurnir}R1_M{i + 1}", out var mecR1)) continue;

                string? key1 = distributed[2 * i];
                string? key2 = distributed[2 * i + 1];

                if (key1 != null && !JeSlobodan(key1))
                {
                    var par1 = shuffledParovi[int.Parse(key1)];
                    mecR1.Igrac1ID = par1.Igrac1ID; mecR1.Igrac1PartnerID = par1.Igrac2ID;
                }
                else if (JeSlobodan(key1)) mecR1.Igrac1ID = SLOBODAN;

                if (key2 != null && !JeSlobodan(key2))
                {
                    var par2 = shuffledParovi[int.Parse(key2)];
                    mecR1.Igrac2ID = par2.Igrac1ID; mecR1.Igrac2PartnerID = par2.Igrac2ID;
                }
                else if (JeSlobodan(key2)) mecR1.Igrac2ID = SLOBODAN;
            }

            PropagirajBye(mecevi);
            return mecevi;
        }

        /// <inheritdoc/>
        public List<Mec> GenerirajPlasmanFazu(
            Turnir turnir, int L, int R,
            List<string?> gubitnici, List<Mec> postojeciMecevi, bool isUtjesni = false)
        {
            var noviMecevi = new List<Mec>();
            int n = R - L + 1;
            if (gubitnici.Count != n) return noviMecevi;

            var shuffled  = gubitnici.OrderBy(_ => _rng.Next()).ToList();
            DateTime start = postojeciMecevi.Any()
                ? postojeciMecevi.Max(m => m.VrijemeMeca).AddHours(2) : DateTime.Now;

            GenerirajPlasmanRekurzivno(turnir, noviMecevi, L, R, shuffled, start, 0, isUtjesni);

            var sviKombinovani = postojeciMecevi.Concat(noviMecevi).ToList();
            PropagirajBye(sviKombinovani);

            return noviMecevi;
        }

        private void GenerirajPlasmanRekurzivno(
            Turnir turnir, List<Mec> mecevi,
            int L, int R, List<string?>? igraciBaza,
            DateTime startTime, int dubina, bool isUtjesni = false)
        {
            int n = R - L + 1;
            if (n < 2) return;

            int half       = n / 2;
            string prefix  = isUtjesni ? Prefix.UtjesniPlacement.TrimEnd('_') : Prefix.Placement.TrimEnd('_');
            TipMeca tip    = isUtjesni ? TipMeca.Utjesni : TipMeca.Razigravanje;

            for (int m = 1; m <= half; m++)
            {
                int subMatchIdx = NextMatchIndex(m);
                int subSlot     = NextMatchSlot(m);

                string? winnerNext = null, loserNext = null;
                int?    winnerSlot = null, loserSlot = null;

                if (n > 2)
                {
                    winnerNext = $"{prefix}_{L}_{L + half - 1}_R1_M{subMatchIdx}";
                    winnerSlot = subSlot;
                    loserNext  = $"{prefix}_{L + half}_{R}_R1_M{subMatchIdx}";
                    loserSlot  = subSlot;
                }

                string? igrac1 = null, igrac2 = null;
                if (igraciBaza != null && dubina == 0)
                {
                    igrac1 = igraciBaza[(m - 1) * 2];
                    igrac2 = igraciBaza[(m - 1) * 2 + 1];
                }

                mecevi.Add(new Mec
                {
                    TurnirID            = turnir.ID,
                    MatchCode           = $"{prefix}_{L}_{R}_R1_M{m}",
                    Runda               = 1,
                    Odigran             = false,
                    TipMeca             = tip,
                    PlacingRange        = $"{L}-{R}",
                    VrijemeMeca         = startTime.AddHours(m + dubina * 2),
                    WinnerNextMatchCode = winnerNext,
                    WinnerNextMatchSlot = winnerSlot,
                    LoserNextMatchCode  = loserNext,
                    LoserNextMatchSlot  = loserSlot,
                    Igrac1ID            = igrac1,
                    Igrac2ID            = igrac2
                });
            }

            if (n > 2)
            {
                GenerirajPlasmanRekurzivno(turnir, mecevi, L, L + half - 1, null,
                    startTime.AddDays(1), dubina + 1, isUtjesni);
                GenerirajPlasmanRekurzivno(turnir, mecevi, L + half, R, null,
                    startTime.AddDays(1), dubina + 1, isUtjesni);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // BYE PROPAGACIJA
        // ════════════════════════════════════════════════════════════════════

        /// <inheritdoc/>
        public void PropagirajBye(List<Mec> mecevi) => PropagirajByeStatic(mecevi);

        public static void PropagirajByeStatic(List<Mec> mecevi)
        {
            var bracketMecevi = mecevi.Where(m => !string.IsNullOrEmpty(m.MatchCode)).ToList();

            var byCode = bracketMecevi.ToDictionary(m => m.MatchCode);

            var reverseMap = new Dictionary<string, Mec>(StringComparer.Ordinal);
            foreach (var mec in bracketMecevi)
            {
                foreach (var (code, slot) in ParseDestinations(mec.WinnerNextMatchCode, mec.WinnerNextMatchSlot))
                    reverseMap[$"{code}:{slot}"] = mec;
                foreach (var (code, slot) in ParseDestinations(mec.LoserNextMatchCode, mec.LoserNextMatchSlot))
                    reverseMap[$"{code}:{slot}"] = mec;
            }

            bool promijenjeno    = true;
            int limitSigurnosti  = 0;

            while (promijenjeno && limitSigurnosti < MaxByePropagationIterations)
            {
                promijenjeno = false;
                limitSigurnosti++;

                foreach (var mec in bracketMecevi.Where(m => !m.Odigran))
                {
                    bool slot1OK = !reverseMap.TryGetValue($"{mec.MatchCode}:1", out var pred1)
                                   || pred1.Odigran || mec.Igrac1ID != null;
                    bool slot2OK = !reverseMap.TryGetValue($"{mec.MatchCode}:2", out var pred2)
                                   || pred2.Odigran || mec.Igrac2ID != null;

                    if (!slot1OK || !slot2OK) continue;

                    bool igrac1Slobodan = mec.Igrac1ID == null || JeSlobodan(mec.Igrac1ID);
                    bool igrac2Slobodan = mec.Igrac2ID == null || JeSlobodan(mec.Igrac2ID);

                    if (!igrac1Slobodan && !igrac2Slobodan) continue;

                    mec.Odigran = true;

                    string? winnerId, loserId, winnerPartnerId, loserPartnerId;

                    if (!igrac1Slobodan)
                    {
                        if (mec.Igrac2ID == null) mec.Igrac2ID = SLOBODAN;
                        mec.PoeniIgrac1 = 3; mec.PoeniIgrac2 = 0;
                        winnerId        = mec.Igrac1ID;       loserId        = mec.Igrac2ID;
                        winnerPartnerId = mec.Igrac1PartnerID; loserPartnerId = mec.Igrac2PartnerID;
                    }
                    else if (!igrac2Slobodan)
                    {
                        if (mec.Igrac1ID == null) mec.Igrac1ID = SLOBODAN;
                        mec.PoeniIgrac1 = 0; mec.PoeniIgrac2 = 3;
                        winnerId        = mec.Igrac2ID;        loserId        = mec.Igrac1ID;
                        winnerPartnerId = mec.Igrac2PartnerID; loserPartnerId = mec.Igrac1PartnerID;
                    }
                    else
                    {
                        if (mec.Igrac1ID == null) mec.Igrac1ID = SLOBODAN;
                        if (mec.Igrac2ID == null) mec.Igrac2ID = SLOBODAN;
                        mec.PoeniIgrac1 = 0; mec.PoeniIgrac2 = 0;
                        winnerId        = SLOBODAN; loserId        = SLOBODAN;
                        winnerPartnerId = null;    loserPartnerId = null;
                    }

                    PropagujIgracaUSljedeci(byCode, mec.WinnerNextMatchCode, mec.WinnerNextMatchSlot,
                        winnerId, winnerPartnerId, ref promijenjeno);
                    PropagujIgracaUSljedeci(byCode, mec.LoserNextMatchCode, mec.LoserNextMatchSlot,
                        loserId, loserPartnerId, ref promijenjeno);
                }
            }
        }

        private static void PropagujIgracaUSljedeci(
            Dictionary<string, Mec> byCode,
            string? nextMatchCode, int? defaultSlot,
            string? playerId, string? partnerId,
            ref bool promijenjeno)
        {
            foreach (var (code, slot) in ParseDestinations(nextMatchCode, defaultSlot))
            {
                if (!byCode.TryGetValue(code, out var sljedeci)) continue;

                if (slot == 1 && sljedeci.Igrac1ID != playerId)
                {
                    sljedeci.Igrac1ID = playerId; sljedeci.Igrac1PartnerID = partnerId;
                    promijenjeno = true;
                }
                else if (slot == 2 && sljedeci.Igrac2ID != playerId)
                {
                    sljedeci.Igrac2ID = playerId; sljedeci.Igrac2PartnerID = partnerId;
                    promijenjeno = true;
                }
            }
        }
    }
}
