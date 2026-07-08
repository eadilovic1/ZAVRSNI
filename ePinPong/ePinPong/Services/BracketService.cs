using ePinPong.Models;
using ePinPong.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ePinPong.Services
{
    public class BracketService : IBracketService
    {
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

        public List<Mec> GenerirajGrupe(Turnir turnir, List<string> igracIds)
        {
            var mecevi = new List<Mec>();
            int N = igracIds.Count;
            if (N < 3 || N == 5) return mecevi;

            // Calculate x (groups of 4) and y (groups of 3)
            int x = 0;
            int y = 0;
            bool pronadjeno = false;

            for (int candX = N / 4; candX >= 0; candX--)
            {
                int ostalo = N - (candX * 4);
                if (ostalo % 3 == 0)
                {
                    x = candX;
                    y = ostalo / 3;
                    pronadjeno = true;
                    break;
                }
            }

            if (!pronadjeno) return mecevi;

            int ukGrupa = x + y;

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

            // Check if pot sizes are valid. If not, use fallback random distribution
            if (pot1.Count == ukGrupa && pot2.Count == ukGrupa && pot3.Count == ukGrupa && pot4.Count == x)
            {
                // Shuffle each pot to make the draw random
                var shuffledPot1 = pot1.OrderBy(a => rng.Next()).ToList();
                var shuffledPot2 = pot2.OrderBy(a => rng.Next()).ToList();
                var shuffledPot3 = pot3.OrderBy(a => rng.Next()).ToList();
                var shuffledPot4 = pot4.OrderBy(a => rng.Next()).ToList();

                // Draw for each group
                for (int g = 0; g < ukGrupa; g++)
                {
                    grupeIgraca[g].Add(shuffledPot1[g]);
                    grupeIgraca[g].Add(shuffledPot2[g]);
                    grupeIgraca[g].Add(shuffledPot3[g]);
                    if (g < x)
                    {
                        grupeIgraca[g].Add(shuffledPot4[g]);
                    }
                }
            }
            else
            {
                // Fallback: completely random unseeded distribution
                var shuffledIgracIds = igracIds.OrderBy(a => rng.Next()).ToList();
                int igracIdx = 0;
                for (int g = 0; g < x; g++)
                    for (int i = 0; i < 4 && igracIdx < N; i++)
                        grupeIgraca[g].Add(shuffledIgracIds[igracIdx++]);

                for (int g = x; g < ukGrupa; g++)
                    for (int i = 0; i < 3 && igracIdx < N; i++)
                        grupeIgraca[g].Add(shuffledIgracIds[igracIdx++]);
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

                        // Gubitnik ide u razigravanje za mjesta
                        int plL = S / (int)Math.Pow(2, r) + 1;
                        int plR = S / (int)Math.Pow(2, r - 1);
                        mec.LoserNextMatchCode = $"PL_{plL}_{plR}_R1_M{(m + 1) / 2}";
                        mec.LoserNextMatchSlot = (m % 2 != 0) ? 1 : 2;
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
                    mecR1.Igrac1ID = pair.Item1.PlayerID;
                    mecR1.Igrac2ID = pair.Item2?.PlayerID;
                }
            }

            // ── 5.b Generiraj utješni turnir (ako ima igrača) ──────────────────
            var rngUt = new Random();
            var utjesniPlayersShuffled = utjesniIgraciRecords
                .Select(r => (string?)r.PlayerID)
                .OrderBy(_ => rngUt.Next())
                .ToList();
            int utjesniIgraciCount = utjesniPlayersShuffled.Count;

            if (utjesniIgraciCount > 0)
            {
                int L_utjesni = S + 1;
                int R_utjesni_actual = L_utjesni + utjesniIgraciCount - 1;

                var meceviUtjesnog = new List<Mec>();

                if (utjesniIgraciCount == 3)
                {
                    // Specijalni slučaj: točno 3 igrača → round-robin (svaki sa svakim)
                    GenerirajRoundRobin3(turnir, meceviUtjesnog, L_utjesni, utjesniPlayersShuffled, pocetakZavrsnice);
                }
                else
                {
                    int S_utjesni = 2;
                    while (S_utjesni < utjesniIgraciCount) S_utjesni *= 2;

                    // Rasporedi igrače u parove bez BYE vs BYE u R1
                    var finalList = RasporediBye(utjesniPlayersShuffled, S_utjesni, rngUt);

                    int R_utjesni = L_utjesni + S_utjesni - 1;
                    GenerirajUtjesniRekurzivno(turnir, meceviUtjesnog, L_utjesni, R_utjesni, finalList, pocetakZavrsnice, 0, R_utjesni_actual);
                }

                sviMecevi.AddRange(meceviUtjesnog);
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
        /// </summary>
        public List<Mec> GenerirajPlasmanFazu(Turnir turnir, int L, int R, List<string?> gubitnici, List<Mec> postojeciMecevi)
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
            GenerirajPlasmanRekurzivno(turnir, noviMecevi, L, R, shuffled, startTime, 0);

            // Propagiraj BYE-ove kroz novostvorene mečeve
            var sviKombinovani = postojeciMecevi.Concat(noviMecevi).ToList();
            PropagirajBye(sviKombinovani);

            return noviMecevi;
        }

        /// <summary>
        /// Rekurzivno kreira PL_ bracket i dodjeljuje igrače u R1.
        /// </summary>
        private void GenerirajPlasmanRekurzivno(
            Turnir turnir, List<Mec> mecevi,
            int L, int R,
            List<string?>? igraciBaza, // samo za R1, null za dublje runde
            DateTime startTime, int dubina)
        {
            int n = R - L + 1;
            if (n < 2) return;

            int half = n / 2;

            for (int m = 1; m <= half; m++)
            {
                int subMatchIdx = (m + 1) / 2;
                int subSlot = (m % 2 != 0) ? 1 : 2;

                string? winnerNext = null, loserNext = null;
                int? winnerSlot = null, loserSlot = null;

                if (n > 2)
                {
                    winnerNext = $"PL_{L}_{L + half - 1}_R1_M{subMatchIdx}";
                    winnerSlot = subSlot;
                    loserNext = $"PL_{L + half}_{R}_R1_M{subMatchIdx}";
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
                    MatchCode = $"PL_{L}_{R}_R1_M{m}",
                    Runda = 1,
                    Odigran = false,
                    TipMeca = TipMeca.Razigravanje,
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
                GenerirajPlasmanRekurzivno(turnir, mecevi, L, L + half - 1, null, startTime.AddDays(1), dubina + 1);
                GenerirajPlasmanRekurzivno(turnir, mecevi, L + half, R, null, startTime.AddDays(1), dubina + 1);
            }
        }

        /// <summary>
        /// Rekurzivno kreira UT_ (utješni) bracket i dodjeljuje igrače u R1.
        /// </summary>
        private void GenerirajUtjesniRekurzivno(
            Turnir turnir, List<Mec> mecevi,
            int L, int R,
            List<string?>? igraciBaza,
            DateTime startTime, int dubina, int actualMaxPos)
        {
            if (L > actualMaxPos) return;

            int actualCount = actualMaxPos - L + 1;
            if (actualCount == 5)
            {
                GenerirajUtjesni5(turnir, mecevi, L, igraciBaza, startTime, dubina);
                return;
            }

            int n = R - L + 1;
            if (n < 2) return;

            int half = n / 2;

            for (int m = 1; m <= half; m++)
            {
                int subMatchIdx = (m + 1) / 2;
                int subSlot = (m % 2 != 0) ? 1 : 2;

                string? winnerNext = null, loserNext = null;
                int? winnerSlot = null, loserSlot = null;

                if (n > 2)
                {
                    winnerNext = $"UT_{L}_{L + half - 1}_R1_M{subMatchIdx}";
                    winnerSlot = subSlot;

                    if (L + half <= actualMaxPos)
                    {
                        int targetCount = actualMaxPos - (L + half) + 1;
                        if (targetCount == 5)
                        {
                            if (m <= 4)
                            {
                                loserNext = $"UT_{L + half}_{L + half + 4}_R1_M{subMatchIdx}";
                                loserSlot = subSlot;
                            }
                            else if (m == 5)
                            {
                                loserNext = $"UT_RR_{L + half}_{L + half + 2}_M2:2;UT_RR_{L + half}_{L + half + 2}_M3:2";
                                loserSlot = 2;
                            }
                        }
                        else
                        {
                            loserNext = $"UT_{L + half}_{R}_R1_M{subMatchIdx}";
                            loserSlot = subSlot;
                        }
                    }
                }

                string? igrac1 = null, igrac2 = null;
                if (igraciBaza != null && dubina == 0)
                {
                    igrac1 = igraciBaza[(m - 1) * 2];
                    igrac2 = igraciBaza[(m - 1) * 2 + 1];
                }

                mecevi.Add(new Mec
                {
                    TurnirID = turnir.ID,
                    MatchCode = $"UT_{L}_{R}_R1_M{m}",
                    Runda = 1,
                    Odigran = false,
                    TipMeca = TipMeca.Utjesni,
                    PlacingRange = $"{L}-{Math.Min(R, actualMaxPos)}",
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
                GenerirajUtjesniRekurzivno(turnir, mecevi, L, L + half - 1, null, startTime.AddDays(1), dubina + 1, actualMaxPos);
                GenerirajUtjesniRekurzivno(turnir, mecevi, L + half, R, null, startTime.AddDays(1), dubina + 1, actualMaxPos);
            }
        }

        private void GenerirajUtjesni5(
            Turnir turnir, List<Mec> mecevi,
            int L, List<string?>? igraciBaza,
            DateTime startTime, int dubina)
        {
            string? p1 = null, p2 = null, p3 = null, p4 = null, p5 = null;
            if (igraciBaza != null && dubina == 0)
            {
                p1 = igraciBaza.Count > 0 ? igraciBaza[0] : null;
                p2 = igraciBaza.Count > 1 ? igraciBaza[1] : null;
                p3 = igraciBaza.Count > 2 ? igraciBaza[2] : null;
                p4 = igraciBaza.Count > 3 ? igraciBaza[3] : null;
                p5 = igraciBaza.Count > 4 ? igraciBaza[4] : null;
            }

            string winnerNext1 = $"UT_RR_{L}_{L + 2}_M1:1;UT_RR_{L}_{L + 2}_M2:1";
            string loserNext1 = $"UT_{L + 3}_{L + 4}_M1:1";

            mecevi.Add(new Mec
            {
                TurnirID = turnir.ID,
                MatchCode = $"UT_{L}_{L + 4}_R1_M1",
                Runda = 1,
                Odigran = false,
                TipMeca = TipMeca.Utjesni,
                PlacingRange = $"{L}-{L + 4}",
                VrijemeMeca = startTime,
                WinnerNextMatchCode = winnerNext1,
                LoserNextMatchCode = loserNext1,
                Igrac1ID = p1,
                Igrac2ID = p2
            });

            string winnerNext2 = $"UT_RR_{L}_{L + 2}_M1:2;UT_RR_{L}_{L + 2}_M3:1";
            string loserNext2 = $"UT_{L + 3}_{L + 4}_M1:2";

            mecevi.Add(new Mec
            {
                TurnirID = turnir.ID,
                MatchCode = $"UT_{L}_{L + 4}_R1_M2",
                Runda = 1,
                Odigran = false,
                TipMeca = TipMeca.Utjesni,
                PlacingRange = $"{L}-{L + 4}",
                VrijemeMeca = startTime.AddHours(1),
                WinnerNextMatchCode = winnerNext2,
                LoserNextMatchCode = loserNext2,
                Igrac1ID = p3,
                Igrac2ID = p4
            });

            string rrRange = $"{L}-{L + 2}";
            
            mecevi.Add(new Mec
            {
                TurnirID = turnir.ID,
                MatchCode = $"UT_RR_{L}_{L + 2}_M1",
                Runda = 2,
                Odigran = false,
                TipMeca = TipMeca.Utjesni,
                PlacingRange = rrRange,
                VrijemeMeca = startTime.AddDays(1),
                Igrac1ID = null,
                Igrac2ID = null
            });

            mecevi.Add(new Mec
            {
                TurnirID = turnir.ID,
                MatchCode = $"UT_RR_{L}_{L + 2}_M2",
                Runda = 2,
                Odigran = false,
                TipMeca = TipMeca.Utjesni,
                PlacingRange = rrRange,
                VrijemeMeca = startTime.AddDays(1).AddHours(1),
                Igrac1ID = null,
                Igrac2ID = p5
            });

            mecevi.Add(new Mec
            {
                TurnirID = turnir.ID,
                MatchCode = $"UT_RR_{L}_{L + 2}_M3",
                Runda = 2,
                Odigran = false,
                TipMeca = TipMeca.Utjesni,
                PlacingRange = rrRange,
                VrijemeMeca = startTime.AddDays(1).AddHours(2),
                Igrac1ID = null,
                Igrac2ID = p5
            });

            mecevi.Add(new Mec
            {
                TurnirID = turnir.ID,
                MatchCode = $"UT_{L + 3}_{L + 4}_M1",
                Runda = 2,
                Odigran = false,
                TipMeca = TipMeca.Utjesni,
                PlacingRange = $"{L + 3}-{L + 4}",
                VrijemeMeca = startTime.AddDays(1).AddHours(3),
                Igrac1ID = null,
                Igrac2ID = null
            });
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

        private void PropagirajBye(List<Mec> mecevi)
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
                        if (mec.Igrac1ID == null || mec.Igrac2ID == null)
                        {
                            mec.Odigran = true;

                            string? winnerId = null;
                            string? loserId  = null;
                            string? winnerPartnerId = null;
                            string? loserPartnerId = null;

                            if (mec.Igrac1ID != null)
                            {
                                mec.PoeniIgrac1 = 3; mec.PoeniIgrac2 = 0;
                                winnerId = mec.Igrac1ID; loserId = null;
                                winnerPartnerId = mec.Igrac1PartnerID; loserPartnerId = null;
                            }
                            else if (mec.Igrac2ID != null)
                            {
                                mec.PoeniIgrac1 = 0; mec.PoeniIgrac2 = 3;
                                winnerId = mec.Igrac2ID; loserId = null;
                                winnerPartnerId = mec.Igrac2PartnerID; loserPartnerId = null;
                            }
                            else
                            {
                                mec.PoeniIgrac1 = 0; mec.PoeniIgrac2 = 0;
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

            var registracije = turnir.Registracije.ToList();
            if (!registracije.Any()) return plasmani;

            var mecevi           = turnir.Mecevi.ToList();
            var grupniMecevi     = mecevi.Where(m => m.TipMeca == TipMeca.GrupnaFaza).ToList();
            var zavrsniMecevi    = mecevi.Where(m => m.TipMeca == TipMeca.Zavrsnica).ToList();
            var razigravanjeMecevi = mecevi.Where(m => m.TipMeca == TipMeca.Razigravanje).ToList();
            var utjesniMecevi    = mecevi.Where(m => m.TipMeca == TipMeca.Utjesni).ToList();

            var playerRanks = new Dictionary<string, (int Pozicija, int Bodovi, string Detalj)>();

            bool imaZavrsnicu = zavrsniMecevi.Any() || razigravanjeMecevi.Any() || utjesniMecevi.Any();
            if (imaZavrsnicu)
            {
                var zMatches  = zavrsniMecevi.Where(m => m.MatchCode.StartsWith("Z_")).ToList();
                var plMatches = razigravanjeMecevi.ToList();
                var utMatches = utjesniMecevi.ToList();

                // ── UT_ round-robin mečevi (za 3 igrača) ──
                var rrMatches = utMatches.Where(m => m.MatchCode.StartsWith("UT_RR_")).ToList();
                var rrGroups = rrMatches.GroupBy(m => m.PlacingRange).ToList();

                foreach (var group in rrGroups)
                {
                    var (plL, plR) = ParsiranajeRange(group.Key);
                    if (plL <= 0) continue;

                    var groupList = group.ToList();
                    bool sviOdigrani = groupList.Count == 3 && groupList.All(m => m.Odigran);

                    if (sviOdigrani)
                    {
                        var playersInGroup = groupList
                            .SelectMany(m => new[] { m.Igrac1ID, m.Igrac2ID })
                            .Where(id => id != null)
                            .Distinct()
                            .ToList();

                        var stats = new List<(string PlayerId, int Wins, int SetDiff, int SetsWon)>();
                        foreach (var playerId in playersInGroup)
                        {
                            if (playerId == null) continue;
                            int wins = 0, setsWon = 0, setsLost = 0;
                            foreach (var m in groupList)
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

                        for (int i = 0; i < sortedStats.Count; i++)
                        {
                            int pos = plL + i;
                            string playerId = sortedStats[i].PlayerId;
                            playerRanks[playerId] = (pos, DajBodoveZaPoziciju(pos), $"{pos}. mjesto");
                        }
                    }
                }

                // ── Finale (1. i 2. mjesto) ──────────────────────────────────
                var zFinale = zMatches.FirstOrDefault(m => string.IsNullOrEmpty(m.WinnerNextMatchCode));
                if (zFinale != null)
                {
                    if (zFinale.Odigran && zFinale.Igrac1ID != null && zFinale.Igrac2ID != null)
                    {
                        bool i1Pob = (zFinale.PoeniIgrac1 ?? 0) > (zFinale.PoeniIgrac2 ?? 0);
                        playerRanks[zFinale.Igrac1ID] = i1Pob
                            ? (1, DajBodoveZaPoziciju(1), "1. mjesto 🏆 Pobjednik")
                            : (2, DajBodoveZaPoziciju(2), "2. mjesto 🥈");
                        playerRanks[zFinale.Igrac2ID] = i1Pob
                            ? (2, DajBodoveZaPoziciju(2), "2. mjesto 🥈")
                            : (1, DajBodoveZaPoziciju(1), "1. mjesto 🏆 Pobjednik");
                    }
                    else
                    {
                        // Finale još nije odigrano
                        if (zFinale.Igrac1ID != null && !playerRanks.ContainsKey(zFinale.Igrac1ID))
                            playerRanks[zFinale.Igrac1ID] = (1, DajBodoveZaPoziciju(2), "Finale (1-2. mjesto)");
                        if (zFinale.Igrac2ID != null && !playerRanks.ContainsKey(zFinale.Igrac2ID))
                            playerRanks[zFinale.Igrac2ID] = (1, DajBodoveZaPoziciju(2), "Finale (1-2. mjesto)");
                    }
                }

                // ── PL_ finalni mečevi (raspon 2 igrača = meč za konkretno mjesto) ──
                foreach (var plMec in plMatches.Where(m => m.Odigran))
                {
                    var (plL, plR) = ParsiranajeRange(plMec.PlacingRange);
                    if (plL <= 0 || plR - plL != 1) continue;

                    bool i1Pob = (plMec.PoeniIgrac1 ?? 0) > (plMec.PoeniIgrac2 ?? 0);
                    int winPos = plL, losePos = plR;

                    if (plMec.Igrac1ID != null && !playerRanks.ContainsKey(plMec.Igrac1ID))
                    {
                        playerRanks[plMec.Igrac1ID] = i1Pob
                            ? (winPos,  DajBodoveZaPoziciju(winPos),  $"{winPos}. mjesto")
                            : (losePos, DajBodoveZaPoziciju(losePos), $"{losePos}. mjesto");
                    }

                    if (plMec.Igrac2ID != null && !playerRanks.ContainsKey(plMec.Igrac2ID))
                    {
                        playerRanks[plMec.Igrac2ID] = i1Pob
                            ? (losePos, DajBodoveZaPoziciju(losePos), $"{losePos}. mjesto")
                            : (winPos,  DajBodoveZaPoziciju(winPos),  $"{winPos}. mjesto");
                    }
                }

                // ── UT_ finalni mečevi (raspon 2 igrača = meč za konkretno mjesto) ──
                foreach (var utMec in utMatches.Where(m => m.Odigran))
                {
                    var (plL, plR) = ParsiranajeRange(utMec.PlacingRange);
                    if (plL <= 0 || plR - plL != 1) continue;

                    bool i1Pob = (utMec.PoeniIgrac1 ?? 0) > (utMec.PoeniIgrac2 ?? 0);
                    int winPos = plL, losePos = plR;

                    if (utMec.Igrac1ID != null && !playerRanks.ContainsKey(utMec.Igrac1ID))
                    {
                        playerRanks[utMec.Igrac1ID] = i1Pob
                            ? (winPos,  DajBodoveZaPoziciju(winPos),  $"{winPos}. mjesto")
                            : (losePos, DajBodoveZaPoziciju(losePos), $"{losePos}. mjesto");
                    }

                    if (utMec.Igrac2ID != null && !playerRanks.ContainsKey(utMec.Igrac2ID))
                    {
                        playerRanks[utMec.Igrac2ID] = i1Pob
                            ? (losePos, DajBodoveZaPoziciju(losePos), $"{losePos}. mjesto")
                            : (winPos,  DajBodoveZaPoziciju(winPos),  $"{winPos}. mjesto");
                    }
                }

                // ── Igrači u toku razigravanja i utješnog – daj im najuži raspon ────────
                var neplaciraniIds = registracije.Select(r => r.KorisnikID)
                    .Where(id => !playerRanks.ContainsKey(id)).ToList();

                foreach (var playerId in neplaciraniIds)
                {
                    var igracMecevi = zMatches.Concat(plMatches).Concat(utMatches)
                        .Where(m => m.Igrac1ID == playerId || m.Igrac2ID == playerId)
                        .ToList();

                    if (!igracMecevi.Any()) continue;

                    int bestL = 0, bestR = int.MaxValue;
                    foreach (var m in igracMecevi)
                    {
                        var (L, R) = ParsiranajeRange(m.PlacingRange);
                        if (L > 0 && (bestR == int.MaxValue || R - L < bestR - bestL))
                        { bestL = L; bestR = R; }
                    }

                    if (bestL > 0)
                    {
                        string detalj = bestR == bestL ? $"{bestL}. mjesto"
                                      : $"{bestL}-{bestR}. mjesto";
                        playerRanks[playerId] = (bestL, DajBodoveZaPoziciju(bestR), detalj);
                    }
                }
            }

            // ── Igrači koji su ispali u grupnoj fazi ─────────────────────────
            var grupnaFazaIds = registracije.Select(r => r.KorisnikID)
                .Where(id => !playerRanks.ContainsKey(id)).ToList();

            var groupStats = new List<(string PlayerId, int Wins, int SetDiff, int SetsWon)>();
            foreach (var playerId in grupnaFazaIds)
            {
                int wins = 0, setsWon = 0, setsLost = 0;
                var igracMecevi = grupniMecevi
                    .Where(m => m.Odigran && (m.Igrac1ID == playerId || m.Igrac2ID == playerId));

                foreach (var m in igracMecevi)
                {
                    if (m.Igrac1ID == playerId)
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
                groupStats.Add((playerId, wins, setsWon - setsLost, setsWon));
            }

            var sortedGroup = groupStats
                .OrderByDescending(x => x.Wins)
                .ThenByDescending(x => x.SetDiff)
                .ThenByDescending(x => x.SetsWon)
                .ToList();

            int nextPos = playerRanks.Any() ? playerRanks.Values.Max(v => v.Pozicija) + 1 : 1;
            for (int i = 0; i < sortedGroup.Count; i++)
                playerRanks[sortedGroup[i].PlayerId] = (nextPos + i, 5, "Grupna faza");

            // ── Gradi listu plasmana ─────────────────────────────────────────
            foreach (var reg in registracije)
            {
                var user = reg.Korisnik;
                if (user == null) continue;

                if (playerRanks.TryGetValue(user.Id, out var rank))
                {
                    plasmani.Add(new TurnirPlasmanViewModel
                    {
                        KorisnikId     = user.Id,
                        Korisnik       = user,
                        ImePrezime     = $"{user.Ime} {user.Prezime}",
                        Grad           = user.Grad ?? "",
                        IsGost         = user.IsGost,
                        Pozicija       = rank.Pozicija,
                        Bodovi         = rank.Bodovi,
                        DetaljPozicije = rank.Detalj
                    });
                }
                else
                {
                    plasmani.Add(new TurnirPlasmanViewModel
                    {
                        KorisnikId     = user.Id,
                        Korisnik       = user,
                        ImePrezime     = $"{user.Ime} {user.Prezime}",
                        Grad           = user.Grad ?? "",
                        IsGost         = user.IsGost,
                        Pozicija       = nextPos + sortedGroup.Count,
                        Bodovi         = 5,
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

            // Find next power of 2
            int S = 2;
            while (S < N) S *= 2;

            // Shuffle pairs randomly
            var rng = new Random();
            var shuffledPairs = parovi.OrderBy(_ => rng.Next()).ToList();

            // Prepare list of pair identifiers for RasporediBye
            var pairIdentifiers = Enumerable.Range(0, N).Select(i => (string?)i.ToString()).ToList();
            var distributed = RasporediBye(pairIdentifiers, S, rng);

            DateTime pocetak = DateTime.Now;
            if (turnir.Mecevi != null && turnir.Mecevi.Any())
            {
                pocetak = turnir.Mecevi.Max(m => m.VrijemeMeca).AddHours(2);
            }

            int roundsCount = (int)Math.Round(Math.Log2(S));

            // Generate all matches
            for (int r = 1; r <= roundsCount; r++)
            {
                int matchesInRound = S / (int)Math.Pow(2, r);
                for (int m = 1; m <= matchesInRound; m++)
                {
                    mecevi.Add(new Mec
                    {
                        TurnirID = turnir.ID,
                        MatchCode = $"PAR_R{r}_M{m}",
                        Runda = r,
                        Odigran = false,
                        TipMeca = TipMeca.TurnirParova,
                        VrijemeMeca = pocetak.AddDays(r - 1).AddHours(m * 2),
                        PlacingRange = $"1-{S}"
                    });
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

                    if (key1 != null)
                    {
                        var pair1 = shuffledPairs[int.Parse(key1)];
                        mecR1.Igrac1ID = pair1.Igrac1ID;
                        mecR1.Igrac1PartnerID = pair1.Igrac2ID;
                    }
                    if (key2 != null)
                    {
                        var pair2 = shuffledPairs[int.Parse(key2)];
                        mecR1.Igrac2ID = pair2.Igrac1ID;
                        mecR1.Igrac2PartnerID = pair2.Igrac2ID;
                    }
                }
            }

            // Propagate BYEs
            PropagirajBye(mecevi);

            return mecevi;
        }
    }
}
