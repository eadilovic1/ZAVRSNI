using ePinPong.Interfaces;
using ePinPong.Models;
using ePinPong.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Prefix = ePinPong.AppConstants.MatchCodePrefixes;

namespace ePinPong.Services.BracketStrategies
{
    /// <summary>
    /// Bazna klasa za strategije generisanja bracketa. Implementira zajednički algoritam
    /// obrade rezultata grupne faze, rangiranja, određivanja BYE-ova i kreiranja R1 mečeva.
    /// Konkretne strategije specificiraju grananje za gubitnički i utješni bracket.
    /// </summary>
    public abstract class BaseBracketDrawStrategy : IBracketDrawStrategy
    {
        public abstract SistemTurnira Sistem { get; }

        protected abstract bool HasLoserBracket { get; }
        protected virtual bool HasUtjesniBracket => false;

        protected readonly IRandomProvider _rng;

        protected BaseBracketDrawStrategy(IRandomProvider rng)
        {
            _rng = rng;
        }

        public virtual List<Mec> GenerirajZavrsnicu(Turnir turnir, List<Mec> meceviGrupneFaze)
        {
            // ── 1. Izračunaj plasmane iz grupne faze ─────────────────────
            var meceviPoGrupama = meceviGrupneFaze
                .GroupBy(m => m.NazivGrupe).OrderBy(g => g.Key).ToList();

            var plasmani1            = new List<PlayerRecord>();
            var plasmani2            = new List<PlayerRecord>();
            var utjesniIgraciRecords = new List<PlayerRecord>();

            for (int gIdx = 0; gIdx < meceviPoGrupama.Count; gIdx++)
            {
                var grupaGroup  = meceviPoGrupama[gIdx];
                string nazivGrupe = grupaGroup.Key ?? "Grupa";
                var meceviGrupe = grupaGroup.ToList();

                var igraciGrupe = meceviGrupe
                    .SelectMany(m => new[] { m.Igrac1ID, m.Igrac2ID })
                    .Where(id => id != null)
                    .Distinct()
                    .Select(id => id!)
                    .ToList();

                var stats = StandingsCalculationService.IzracunajStatistikuIgraca(meceviGrupe, igraciGrupe);
                var records = stats.Select(x => new PlayerRecord
                {
                    PlayerID       = x.PlayerId,
                    NazivGrupe     = nazivGrupe,
                    GroupIndex     = gIdx,
                    Pobjede        = x.Wins,
                    SetRazlika     = x.SetDiff,
                    OsvojeniSetovi = x.SetsWon
                }).ToList();

                var sorted = records
                    .OrderByDescending(r => r.Pobjede)
                    .ThenByDescending(r => r.SetRazlika)
                    .ThenByDescending(r => r.OsvojeniSetovi)
                    .ToList();

                if (sorted.Count > 0) plasmani1.Add(sorted[0]);
                if (sorted.Count > 1) plasmani2.Add(sorted[1]);
                if (sorted.Count > 2) utjesniIgraciRecords.AddRange(sorted.Skip(2));
            }

            int G = meceviPoGrupama.Count;
            if (G == 0) return new List<Mec>();

            // ── 2. Sortiraj i odredi bye-ove ─────────────────────────────
            var sortedWinners = plasmani1
                .OrderByDescending(p => p.Pobjede).ThenByDescending(p => p.SetRazlika)
                .ThenByDescending(p => p.OsvojeniSetovi).ToList();
            var sortedRunners = plasmani2
                .OrderByDescending(p => p.Pobjede).ThenByDescending(p => p.SetRazlika)
                .ThenByDescending(p => p.OsvojeniSetovi).ToList();

            int M = 2 * G;
            int S = 2;
            while (S < M) S *= 2;
            int B = S - M;

            var priorityList = sortedWinners.Concat(sortedRunners).ToList();
            for (int i = 0; i < priorityList.Count; i++)
                priorityList[i].HasBye = i < B;

            var winnersPlay = sortedWinners.Where(w => !w.HasBye).ToList();
            var winnersBye  = sortedWinners.Where(w =>  w.HasBye).ToList();
            var runnersPlay = sortedRunners.Where(r => !r.HasBye).ToList();
            var runnersBye  = sortedRunners.Where(r =>  r.HasBye).ToList();

            // ── 3. Napravi parove za R1 ──────────────────────────────────
            var pairings    = new List<(PlayerRecord, PlayerRecord?)>();
            var usedRunners = new HashSet<string>();

            foreach (var w in winnersPlay)
            {
                var opp = runnersPlay.FirstOrDefault(r => r.GroupIndex != w.GroupIndex && !usedRunners.Contains(r.PlayerID))
                       ?? runnersPlay.FirstOrDefault(r => !usedRunners.Contains(r.PlayerID));
                if (opp != null) { usedRunners.Add(opp.PlayerID); pairings.Add((w, opp)); }
                else pairings.Add((w, null));
            }

            var remainingRunners = runnersPlay.Where(r => !usedRunners.Contains(r.PlayerID)).ToList();
            for (int i = 0; i < remainingRunners.Count; i += 2)
                pairings.Add(i + 1 < remainingRunners.Count
                    ? (remainingRunners[i], remainingRunners[i + 1])
                    : (remainingRunners[i], null));

            foreach (var w in winnersBye) pairings.Add((w, null));
            foreach (var r in runnersBye)  pairings.Add((r, null));

            // ── 4. Generiši runde završnice ──────────────────────────────
            DateTime start = meceviGrupneFaze.Any()
                ? meceviGrupneFaze.Max(m => m.VrijemeMeca).AddHours(2) : DateTime.UtcNow;

            var sviMecevi = BracketGenerationService.GenerirajSingleEliminationRunde(
                turnir, S,
                matchCodePrefix:       Prefix.Zavrsnica,
                tip:                   TipMeca.Zavrsnica,
                placingRange:          $"1-{S}",
                timeBuilder:           (r, m) => start.AddDays(r - 1).AddHours(m * 2),
                winnerNextCodeBuilder: (nextR, nextM) => $"{Prefix.Zavrsnica}R{nextR}_M{nextM}",
                loserNextCodeBuilder:  HasLoserBracket
                    ? (r, nextM) =>
                    {
                        int plL   = S / (int)Math.Pow(2, r) + 1;
                        int plR   = S / (int)Math.Pow(2, r - 1);
                        return $"{Prefix.Placement}{plL}_{plR}_R1_M{nextM}";
                    }
                    : null);

            // ── 5. Dodijeli igrače R1 mečevima (O(1) dict) ───────────────
            var byCode = sviMecevi.ToDictionary(m => m.MatchCode);
            for (int i = 0; i < pairings.Count; i++)
            {
                if (byCode.TryGetValue($"{Prefix.Zavrsnica}R1_M{i + 1}", out var mecR1))
                {
                    mecR1.Igrac1ID = pairings[i].Item1?.PlayerID ?? BracketService.SLOBODAN;
                    mecR1.Igrac2ID = pairings[i].Item2?.PlayerID ?? BracketService.SLOBODAN;
                }
            }

            // ── 6. Utješni bracket (opcionalni) ─────────────────────────
            if (HasUtjesniBracket)
            {
                GenerirajUtjesniBracket(turnir, sviMecevi, utjesniIgraciRecords, start);
            }

            BracketGenerationService.PropagirajByeStatic(sviMecevi);
            return sviMecevi;
        }

        private void GenerirajUtjesniBracket(
            Turnir turnir, List<Mec> sviMecevi,
            List<PlayerRecord> utjesniIgraciRecords, DateTime start)
        {
            var utjesniShuffled = utjesniIgraciRecords
                .Select(r => (string?)r.PlayerID)
                .OrderBy(_ => _rng.Next())
                .ToList();
            int utCount = utjesniShuffled.Count;

            if (utCount > 0)
            {
                var meceviUt = new List<Mec>();
                if (utCount == 3)
                {
                    var ucesnici = utjesniShuffled.Select(p => (p, (string?)null)).ToList();
                    BracketGenerationService.GenerirajRoundRobin(turnir, meceviUt, 1, ucesnici,
                        start, TipMeca.Utjesni,
                        Prefix.UtjesniRoundRobin.TrimEnd('_'));
                }
                else
                {
                    int S_ut = 2;
                    while (S_ut < utCount) S_ut *= 2;
                    var finalList = BracketDrawService.RasporediSaSlobodanom(
                        utjesniShuffled, S_ut, _rng.Next);
                    BracketGenerationService.GenerirajUtjesniBracketStatic(turnir, meceviUt, S_ut, finalList, start);
                }
                sviMecevi.AddRange(meceviUt);
            }
        }

        private sealed class PlayerRecord
        {
            public string PlayerID        { get; set; } = string.Empty;
            public string NazivGrupe      { get; set; } = string.Empty;
            public int    GroupIndex      { get; set; }
            public int    Pobjede         { get; set; }
            public int    SetRazlika      { get; set; }
            public int    OsvojeniSetovi  { get; set; }
            public bool   HasBye          { get; set; }
        }
    }
}
