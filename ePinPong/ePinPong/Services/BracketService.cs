using ePinPong.Models;
using ePinPong.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ePinPong.Services
{
    public class BracketService : IBracketService
    {
        // Sentinel ID koji označava fiktivnog igrača koji automatski gubi
        public const string SLOBODAN = "SLOBODAN";
        // ─────────────────────────────────────────────────────────────────────
        //  POMOĆNA: Parsiraj PlacingRange
        // ─────────────────────────────────────────────────────────────────────
        private static (int L, int R) ParsiranjRangePublic(string? range)
        {
            if (string.IsNullOrEmpty(range)) return (0, 0);
            var parts = range.Split('-');
            if (parts.Length == 2
                && int.TryParse(parts[0], out int L)
                && int.TryParse(parts[1], out int R))
                return (L, R);
            return (0, 0);
        }
        // ─────────────────────────────────────────────────────────────────────
        //  GRUPNA FAZA
        // ─────────────────────────────────────────────────────────────────────

        public List<Mec> GenerirajGrupe(Turnir turnir, List<string> igracIds, bool useQualityGrouping = false)
        {
            var mecevi = new List<Mec>();
            int N = igracIds.Count;
            if (N < 3) return mecevi;

            var groupSizes = useQualityGrouping ? GetMastersGroupSizes(N) : GetGroupSizes(N);
            if (groupSizes.Count == 0) return mecevi;

            int ukGrupa = groupSizes.Count;
            int groupsOf4 = groupSizes.Count(size => size == 4);

            // Read the pots from registrations
            var pot1 = new List<string>();
            var pot2 = new List<string>();
            var pot3 = new List<string>();
            var pot4 = new List<string>();

            if (turnir.Registracije != null && turnir.Registracije.Any())
            {
                foreach (var reg in turnir.Registracije)
                {
                    if (!igracIds.Contains(reg.KorisnikID)) continue;

                    if (reg.Sesir == 1) pot1.Add(reg.KorisnikID);
                    else if (reg.Sesir == 2) pot2.Add(reg.KorisnikID);
                    else if (reg.Sesir == 3) pot3.Add(reg.KorisnikID);
                    else if (reg.Sesir == 4) pot4.Add(reg.KorisnikID);
                    else pot4.Add(reg.KorisnikID); // Fallback za nepoznate
                }
            }

            var rng = new Random();
            var grupeIgraca = new List<List<string>>();
            for (int i = 0; i < ukGrupa; i++) grupeIgraca.Add(new List<string>());

            int expectedPot4Count = groupSizes.Sum(size => Math.Max(0, size - 3));
            if (useQualityGrouping)
            {
                grupeIgraca = DistributeOrderedPlayersIntoGroups(igracIds, groupSizes);
            }
            else if (pot1.Count == ukGrupa && pot2.Count == ukGrupa && pot3.Count == ukGrupa && pot4.Count == expectedPot4Count)
            {
                var shuffledPot1 = pot1.OrderBy(a => rng.Next()).ToList();
                var shuffledPot2 = pot2.OrderBy(a => rng.Next()).ToList();
                var shuffledPot3 = pot3.OrderBy(a => rng.Next()).ToList();
                var shuffledPot4 = pot4.OrderBy(a => rng.Next()).ToList();

                for (int g = 0; g < ukGrupa; g++)
                {
                    grupeIgraca[g].Add(shuffledPot1[g]);
                    grupeIgraca[g].Add(shuffledPot2[g]);
                    grupeIgraca[g].Add(shuffledPot3[g]);
                    int extraCount = groupSizes[g] - 3;
                    for (int e = 0; e < extraCount; e++)
                    {
                        if (shuffledPot4.Count > 0)
                        {
                            grupeIgraca[g].Add(shuffledPot4[0]);
                            shuffledPot4.RemoveAt(0);
                        }
                    }
                }
            }
            else
            {
                var shuffledIgracIds = igracIds.OrderBy(a => rng.Next()).ToList();
                int igracIdx = 0;
                for (int g = 0; g < ukGrupa; g++)
                {
                    for (int i = 0; i < groupSizes[g] && igracIdx < N; i++)
                    {
                        grupeIgraca[g].Add(shuffledIgracIds[igracIdx++]);
                    }
                }
            }

            DateTime pocetak = turnir.DatumPocetka;
            int mecsIndex = 0;
            for (int g = 0; g < ukGrupa; g++)
            {
                string nazivGrupe = $"Grupa {(char)('A' + g)}";
                var clanoviGrupe = grupeIgraca[g];
                int nClanova = clanoviGrupe.Count;

                for (int i = 0; i < nClanova; i++)
                    for (int j = i + 1; j < nClanova; j++)
                    {
                        mecevi.Add(new Mec
                        {
                            TurnirID = turnir.ID,
                            Igrac1ID = clanoviGrupe[i],
                            Igrac2ID = clanoviGrupe[j],
                            Runda = 1,
                            Odigran = false,
                            VrijemeMeca = pocetak.AddMinutes(g * 35 + mecsIndex * 15),
                            TipMeca = TipMeca.GrupnaFaza,
                            NazivGrupe = nazivGrupe,
                            PlacingRange = ""
                        });
                        mecsIndex++;
                    }
            }

            return mecevi;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  ZAVRŠNICA  +  RAZIGRAVANJE ZA MJESTA
        // ─────────────────────────────────────────────────────────────────────

        public List<Mec> GenerirajZavrsnicu(Turnir turnir, List<Mec> meceviGrupneFaze)
        {
            // ── 1. Parsiraj grupnu fazu ───────────────────────────────────────
            var meceviPoGrupama = meceviGrupneFaze.GroupBy(m => m.NazivGrupe).OrderBy(g => g.Key).ToList();
            var plasmani1 = new List<PlayerRecord>(); // pobjednici grupa
            var plasmani2 = new List<PlayerRecord>(); // drugoplasirani
            var utjesniIgraciRecords = new List<PlayerRecord>(); // igrači za utješni turnir

            for (int gIdx = 0; gIdx < meceviPoGrupama.Count; gIdx++)
            {
                var grupaGroup = meceviPoGrupama[gIdx];
                string nazivGrupe = grupaGroup.Key ?? "Grupa";
                var meceviGrupe = grupaGroup.ToList();

                var igraciGrupe = meceviGrupe
                    .SelectMany(m => new[] { m.Igrac1ID, m.Igrac2ID })
                    .Where(id => id != null).Distinct().ToList();

                var records = new List<PlayerRecord>();
                foreach (var igracId in igraciGrupe)
                {
                    if (igracId == null) continue;
                    int pobjede = 0, osvojeniSetovi = 0, izgubljeniSetovi = 0;

                    foreach (var m in meceviGrupe.Where(m => m.Odigran))
                    {
                        if (m.Igrac1ID == igracId)
                        {
                            osvojeniSetovi += m.PoeniIgrac1 ?? 0;
                            izgubljeniSetovi += m.PoeniIgrac2 ?? 0;
                            if ((m.PoeniIgrac1 ?? 0) > (m.PoeniIgrac2 ?? 0)) pobjede++;
                        }
                        else if (m.Igrac2ID == igracId)
                        {
                            osvojeniSetovi += m.PoeniIgrac2 ?? 0;
                            izgubljeniSetovi += m.PoeniIgrac1 ?? 0;
                            if ((m.PoeniIgrac2 ?? 0) > (m.PoeniIgrac1 ?? 0)) pobjede++;
                        }
                    }

                    records.Add(new PlayerRecord
                    {
                        PlayerID = igracId,
                        NazivGrupe = nazivGrupe,
                        GroupIndex = gIdx,
                        Pobjede = pobjede,
                        SetRazlika = osvojeniSetovi - izgubljeniSetovi,
                        OsvojeniSetovi = osvojeniSetovi
                    });
                }

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

            // ── 2. Veličina bracketa ─────────────────────────────────────────
            var sortedWinners = plasmani1
                .OrderByDescending(p => p.Pobjede).ThenByDescending(p => p.SetRazlika)
                .ThenByDescending(p => p.OsvojeniSetovi).ToList();

            var sortedRunners = plasmani2
                .OrderByDescending(p => p.Pobjede).ThenByDescending(p => p.SetRazlika)
                .ThenByDescending(p => p.OsvojeniSetovi).ToList();

            int M = 2 * G;
            int S = 2;
            while (S < M) S *= 2;
            int B = S - M; // BYE-ovi

            // Označi BYE-ove (prvih B igrača po prioritetu dobijaju BYE)
            var priorityList = new List<PlayerRecord>();
            priorityList.AddRange(sortedWinners);
            priorityList.AddRange(sortedRunners);
            for (int i = 0; i < priorityList.Count; i++)
                priorityList[i].HasBye = i < B;

            var winnersPlay = sortedWinners.Where(w => !w.HasBye).ToList();
            var winnersBye  = sortedWinners.Where(w =>  w.HasBye).ToList();
            var runnersPlay = sortedRunners.Where(r => !r.HasBye).ToList();
            var runnersBye  = sortedRunners.Where(r =>  r.HasBye).ToList();

            // ── 3. Pravi parove za R1 glavnog stabla ─────────────────────────
            var pairings = new List<(PlayerRecord, PlayerRecord?)>();
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
            {
                if (i + 1 < remainingRunners.Count)
                    pairings.Add((remainingRunners[i], remainingRunners[i + 1]));
                else
                    pairings.Add((remainingRunners[i], null));
            }

            foreach (var w in winnersBye)  pairings.Add((w, null));
            foreach (var r in runnersBye)  pairings.Add((r, null));

            // ── 4. Generiši sve Z_ mečeve ────────────────────────────────────
            var sviMecevi = new List<Mec>();
            int roundsCount = (int)Math.Round(Math.Log2(S));

            DateTime pocetakZavrsnice = meceviGrupneFaze.Any()
                ? meceviGrupneFaze.Max(m => m.VrijemeMeca).AddHours(2)
                : DateTime.Now;

            for (int r = 1; r <= roundsCount; r++)
            {
                int matchesInRound = S / (int)Math.Pow(2, r);
                for (int m = 1; m <= matchesInRound; m++)
                {
                    var mec = new Mec
                    {
                        TurnirID   = turnir.ID,
                        MatchCode  = $"Z_R{r}_M{m}",
                        Runda      = r,
                        Odigran    = false,
                        TipMeca    = TipMeca.Zavrsnica,
                        VrijemeMeca = pocetakZavrsnice.AddDays(r - 1).AddHours(m * 2),
                        PlacingRange = $"1-{S}"
                    };

                    if (r < roundsCount)
                    {
                        mec.WinnerNextMatchCode = $"Z_R{r + 1}_M{(m + 1) / 2}";
                        mec.WinnerNextMatchSlot = (m % 2 != 0) ? 1 : 2;

                        if (turnir.SistemTurnira != SistemTurnira.SingleElimination)
                        {
                            // Gubitnik ide u razigravanje za mjesta
                            int plL = S / (int)Math.Pow(2, r) + 1;
                            int plR = S / (int)Math.Pow(2, r - 1);
                            mec.LoserNextMatchCode = $"PL_{plL}_{plR}_R1_M{(m + 1) / 2}";
                            mec.LoserNextMatchSlot = (m % 2 != 0) ? 1 : 2;
                        }
                    }
                    // Posljednji krug (finale): gubitnik dobija 2. mjesto, nema LoserNextMatch

                    sviMecevi.Add(mec);
                }
            }

            // ── 5. Popuni R1 s parovima ──────────────────────────────────────
            for (int i = 0; i < pairings.Count; i++)
            {
                var pair = pairings[i];
                var mecR1 = sviMecevi.FirstOrDefault(m => m.MatchCode == $"Z_R1_M{i + 1}");
                if (mecR1 != null)
                {
                    mecR1.Igrac1ID = pair.Item1?.PlayerID ?? SLOBODAN;
                    mecR1.Igrac2ID = pair.Item2?.PlayerID ?? SLOBODAN;
                }
            }

            // ── 5.b Generiraj utješni turnir (samo za DoubleEliminationUtjesni) ──
            if (turnir.SistemTurnira == SistemTurnira.DoubleEliminationUtjesni)
            {
                var rngUt = new Random();
                var utjesniPlayersShuffled = utjesniIgraciRecords
                    .Select(r => (string?)r.PlayerID)
                    .OrderBy(_ => rngUt.Next())
                    .ToList();
                int utjesniIgraciCount = utjesniPlayersShuffled.Count;

                if (utjesniIgraciCount > 0)
                {
                    var meceviUtjesnog = new List<Mec>();

                    if (utjesniIgraciCount == 3)
                    {
                        // Specijalni slučaj: točno 3 igrača → round-robin (svaki sa svakim) s relativnim rasponom 1-3
                        GenerirajRoundRobin3(turnir, meceviUtjesnog, 1, utjesniPlayersShuffled, pocetakZavrsnice);
                    }
                    else
                    {
                        // Popuni šemu do 2^n sa "Slobodan" igračima
                        int S_utjesni = 2;
                        while (S_utjesni < utjesniIgraciCount) S_utjesni *= 2;

                        var finalList = RasporediSaSlobodanom(utjesniPlayersShuffled, S_utjesni, rngUt);
                        GenerirajUtjesniBracket(turnir, meceviUtjesnog, S_utjesni, finalList, pocetakZavrsnice);
                    }

                    sviMecevi.AddRange(meceviUtjesnog);
                }
            }

            // ── 6. Propagiraj BYE-ove kroz Z_ i UT_ mečeve ───────────────────────
            PropagirajBye(sviMecevi);

            return sviMecevi;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  GENERISANJE PL_ FAZE NA ZAHTJEV
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Generiše razigravanje za mjesta L do R sa datim gubitnicima.
        /// Gubitnici se nasumično raspoređuju u R1 mečeve.
        /// Pobjednici R1 mečeva idu u gornji sub-bracket (L .. L+half-1),
        /// gubitnici R1 mečeva idu u donji sub-bracket (L+half .. R).
        /// Ako je isUtjesni = true, mečevi se kreiraju sa TipMeca.Utjesni i kodom UT_PL_.
        /// </summary>
        public List<Mec> GenerirajPlasmanFazu(Turnir turnir, int L, int R, List<string?> gubitnici, List<Mec> postojeciMecevi, bool isUtjesni = false)
        {
            var noviMecevi = new List<Mec>();
            int n = R - L + 1; // ukupno igrača u ovoj fazi
            if (gubitnici.Count != n) return noviMecevi;

            // Nasumično rasporedi gubitnike
            var rng = new Random();
            var shuffled = gubitnici.OrderBy(_ => rng.Next()).ToList();

            DateTime startTime = postojeciMecevi.Any()
                ? postojeciMecevi.Max(m => m.VrijemeMeca).AddHours(2)
                : DateTime.Now;

            // Rekurzivno kreiraj cijeli PL bracket
            GenerirajPlasmanRekurzivno(turnir, noviMecevi, L, R, shuffled, startTime, 0, isUtjesni);

            // Propagiraj BYE-ove kroz novostvorene mečeve
            var sviKombinovani = postojeciMecevi.Concat(noviMecevi).ToList();
            PropagirajBye(sviKombinovani);

            return noviMecevi;
        }

        /// <summary>
        /// Rekurzivno kreira PL_ ili UT_PL_ bracket i dodjeljuje igrače u R1.
        /// </summary>
        private void GenerirajPlasmanRekurzivno(
            Turnir turnir, List<Mec> mecevi,
            int L, int R,
            List<string?>? igraciBaza, // samo za R1, null za dublje runde
            DateTime startTime, int dubina,
            bool isUtjesni = false)
        {
            int n = R - L + 1;
            if (n < 2) return;

            int half = n / 2;
            string codePrefix = isUtjesni ? "UT_PL" : "PL";
            TipMeca tip = isUtjesni ? TipMeca.Utjesni : TipMeca.Razigravanje;

            for (int m = 1; m <= half; m++)
            {
                int subMatchIdx = (m + 1) / 2;
                int subSlot = (m % 2 != 0) ? 1 : 2;

                string? winnerNext = null, loserNext = null;
                int? winnerSlot = null, loserSlot = null;

                if (n > 2)
                {
                    winnerNext = $"{codePrefix}_{L}_{L + half - 1}_R1_M{subMatchIdx}";
                    winnerSlot = subSlot;
                    loserNext = $"{codePrefix}_{L + half}_{R}_R1_M{subMatchIdx}";
                    loserSlot = subSlot;
                }

                string? igrac1 = null, igrac2 = null;
                if (igraciBaza != null && dubina == 0)
                {
                    // Dodjeli igrače samo u R1 (dubina == 0)
                    igrac1 = igraciBaza[(m - 1) * 2];
                    igrac2 = igraciBaza[(m - 1) * 2 + 1];
                }

                mecevi.Add(new Mec
                {
                    TurnirID = turnir.ID,
                    MatchCode = $"{codePrefix}_{L}_{R}_R1_M{m}",
                    Runda = 1,
                    Odigran = false,
                    TipMeca = tip,
                    PlacingRange = $"{L}-{R}",
                    VrijemeMeca = startTime.AddHours(m + dubina * 2),
                    WinnerNextMatchCode = winnerNext,
                    WinnerNextMatchSlot = winnerSlot,
                    LoserNextMatchCode = loserNext,
                    LoserNextMatchSlot = loserSlot,
                    Igrac1ID = igrac1,
                    Igrac2ID = igrac2
                });
            }

            if (n > 2)
            {
                GenerirajPlasmanRekurzivno(turnir, mecevi, L, L + half - 1, null, startTime.AddDays(1), dubina + 1, isUtjesni);
                GenerirajPlasmanRekurzivno(turnir, mecevi, L + half, R, null, startTime.AddDays(1), dubina + 1, isUtjesni);
            }
        }

        /// <summary>
        /// Generiše utješni bracket popunjen sa 2^n igračima s relativnim plasmanom (1..S_utjesni).
        /// Gubitnici napreduju u utješno razigravanje (UT_PL_).
        /// </summary>
        private void GenerirajUtjesniBracket(
            Turnir turnir, List<Mec> mecevi,
            int S_utjesni,
            List<string?> igraciBaza,
            DateTime startTime)
        {
            int roundsCount = (int)Math.Round(Math.Log2(S_utjesni));

            for (int r = 1; r <= roundsCount; r++)
            {
                int matchesInRound = S_utjesni / (int)Math.Pow(2, r);
                for (int m = 1; m <= matchesInRound; m++)
                {
                    string? winnerNext = null;
                    int? winnerSlot = null;
                    string? loserNext = null;
                    int? loserSlot = null;

                    if (r < roundsCount)
                    {
                        winnerNext = $"UT_R{r + 1}_M{(m + 1) / 2}";
                        winnerSlot = (m % 2 != 0) ? 1 : 2;

                        int plL = S_utjesni / (int)Math.Pow(2, r) + 1;
                        int plR = S_utjesni / (int)Math.Pow(2, r - 1);
                        loserNext = $"UT_PL_{plL}_{plR}_R1_M{(m + 1) / 2}";
                        loserSlot = (m % 2 != 0) ? 1 : 2;
                    }

                    string? igrac1 = null, igrac2 = null;
                    if (r == 1)
                    {
                        igrac1 = igraciBaza[(m - 1) * 2];
                        igrac2 = igraciBaza[(m - 1) * 2 + 1];
                    }

                    mecevi.Add(new Mec
                    {
                        TurnirID = turnir.ID,
                        MatchCode = $"UT_R{r}_M{m}",
                        Runda = r,
                        Odigran = false,
                        TipMeca = TipMeca.Utjesni,
                        PlacingRange = $"1-{S_utjesni}",
                        VrijemeMeca = startTime.AddDays(r - 1).AddHours(m),
                        WinnerNextMatchCode = winnerNext,
                        WinnerNextMatchSlot = winnerSlot,
                        LoserNextMatchCode = loserNext,
                        LoserNextMatchSlot = loserSlot,
                        Igrac1ID = igrac1,
                        Igrac2ID = igrac2
                    });
                }
            }
        }

        private void GenerirajRoundRobin3(
            Turnir turnir, List<Mec> mecevi,
            int startPos, List<string?> players,
            DateTime startTime)
        {
            var p1 = players.Count > 0 ? players[0] : null;
            var p2 = players.Count > 1 ? players[1] : null;
            var p3 = players.Count > 2 ? players[2] : null;

            int endPos = startPos + 2;
            string placingRange = $"{startPos}-{endPos}";

            // Meč 1: P1 vs P2
            mecevi.Add(new Mec
            {
                TurnirID = turnir.ID,
                MatchCode = $"UT_RR_{startPos}_{endPos}_M1",
                Runda = 1,
                Odigran = false,
                TipMeca = TipMeca.Utjesni,
                PlacingRange = placingRange,
                VrijemeMeca = startTime,
                Igrac1ID = p1,
                Igrac2ID = p2
            });

            // Meč 2: P1 vs P3
            mecevi.Add(new Mec
            {
                TurnirID = turnir.ID,
                MatchCode = $"UT_RR_{startPos}_{endPos}_M2",
                Runda = 1,
                Odigran = false,
                TipMeca = TipMeca.Utjesni,
                PlacingRange = placingRange,
                VrijemeMeca = startTime.AddHours(1),
                Igrac1ID = p1,
                Igrac2ID = p3
            });

            // Meč 3: P2 vs P3
            mecevi.Add(new Mec
            {
                TurnirID = turnir.ID,
                MatchCode = $"UT_RR_{startPos}_{endPos}_M3",
                Runda = 1,
                Odigran = false,
                TipMeca = TipMeca.Utjesni,
                PlacingRange = placingRange,
                VrijemeMeca = startTime.AddHours(2),
                Igrac1ID = p2,
                Igrac2ID = p3
            });
        }

        private List<string?> RasporediBye(List<string?> players, int bracketSize, Random rng)
        {
            int pCount = players.Count;
            int byeCount = bracketSize - pCount;
            int pairCount = bracketSize / 2;

            var pairIndices = Enumerable.Range(0, pairCount).OrderBy(_ => rng.Next()).ToList();
            var byePairSet = new HashSet<int>(pairIndices.Take(byeCount));

            var result = new string?[bracketSize];
            int playerIdx = 0;

            for (int i = 0; i < pairCount; i++)
            {
                if (byePairSet.Contains(i))
                {
                    result[2 * i] = players[playerIdx++];
                    result[2 * i + 1] = null;
                }
                else
                {
                    result[2 * i] = players[playerIdx++];
                    result[2 * i + 1] = players[playerIdx++];
                }
            }

            return result.ToList();
        }

        /// <summary>
        /// Kao RasporediBye ali umjesto null stavlja SLOBODAN sentinel.
        /// Osigurava da u jednom paru nema dva SLOBODAN igrača.
        /// </summary>
        private List<string?> RasporediSaSlobodanom(List<string?> players, int bracketSize, Random rng)
        {
            int pCount = players.Count;
            int slobodanCount = bracketSize - pCount;
            int pairCount = bracketSize / 2;

            var result = new string?[bracketSize];
            for (int i = 0; i < bracketSize; i++) result[i] = SLOBODAN;

            if (pCount == 0) return result.ToList();

            var pairIndices = Enumerable.Range(0, pairCount).OrderBy(_ => rng.Next()).ToList();

            if (slobodanCount <= pairCount)
            {
                // slobodanCount parova ima po 1 SLOBODAN i 1 pravog igrača
                // ostalih (pairCount - slobodanCount) parova ima 2 pravog igrača
                var slobodanPairs = new HashSet<int>(pairIndices.Take(slobodanCount));
                int playerIdx = 0;

                for (int i = 0; i < pairCount; i++)
                {
                    if (slobodanPairs.Contains(i))
                    {
                        result[2 * i] = players[playerIdx++];
                        result[2 * i + 1] = SLOBODAN;
                    }
                    else
                    {
                        result[2 * i] = players[playerIdx++];
                        result[2 * i + 1] = players[playerIdx++];
                    }
                }
            }
            else
            {
                // pCount parova ima 1 pravog igrača i 1 SLOBODAN
                // ostalih (pairCount - pCount) parova ima oba SLOBODAN
                var playerPairs = new HashSet<int>(pairIndices.Take(pCount));
                int playerIdx = 0;

                for (int i = 0; i < pairCount; i++)
                {
                    if (playerPairs.Contains(i))
                    {
                        result[2 * i] = players[playerIdx++];
                        result[2 * i + 1] = SLOBODAN;
                    }
                    else
                    {
                        result[2 * i] = SLOBODAN;
                        result[2 * i + 1] = SLOBODAN;
                    }
                }
            }

            return result.ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PROPAGACIJA BYE-OVA
        // ─────────────────────────────────────────────────────────────────────

        private static bool DaLiMecHraniSljedeciSaSlotom(string? nextMatchCodeSpec, int? oldSlot, string targetCode, int targetSlot)
        {
            if (string.IsNullOrEmpty(nextMatchCodeSpec)) return false;
            
            if (nextMatchCodeSpec.Contains(';'))
            {
                var parts = nextMatchCodeSpec.Split(';');
                foreach (var part in parts)
                {
                    if (string.IsNullOrEmpty(part)) continue;
                    var tokens = part.Split(':');
                    if (tokens[0] == targetCode)
                    {
                        int slot = tokens.Length > 1 && int.TryParse(tokens[1], out int s) ? s : 1;
                        if (slot == targetSlot) return true;
                    }
                }
                return false;
            }
            else
            {
                var tokens = nextMatchCodeSpec.Split(':');
                if (tokens[0] == targetCode)
                {
                    int slot = tokens.Length > 1 && int.TryParse(tokens[1], out int s) ? s : (oldSlot ?? 1);
                    return slot == targetSlot;
                }
                return false;
            }
        }

        // Provjeri da li je ID fiktivni "Slobodan" igrač
        public static bool JeSlobodan(string? id) => id == SLOBODAN;

        public void PropagirajBye(List<Mec> mecevi)
        {
            bool promijenjeno = true;
            int limitSigurnosti = 0;

            while (promijenjeno && limitSigurnosti < 500)
            {
                promijenjeno = false;
                limitSigurnosti++;

                foreach (var mec in mecevi.Where(m => !m.Odigran))
                {
                    bool slot1Odredjen = false;
                    bool slot2Odredjen = false;

                    var preduslov1 = mecevi.FirstOrDefault(m =>
                        DaLiMecHraniSljedeciSaSlotom(m.WinnerNextMatchCode, m.WinnerNextMatchSlot, mec.MatchCode, 1) ||
                        DaLiMecHraniSljedeciSaSlotom(m.LoserNextMatchCode, m.LoserNextMatchSlot, mec.MatchCode, 1));

                    var preduslov2 = mecevi.FirstOrDefault(m =>
                        DaLiMecHraniSljedeciSaSlotom(m.WinnerNextMatchCode, m.WinnerNextMatchSlot, mec.MatchCode, 2) ||
                        DaLiMecHraniSljedeciSaSlotom(m.LoserNextMatchCode, m.LoserNextMatchSlot, mec.MatchCode, 2));

                    if (preduslov1 == null || preduslov1.Odigran || mec.Igrac1ID != null)
                        slot1Odredjen = true;
                    if (preduslov2 == null || preduslov2.Odigran || mec.Igrac2ID != null)
                        slot2Odredjen = true;

                    if (slot1Odredjen && slot2Odredjen)
                    {
                        // Meč se auto-rješava ako je jedan od igrača null ili SLOBODAN
                        bool igrac1Slobodan = mec.Igrac1ID == null || JeSlobodan(mec.Igrac1ID);
                        bool igrac2Slobodan = mec.Igrac2ID == null || JeSlobodan(mec.Igrac2ID);

                        if (igrac1Slobodan || igrac2Slobodan)
                        {
                            mec.Odigran = true;

                            string? winnerId = null;
                            string? loserId  = null;
                            string? winnerPartnerId = null;
                            string? loserPartnerId = null;

                            if (!igrac1Slobodan)
                            {
                                // Igrac1 je pravi igrač, Igrac2 je slobodan → igrac1 pobjeđuje
                                if (mec.Igrac2ID == null) mec.Igrac2ID = SLOBODAN;
                                mec.PoeniIgrac1 = 3; mec.PoeniIgrac2 = 0;
                                winnerId = mec.Igrac1ID; 
                                loserId = mec.Igrac2ID;
                                winnerPartnerId = mec.Igrac1PartnerID; 
                                loserPartnerId = mec.Igrac2PartnerID;
                            }
                            else if (!igrac2Slobodan)
                            {
                                // Igrac2 je pravi igrač, Igrac1 je slobodan → igrac2 pobjeđuje
                                if (mec.Igrac1ID == null) mec.Igrac1ID = SLOBODAN;
                                mec.PoeniIgrac1 = 0; mec.PoeniIgrac2 = 3;
                                winnerId = mec.Igrac2ID; 
                                loserId = mec.Igrac1ID;
                                winnerPartnerId = mec.Igrac2PartnerID; 
                                loserPartnerId = mec.Igrac1PartnerID;
                            }
                            else
                            {
                                // Oba su slobodna – SLOBODAN napreduje da sljedeći meč dobije protivnika
                                if (mec.Igrac1ID == null) mec.Igrac1ID = SLOBODAN;
                                if (mec.Igrac2ID == null) mec.Igrac2ID = SLOBODAN;
                                mec.PoeniIgrac1 = 0; mec.PoeniIgrac2 = 0;
                                winnerId = SLOBODAN; 
                                loserId = SLOBODAN;
                            }

                            if (!string.IsNullOrEmpty(mec.WinnerNextMatchCode))
                            {
                                var destinations = mec.WinnerNextMatchCode.Split(';');
                                foreach (var dest in destinations)
                                {
                                    if (string.IsNullOrEmpty(dest)) continue;
                                    var parts = dest.Split(':');
                                    string targetCode = parts[0];
                                    int slot = (parts.Length > 1 && int.TryParse(parts[1], out int s)) ? s : (mec.WinnerNextMatchSlot ?? 1);

                                    var sljedeci = mecevi.FirstOrDefault(m => m.MatchCode == targetCode);
                                    if (sljedeci != null)
                                    {
                                        string? targetPlayerId = winnerId;
                                        string? targetPartnerId = winnerPartnerId;
                                        if (slot == 1 && sljedeci.Igrac1ID != targetPlayerId) {
                                            sljedeci.Igrac1ID = targetPlayerId;
                                            sljedeci.Igrac1PartnerID = targetPartnerId;
                                            promijenjeno = true;
                                        }
                                        else if (slot == 2 && sljedeci.Igrac2ID != targetPlayerId) {
                                            sljedeci.Igrac2ID = targetPlayerId;
                                            sljedeci.Igrac2PartnerID = targetPartnerId;
                                            promijenjeno = true;
                                        }
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(mec.LoserNextMatchCode))
                            {
                                var destinations = mec.LoserNextMatchCode.Split(';');
                                foreach (var dest in destinations)
                                {
                                    if (string.IsNullOrEmpty(dest)) continue;
                                    var parts = dest.Split(':');
                                    string targetCode = parts[0];
                                    int slot = (parts.Length > 1 && int.TryParse(parts[1], out int s)) ? s : (mec.LoserNextMatchSlot ?? 1);

                                    var sljedeci = mecevi.FirstOrDefault(m => m.MatchCode == targetCode);
                                    if (sljedeci != null)
                                    {
                                        string? targetPlayerId = loserId;
                                        string? targetPartnerId = loserPartnerId;
                                        if (slot == 1 && sljedeci.Igrac1ID != targetPlayerId) {
                                            sljedeci.Igrac1ID = targetPlayerId;
                                            sljedeci.Igrac1PartnerID = targetPartnerId;
                                            promijenjeno = true;
                                        }
                                        else if (slot == 2 && sljedeci.Igrac2ID != targetPlayerId) {
                                            sljedeci.Igrac2ID = targetPlayerId;
                                            sljedeci.Igrac2PartnerID = targetPartnerId;
                                            promijenjeno = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PLASMAN
        // ─────────────────────────────────────────────────────────────────────

        public List<TurnirPlasmanViewModel> IzracunajPlasman(Turnir turnir)
        {
            var plasmani = new List<TurnirPlasmanViewModel>();
            if (turnir == null) return plasmani;

            var registracije = turnir.Registracije
                .Where(r => r.KorisnikID != SLOBODAN && r.Korisnik != null && !JeSlobodan(r.Korisnik.Id))
                .ToList();
            if (!registracije.Any()) return plasmani;

            var mecevi           = turnir.Mecevi.ToList();
            var grupniMecevi     = mecevi.Where(m => m.TipMeca == TipMeca.GrupnaFaza).ToList();
            var zavrsniMecevi    = mecevi.Where(m => m.TipMeca == TipMeca.Zavrsnica).ToList();
            var razigravanjeMecevi = mecevi.Where(m => m.TipMeca == TipMeca.Razigravanje).ToList();
            var utjesniMecevi    = mecevi.Where(m => m.TipMeca == TipMeca.Utjesni).ToList();

            var playerRanks = new Dictionary<string, (int Pozicija, int Bodovi, string Detalj)>();

            int brojGrupa = grupniMecevi.Select(m => m.NazivGrupe).Where(n => !string.IsNullOrEmpty(n)).Distinct().Count();
            int offsetUtjesni = brojGrupa > 0 ? (brojGrupa * 2) : 0;
            if (offsetUtjesni == 0 && zavrsniMecevi.Any())
            {
                var zMatches = zavrsniMecevi.Where(m => m.MatchCode.StartsWith("Z_")).ToList();
                offsetUtjesni = zMatches
                    .SelectMany(m => new[] { m.Igrac1ID, m.Igrac2ID })
                    .Where(id => id != null && !JeSlobodan(id))
                    .Distinct()
                    .Count();
            }

            bool imaUtjesni = (turnir.SistemTurnira == SistemTurnira.DoubleEliminationUtjesni) || utjesniMecevi.Any();

            // ── 1. GLAVNA ZAVRŠNICA ────────────────────────────────────────────────
            var zMatchesList = zavrsniMecevi.Where(m => m.MatchCode.StartsWith("Z_")).ToList();
            var zFinale = zMatchesList.FirstOrDefault(m => string.IsNullOrEmpty(m.WinnerNextMatchCode));

            // A) Finale (1. i 2. mjesto)
            if (zFinale != null)
            {
                if (zFinale.Odigran && zFinale.Igrac1ID != null && zFinale.Igrac2ID != null)
                {
                    bool i1Pob = (zFinale.PoeniIgrac1 ?? 0) > (zFinale.PoeniIgrac2 ?? 0);
                    string winId = i1Pob ? zFinale.Igrac1ID : zFinale.Igrac2ID;
                    string loseId = i1Pob ? zFinale.Igrac2ID : zFinale.Igrac1ID;

                    if (!JeSlobodan(winId))
                        playerRanks[winId] = (1, DajBodoveZaPoziciju(1), "1. mjesto 🏆 Pobjednik");
                    if (!JeSlobodan(loseId))
                        playerRanks[loseId] = (2, DajBodoveZaPoziciju(2), "2. mjesto 🥈");
                }
                else
                {
                    if (zFinale.Igrac1ID != null && !JeSlobodan(zFinale.Igrac1ID))
                        playerRanks[zFinale.Igrac1ID] = (1, DajBodoveZaPoziciju(1), "Finale (1-2. mjesto)");
                    if (zFinale.Igrac2ID != null && !JeSlobodan(zFinale.Igrac2ID))
                        playerRanks[zFinale.Igrac2ID] = (2, DajBodoveZaPoziciju(2), "Finale (1-2. mjesto)");
                }
            }

            // B) Plasmani od 3. mjesta do (brojGrupa * 2)
            bool imaPlMeceve = razigravanjeMecevi.Any(m => m.MatchCode.StartsWith("PL_"));
            if (turnir.SistemTurnira == SistemTurnira.SingleElimination && !imaPlMeceve)
            {
                // Single elimination bez razigravanja:
                // Poraženi u istoj rundi dijele isti plasman: Polufinale -> 3, Četvrtfinale -> 5, Osmina -> 9, itd.
                var zPoRundama = zMatchesList.GroupBy(m => m.Runda).OrderByDescending(g => g.Key).ToList();
                int maxRunda = zPoRundama.Any() ? zPoRundama.First().Key : 0;

                foreach (var rundaGroup in zPoRundama)
                {
                    int r = rundaGroup.Key;
                    if (r == maxRunda) continue; // Finale već obrađeno

                    int targetPos = (int)Math.Pow(2, maxRunda - r) + 1;

                    foreach (var m in rundaGroup.Where(m => m.Odigran))
                    {
                        if (m.Igrac1ID != null && m.Igrac2ID != null)
                        {
                            bool i1Pob = (m.PoeniIgrac1 ?? 0) > (m.PoeniIgrac2 ?? 0);
                            string loserId = i1Pob ? m.Igrac2ID : m.Igrac1ID;
                            if (!JeSlobodan(loserId) && !playerRanks.ContainsKey(loserId))
                            {
                                string opisPozicije = targetPos == 3 ? "3. mjesto 🥉 (Polufinale)" : $"{targetPos}. mjesto";
                                playerRanks[loserId] = (targetPos, DajBodoveZaPoziciju(targetPos), opisPozicije);
                            }
                        }
                    }
                }
            }
            else
            {
                // Single elimination sa razigravanjem ILI Double Elimination / Utješni:
                // Plasmani od 3. mjesta naviše dodjeljuju se redom kroz PL_ mečeve razigravanja.
                // AKO JE MEČ BIO SA "SLOBODAN", TAJ PLASMAN DOBIJA SLJEDEĆI STVARNI IGRAČ BEZ PRESKAKANJA POZICIJA!
                var plMatchesList = razigravanjeMecevi
                    .Where(m => m.MatchCode.StartsWith("PL_"))
                    .Select(m => {
                        var (l, r) = ParsiranajeRange(m.PlacingRange);
                        return new { Match = m, L = l, R = r };
                    })
                    .Where(x => x.L > 0 && x.R - x.L == 1)
                    .OrderBy(x => x.L)
                    .ToList();

                int currentPos = 3;
                foreach (var item in plMatchesList)
                {
                    var m = item.Match;
                    List<string> orderedPlayers = new List<string>();
                    if (m.Odigran && m.Igrac1ID != null && m.Igrac2ID != null)
                    {
                        bool i1Pob = (m.PoeniIgrac1 ?? 0) > (m.PoeniIgrac2 ?? 0);
                        orderedPlayers.Add(i1Pob ? m.Igrac1ID : m.Igrac2ID);
                        orderedPlayers.Add(i1Pob ? m.Igrac2ID : m.Igrac1ID);
                    }
                    else
                    {
                        if (m.Igrac1ID != null) orderedPlayers.Add(m.Igrac1ID);
                        if (m.Igrac2ID != null) orderedPlayers.Add(m.Igrac2ID);
                    }

                    foreach (var pid in orderedPlayers)
                    {
                        if (!JeSlobodan(pid) && !playerRanks.ContainsKey(pid))
                        {
                            string opis = currentPos == 3 ? "3. mjesto 🥉" : $"{currentPos}. mjesto";
                            playerRanks[pid] = (currentPos, DajBodoveZaPoziciju(currentPos), opis);
                            currentPos++;
                        }
                    }
                }

                // Ako ima neplaciranih igrača iz glavne završnice, dodijeli im sekvencijalne pozicije
                var neplaciraniZavrsnica = zMatchesList.Concat(razigravanjeMecevi)
                    .SelectMany(m => new[] { m.Igrac1ID, m.Igrac2ID })
                    .Where(id => id != null && !JeSlobodan(id) && !playerRanks.ContainsKey(id))
                    .Select(id => id!)
                    .Distinct()
                    .ToList();

                foreach (var pid in neplaciraniZavrsnica)
                {
                    playerRanks[pid] = (currentPos, DajBodoveZaPoziciju(currentPos), $"{currentPos}. mjesto");
                    currentPos++;
                }
            }

            // ── 2. GRUPNA FAZA / UTJEŠNI TURNIR ───────────────────────────────────
            int grupnaFazaPos = offsetUtjesni > 0 ? (offsetUtjesni + 1) : 1;

            var grupnaFazaIds = registracije.Select(r => r.KorisnikID)
                .Where(id => !playerRanks.ContainsKey(id) && !JeSlobodan(id)).ToList();

            // Detektuj "group-only" turnir: samo 1 grupa sa svim igračima, bez završnice i utješnog
            bool isGroupOnly = !zavrsniMecevi.Any() && !utjesniMecevi.Any() && grupniMecevi.Any() && brojGrupa == 1;

            if (isGroupOnly)
            {
                // Turnir sa jednom grupom (npr. 5 igrača) – grupni poredak je konačni plasman
                var groupStats = new List<(string PlayerId, int Wins, int SetDiff, int SetsWon)>();
                foreach (var pid in grupnaFazaIds)
                {
                    int wins = 0, setsWon = 0, setsLost = 0;
                    var igracMecevi = grupniMecevi
                        .Where(m => m.Odigran && (m.Igrac1ID == pid || m.Igrac2ID == pid));
                    foreach (var m in igracMecevi)
                    {
                        if (m.Igrac1ID == pid)
                        {
                            setsWon  += m.PoeniIgrac1 ?? 0;
                            setsLost += m.PoeniIgrac2 ?? 0;
                            if ((m.PoeniIgrac1 ?? 0) > (m.PoeniIgrac2 ?? 0)) wins++;
                        }
                        else
                        {
                            setsWon  += m.PoeniIgrac2 ?? 0;
                            setsLost += m.PoeniIgrac1 ?? 0;
                            if ((m.PoeniIgrac2 ?? 0) > (m.PoeniIgrac1 ?? 0)) wins++;
                        }
                    }
                    groupStats.Add((pid, wins, setsWon - setsLost, setsWon));
                }

                var sortedGroup = groupStats
                    .OrderByDescending(x => x.Wins)
                    .ThenByDescending(x => x.SetDiff)
                    .ThenByDescending(x => x.SetsWon)
                    .ToList();

                int pos = 1;
                foreach (var item in sortedGroup)
                {
                    string opis = pos switch
                    {
                        1 => "1. mjesto 🏆 Pobjednik",
                        2 => "2. mjesto 🥈",
                        3 => "3. mjesto 🥉",
                        _ => $"{pos}. mjesto"
                    };
                    playerRanks[item.PlayerId] = (pos, DajBodoveZaPoziciju(pos), opis);
                    pos++;
                }
            }
            else if (!imaUtjesni)
            {
                // Bez utješnog turnira: svi ostali igrači koji su ispali u grupnoj fazi dijele isti plasman (brojGrupa * 2 + 1)
                foreach (var pid in grupnaFazaIds)
                {
                    playerRanks[pid] = (grupnaFazaPos, DajBodoveZaPoziciju(grupnaFazaPos), $"{grupnaFazaPos}. mjesto (Grupna faza)");
                }
            }
            else
            {
                // Sa utješnim turnirom: na osnovu utješnog turnira i njegovog razigravanja dodjeljuju se plasmani od (brojGrupa * 2 + 1) do (brojIgraca)
                int utjesniCurrentPos = grupnaFazaPos;
                var utMatches = utjesniMecevi.ToList();

                // A) Round-robin utješni (ako ima 3 igrača)
                var rrMatches = utMatches.Where(m => m.MatchCode.StartsWith("UT_RR_")).ToList();
                if (rrMatches.Any())
                {
                    var groupList = rrMatches.ToList();
                    var playersInGroup = groupList
                        .SelectMany(m => new[] { m.Igrac1ID, m.Igrac2ID })
                        .Where(id => id != null && !JeSlobodan(id))
                        .Select(id => id!)
                        .Distinct()
                        .ToList();

                    var stats = new List<(string PlayerId, int Wins, int SetDiff, int SetsWon)>();
                    foreach (var playerId in playersInGroup)
                    {
                        int wins = 0, setsWon = 0, setsLost = 0;
                        foreach (var m in groupList.Where(m => m.Odigran))
                        {
                            if (m.Igrac1ID == playerId)
                            {
                                setsWon += m.PoeniIgrac1 ?? 0;
                                setsLost += m.PoeniIgrac2 ?? 0;
                                if ((m.PoeniIgrac1 ?? 0) > (m.PoeniIgrac2 ?? 0)) wins++;
                            }
                            else if (m.Igrac2ID == playerId)
                            {
                                setsWon += m.PoeniIgrac2 ?? 0;
                                setsLost += m.PoeniIgrac1 ?? 0;
                                if ((m.PoeniIgrac2 ?? 0) > (m.PoeniIgrac1 ?? 0)) wins++;
                            }
                        }
                        stats.Add((playerId, wins, setsWon - setsLost, setsWon));
                    }

                    var sortedStats = stats
                        .OrderByDescending(s => s.Wins)
                        .ThenByDescending(s => s.SetDiff)
                        .ThenByDescending(s => s.SetsWon)
                        .ToList();

                    foreach (var item in sortedStats)
                    {
                        if (!playerRanks.ContainsKey(item.PlayerId))
                        {
                            playerRanks[item.PlayerId] = (utjesniCurrentPos, DajBodoveZaPoziciju(utjesniCurrentPos), $"{utjesniCurrentPos}. mjesto");
                            utjesniCurrentPos++;
                        }
                    }
                }
                else
                {
                    // B) Utješno finale
                    var utFinale = utMatches.FirstOrDefault(m => m.MatchCode.StartsWith("UT_R") && string.IsNullOrEmpty(m.WinnerNextMatchCode));
                    if (utFinale != null)
                    {
                        List<string> orderedUtFinalists = new List<string>();
                        if (utFinale.Odigran && utFinale.Igrac1ID != null && utFinale.Igrac2ID != null)
                        {
                            bool i1Pob = (utFinale.PoeniIgrac1 ?? 0) > (utFinale.PoeniIgrac2 ?? 0);
                            orderedUtFinalists.Add(i1Pob ? utFinale.Igrac1ID : utFinale.Igrac2ID);
                            orderedUtFinalists.Add(i1Pob ? utFinale.Igrac2ID : utFinale.Igrac1ID);
                        }
                        else
                        {
                            if (utFinale.Igrac1ID != null) orderedUtFinalists.Add(utFinale.Igrac1ID);
                            if (utFinale.Igrac2ID != null) orderedUtFinalists.Add(utFinale.Igrac2ID);
                        }

                        bool isFirst = true;
                        foreach (var pid in orderedUtFinalists)
                        {
                            if (!JeSlobodan(pid) && !playerRanks.ContainsKey(pid))
                            {
                                string opis = isFirst ? $"{utjesniCurrentPos}. mjesto (Pobjednik utješnog)" : $"{utjesniCurrentPos}. mjesto";
                                playerRanks[pid] = (utjesniCurrentPos, DajBodoveZaPoziciju(utjesniCurrentPos), opis);
                                utjesniCurrentPos++;
                                isFirst = false;
                            }
                        }
                    }

                    // C) Utješno razigravanje (SAMO finalni UT_PL_ mečevi za tačne pozicije)
                    var utPlMatches = utMatches
                        .Where(m => m.MatchCode.StartsWith("UT_PL_"))
                        .Select(m => {
                            var (l, r) = ParsiranajeRange(m.PlacingRange);
                            return new { Match = m, L = l, R = r };
                        })
                        .Where(x => x.L > 0 && x.R - x.L == 1)
                        .OrderBy(x => x.L)
                        .ToList();

                    foreach (var item in utPlMatches)
                    {
                        var m = item.Match;
                        List<string> orderedPlayers = new List<string>();
                        if (m.Odigran && m.Igrac1ID != null && m.Igrac2ID != null)
                        {
                            bool i1Pob = (m.PoeniIgrac1 ?? 0) > (m.PoeniIgrac2 ?? 0);
                            orderedPlayers.Add(i1Pob ? m.Igrac1ID : m.Igrac2ID);
                            orderedPlayers.Add(i1Pob ? m.Igrac2ID : m.Igrac1ID);
                        }
                        else
                        {
                            if (m.Igrac1ID != null) orderedPlayers.Add(m.Igrac1ID);
                            if (m.Igrac2ID != null) orderedPlayers.Add(m.Igrac2ID);
                        }

                        foreach (var pid in orderedPlayers)
                        {
                            if (!JeSlobodan(pid) && !playerRanks.ContainsKey(pid))
                            {
                                playerRanks[pid] = (utjesniCurrentPos, DajBodoveZaPoziciju(utjesniCurrentPos), $"{utjesniCurrentPos}. mjesto");
                                utjesniCurrentPos++;
                            }
                        }
                    }
                }

                // D) Ostali nerangirani igrači koji su ispali u grupnoj fazi
                var preostaliGrupnaFaza = registracije.Select(r => r.KorisnikID)
                    .Where(id => !playerRanks.ContainsKey(id) && !JeSlobodan(id)).ToList();

                if (preostaliGrupnaFaza.Any())
                {
                    var groupStats = new List<(string PlayerId, int Wins, int SetDiff, int SetsWon)>();
                    foreach (var playerId in preostaliGrupnaFaza)
                    {
                        int wins = 0, setsWon = 0, setsLost = 0;
                        var igracMecevi = grupniMecevi
                            .Where(m => m.Odigran && (m.Igrac1ID == playerId || m.Igrac2ID == playerId));

                        foreach (var m in igracMecevi)
                        {
                            if (m.Igrac1ID == playerId)
                            {
                                setsWon += m.PoeniIgrac1 ?? 0;
                                setsLost += m.PoeniIgrac2 ?? 0;
                                if ((m.PoeniIgrac1 ?? 0) > (m.PoeniIgrac2 ?? 0)) wins++;
                            }
                            else
                            {
                                setsWon += m.PoeniIgrac2 ?? 0;
                                setsLost += m.PoeniIgrac1 ?? 0;
                                if ((m.PoeniIgrac2 ?? 0) > (m.PoeniIgrac1 ?? 0)) wins++;
                            }
                        }
                        groupStats.Add((playerId, wins, setsWon - setsLost, setsWon));
                    }

                    var sortedGroup = groupStats
                        .OrderByDescending(x => x.Wins)
                        .ThenByDescending(x => x.SetDiff)
                        .ThenByDescending(x => x.SetsWon)
                        .ToList();

                    foreach (var item in sortedGroup)
                    {
                        if (!playerRanks.ContainsKey(item.PlayerId))
                        {
                            playerRanks[item.PlayerId] = (utjesniCurrentPos, DajBodoveZaPoziciju(utjesniCurrentPos), $"{utjesniCurrentPos}. mjesto (Grupna faza)");
                            utjesniCurrentPos++;
                        }
                    }
                }
            }

            // ── 3. GRADI FINALNU LISTU PLASMANA ─────────────────────────────────────────
            foreach (var reg in registracije)
            {
                var user = reg.Korisnik;
                if (user == null || JeSlobodan(user.Id)) continue;

                if (playerRanks.TryGetValue(user.Id, out var rank))
                {
                    plasmani.Add(new TurnirPlasmanViewModel
                    {
                        KorisnikId = user.Id,
                        Korisnik = user,
                        ImePrezime = $"{user.Ime} {user.Prezime}",
                        Grad = user.Grad ?? "",
                        IsGost = user.IsGost,
                        Pozicija = rank.Pozicija,
                        Bodovi = rank.Bodovi,
                        DetaljPozicije = rank.Detalj
                    });
                }
                else
                {
                    plasmani.Add(new TurnirPlasmanViewModel
                    {
                        KorisnikId = user.Id,
                        Korisnik = user,
                        ImePrezime = $"{user.Ime} {user.Prezime}",
                        Grad = user.Grad ?? "",
                        IsGost = user.IsGost,
                        Pozicija = grupnaFazaPos,
                        Bodovi = DajBodoveZaPoziciju(grupnaFazaPos),
                        DetaljPozicije = "Učešće"
                    });
                }
            }

            return plasmani.OrderBy(p => p.Pozicija).ToList();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  POMOĆNE METODE
        // ─────────────────────────────────────────────────────────────────────

        private static (int L, int R) ParsiranajeRange(string? range)
        {
            if (string.IsNullOrEmpty(range)) return (0, 0);
            var parts = range.Split('-');
            if (parts.Length == 2
                && int.TryParse(parts[0], out int L)
                && int.TryParse(parts[1], out int R))
                return (L, R);
            return (0, 0);
        }

        private static int DajBodoveZaPoziciju(int pozicija) => pozicija switch
        {
            1  => 100,
            2  => 80,
            3  => 70,
            4  => 60,
            5  => 55,
            6  => 50,
            7  => 45,
            8  => 40,
            9  => 35,
            10 => 32,
            11 => 30,
            12 => 28,
            13 => 26,
            14 => 24,
            15 => 22,
            16 => 20,
            _ when pozicija <= 32 => 10,
            _ => 5
        };

        private static List<int> GetGroupSizes(int playerCount)
        {
            if (playerCount < 3)
                return new List<int>();

            if (playerCount == 5)
                return new List<int> { 5 };

            int groupCount = (playerCount + 3) / 4;
            int remainder = playerCount % 4;

            if (remainder == 0)
            {
                return Enumerable.Repeat(4, groupCount).ToList();
            }

            if (remainder == 1)
            {
                // Convert the last three groups to size 3 to avoid a group of size 1.
                return Enumerable.Repeat(4, groupCount - 3)
                    .Concat(Enumerable.Repeat(3, 3))
                    .ToList();
            }

            if (remainder == 2)
            {
                // Convert the last two groups to size 3.
                return Enumerable.Repeat(4, groupCount - 2)
                    .Concat(Enumerable.Repeat(3, 2))
                    .ToList();
            }

            // remainder == 3
            return Enumerable.Repeat(4, groupCount - 1)
                .Concat(new[] { 3 })
                .ToList();
        }

        private static List<int> GetMastersGroupSizes(int playerCount)
        {
            if (playerCount < 3)
                return new List<int>();

            int groupCount = (playerCount + 6) / 7;
            int baseSize = playerCount / groupCount;
            int remainder = playerCount % groupCount;

            var sizes = new List<int>();
            for (int i = 0; i < groupCount; i++)
            {
                sizes.Add(baseSize + (i < remainder ? 1 : 0));
            }

            return sizes;
        }

        private static List<List<string>> DistributeOrderedPlayersIntoGroups(List<string> players, List<int> groupSizes)
        {
            var groups = new List<List<string>>();
            for (int i = 0; i < groupSizes.Count; i++)
            {
                groups.Add(new List<string>());
            }

            int index = 0;
            foreach (var player in players)
            {
                if (index >= groupSizes.Count) index = 0;
                groups[index].Add(player);
                if (groups[index].Count >= groupSizes[index])
                {
                    index++;
                }
            }

            return groups;
        }

        private class PlayerRecord
        {
            public string PlayerID      { get; set; } = string.Empty;
            public string NazivGrupe   { get; set; } = string.Empty;
            public int    GroupIndex   { get; set; }
            public int    Pobjede      { get; set; }
            public int    SetRazlika   { get; set; }
            public int    OsvojeniSetovi { get; set; }
            public bool   HasBye       { get; set; }
        }

        private class SlotState
        {
            public bool IsReal { get; set; }
            public string? SourceMatchCode { get; set; }
            public bool IsWinner { get; set; }
            public string? PlayerID { get; set; }
        }

        public List<Mec> GenerirajTurnirParova(Turnir turnir, List<TurnirPar> parovi)
        {
            var mecevi = new List<Mec>();
            int N = parovi.Count;
            if (N < 2) return mecevi;

            DateTime pocetak = DateTime.Now;
            if (turnir.Mecevi != null && turnir.Mecevi.Any())
            {
                pocetak = turnir.Mecevi.Max(m => m.VrijemeMeca).AddHours(2);
            }

            // Specijalni slučaj: točno 3 para → round-robin (svaki sa svakim)
            if (N == 3)
            {
                var rngRr = new Random();
                var shuffled3 = parovi.OrderBy(_ => rngRr.Next()).ToList();
                // Prikazujemo kao grupnu fazu s nazivom grupe kako bi view znao
                mecevi.Add(new Mec { TurnirID = turnir.ID, MatchCode = "PAR_RR_M1", Runda = 1, Odigran = false, TipMeca = TipMeca.TurnirParova, VrijemeMeca = pocetak, PlacingRange = "1-3", NazivGrupe = "Grupa A (Parovi)", Igrac1ID = shuffled3[0].Igrac1ID, Igrac1PartnerID = shuffled3[0].Igrac2ID, Igrac2ID = shuffled3[1].Igrac1ID, Igrac2PartnerID = shuffled3[1].Igrac2ID });
                mecevi.Add(new Mec { TurnirID = turnir.ID, MatchCode = "PAR_RR_M2", Runda = 1, Odigran = false, TipMeca = TipMeca.TurnirParova, VrijemeMeca = pocetak.AddHours(1), PlacingRange = "1-3", NazivGrupe = "Grupa A (Parovi)", Igrac1ID = shuffled3[0].Igrac1ID, Igrac1PartnerID = shuffled3[0].Igrac2ID, Igrac2ID = shuffled3[2].Igrac1ID, Igrac2PartnerID = shuffled3[2].Igrac2ID });
                mecevi.Add(new Mec { TurnirID = turnir.ID, MatchCode = "PAR_RR_M3", Runda = 1, Odigran = false, TipMeca = TipMeca.TurnirParova, VrijemeMeca = pocetak.AddHours(2), PlacingRange = "1-3", NazivGrupe = "Grupa A (Parovi)", Igrac1ID = shuffled3[1].Igrac1ID, Igrac1PartnerID = shuffled3[1].Igrac2ID, Igrac2ID = shuffled3[2].Igrac1ID, Igrac2PartnerID = shuffled3[2].Igrac2ID });
                return mecevi;
            }

            // Find next power of 2
            int S = 2;
            while (S < N) S *= 2;

            // Shuffle pairs randomly
            var rng = new Random();
            var shuffledPairs = parovi.OrderBy(_ => rng.Next()).ToList();

            // Prepare list of pair identifiers with SLOBODAN padding
            var pairIdentifiers = Enumerable.Range(0, N).Select(i => (string?)i.ToString()).ToList();
            var distributed = RasporediSaSlobodanom(pairIdentifiers, S, rng);

            int roundsCount = (int)Math.Round(Math.Log2(S));

            // Generate all matches
            for (int r = 1; r <= roundsCount; r++)
            {
                int matchesInRound = S / (int)Math.Pow(2, r);
                for (int m = 1; m <= matchesInRound; m++)
                {
                    var mec = new Mec
                    {
                        TurnirID = turnir.ID,
                        MatchCode = $"PAR_R{r}_M{m}",
                        Runda = r,
                        Odigran = false,
                        TipMeca = TipMeca.TurnirParova,
                        VrijemeMeca = pocetak.AddDays(r - 1).AddHours(m * 2),
                        PlacingRange = $"1-{S}"
                    };

                    if (r < roundsCount)
                    {
                        mec.WinnerNextMatchCode = $"PAR_R{r + 1}_M{(m + 1) / 2}";
                        mec.WinnerNextMatchSlot = (m % 2 != 0) ? 1 : 2;
                    }

                    mecevi.Add(mec);
                }
            }

            // Populate Round 1 with pairs based on the distribution
            for (int i = 0; i < S / 2; i++)
            {
                var mecR1 = mecevi.FirstOrDefault(m => m.MatchCode == $"PAR_R1_M{i + 1}");
                if (mecR1 != null)
                {
                    var key1 = distributed[2 * i];
                    var key2 = distributed[2 * i + 1];

                    if (key1 != null && !JeSlobodan(key1))
                    {
                        var pair1 = shuffledPairs[int.Parse(key1)];
                        mecR1.Igrac1ID = pair1.Igrac1ID;
                        mecR1.Igrac1PartnerID = pair1.Igrac2ID;
                    }
                    else if (JeSlobodan(key1))
                    {
                        mecR1.Igrac1ID = SLOBODAN;
                    }

                    if (key2 != null && !JeSlobodan(key2))
                    {
                        var pair2 = shuffledPairs[int.Parse(key2)];
                        mecR1.Igrac2ID = pair2.Igrac1ID;
                        mecR1.Igrac2PartnerID = pair2.Igrac2ID;
                    }
                    else if (JeSlobodan(key2))
                    {
                        mecR1.Igrac2ID = SLOBODAN;
                    }
                }
            }

            // Propagate BYEs and Slobodan auto-losses
            PropagirajBye(mecevi);

            return mecevi;
        }
    }
}
