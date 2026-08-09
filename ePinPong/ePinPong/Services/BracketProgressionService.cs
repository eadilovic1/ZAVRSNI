using ePinPong.Models;
using ePinPong.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ePinPong.Services
{
    public class BracketProgressionService : IBracketProgressionService
    {
        private readonly ApplicationDbContext _context;

        public BracketProgressionService(ApplicationDbContext context)
        {
            _context = context;
        }

        private const string SLOBODAN = BracketService.SLOBODAN;

        public static bool JeSlobodan(string? id) => id == SLOBODAN;

        public List<Mec> GenerirajZavrsnicu(Turnir turnir, List<Mec> meceviGrupneFaze)
        {
            var meceviPoGrupama = meceviGrupneFaze.GroupBy(m => m.NazivGrupe).OrderBy(g => g.Key).ToList();
            var plasmani1 = new List<PlayerRecord>();
            var plasmani2 = new List<PlayerRecord>();
            var utjesniIgraciRecords = new List<PlayerRecord>();

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
                            if (m.PobjednikId == igracId) pobjede++;
                        }
                        else if (m.Igrac2ID == igracId)
                        {
                            osvojeniSetovi += m.PoeniIgrac2 ?? 0;
                            izgubljeniSetovi += m.PoeniIgrac1 ?? 0;
                            if (m.PobjednikId == igracId) pobjede++;
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

            var priorityList = new List<PlayerRecord>();
            priorityList.AddRange(sortedWinners);
            priorityList.AddRange(sortedRunners);
            for (int i = 0; i < priorityList.Count; i++)
                priorityList[i].HasBye = i < B;

            var winnersPlay = sortedWinners.Where(w => !w.HasBye).ToList();
            var winnersBye  = sortedWinners.Where(w =>  w.HasBye).ToList();
            var runnersPlay = sortedRunners.Where(r => !r.HasBye).ToList();
            var runnersBye  = sortedRunners.Where(r =>  r.HasBye).ToList();

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
                            int plL = S / (int)Math.Pow(2, r) + 1;
                            int plR = S / (int)Math.Pow(2, r - 1);
                            mec.LoserNextMatchCode = $"PL_{plL}_{plR}_R1_M{(m + 1) / 2}";
                            mec.LoserNextMatchSlot = (m % 2 != 0) ? 1 : 2;
                        }
                    }

                    sviMecevi.Add(mec);
                }
            }

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
                        GenerirajRoundRobin3(turnir, meceviUtjesnog, 1, utjesniPlayersShuffled, pocetakZavrsnice);
                    }
                    else
                    {
                        int S_utjesni = 2;
                        while (S_utjesni < utjesniIgraciCount) S_utjesni *= 2;

                        var finalList = BracketDrawService.RasporediSaSlobodanom(utjesniPlayersShuffled, S_utjesni, rngUt);
                        GenerirajUtjesniBracket(turnir, meceviUtjesnog, S_utjesni, finalList, pocetakZavrsnice);
                    }

                    sviMecevi.AddRange(meceviUtjesnog);
                }
            }

            PropagirajBye(sviMecevi);

            return sviMecevi;
        }

        public List<Mec> GenerirajPlasmanFazu(Turnir turnir, int L, int R, List<string?> gubitnici, List<Mec> postojeciMecevi, bool isUtjesni = false)
        {
            var noviMecevi = new List<Mec>();
            int n = R - L + 1;
            if (gubitnici.Count != n) return noviMecevi;

            var rng = new Random();
            var shuffled = gubitnici.OrderBy(_ => rng.Next()).ToList();

            DateTime startTime = postojeciMecevi.Any()
                ? postojeciMecevi.Max(m => m.VrijemeMeca).AddHours(2)
                : DateTime.Now;

            GenerirajPlasmanRekurzivno(turnir, noviMecevi, L, R, shuffled, startTime, 0, isUtjesni);

            var sviKombinovani = postojeciMecevi.Concat(noviMecevi).ToList();
            PropagirajBye(sviKombinovani);

            return noviMecevi;
        }

        private void GenerirajPlasmanRekurzivno(
            Turnir turnir, List<Mec> mecevi,
            int L, int R,
            List<string?>? igraciBaza,
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
                                if (mec.Igrac2ID == null) mec.Igrac2ID = SLOBODAN;
                                mec.PoeniIgrac1 = 3; mec.PoeniIgrac2 = 0;
                                winnerId = mec.Igrac1ID; 
                                loserId = mec.Igrac2ID;
                                winnerPartnerId = mec.Igrac1PartnerID; 
                                loserPartnerId = mec.Igrac2PartnerID;
                            }
                            else if (!igrac2Slobodan)
                            {
                                if (mec.Igrac1ID == null) mec.Igrac1ID = SLOBODAN;
                                mec.PoeniIgrac1 = 0; mec.PoeniIgrac2 = 3;
                                winnerId = mec.Igrac2ID; 
                                loserId = mec.Igrac1ID;
                                winnerPartnerId = mec.Igrac2PartnerID; 
                                loserPartnerId = mec.Igrac1PartnerID;
                            }
                            else
                            {
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

            if (N == 3)
            {
                var rngRr = new Random();
                var shuffled3 = parovi.OrderBy(_ => rngRr.Next()).ToList();
                mecevi.Add(new Mec { TurnirID = turnir.ID, MatchCode = "PAR_RR_M1", Runda = 1, Odigran = false, TipMeca = TipMeca.TurnirParova, VrijemeMeca = pocetak, PlacingRange = "1-3", NazivGrupe = "Grupa A (Parovi)", Igrac1ID = shuffled3[0].Igrac1ID, Igrac1PartnerID = shuffled3[0].Igrac2ID, Igrac2ID = shuffled3[1].Igrac1ID, Igrac2PartnerID = shuffled3[1].Igrac2ID });
                mecevi.Add(new Mec { TurnirID = turnir.ID, MatchCode = "PAR_RR_M2", Runda = 1, Odigran = false, TipMeca = TipMeca.TurnirParova, VrijemeMeca = pocetak.AddHours(1), PlacingRange = "1-3", NazivGrupe = "Grupa A (Parovi)", Igrac1ID = shuffled3[0].Igrac1ID, Igrac1PartnerID = shuffled3[0].Igrac2ID, Igrac2ID = shuffled3[2].Igrac1ID, Igrac2PartnerID = shuffled3[2].Igrac2ID });
                mecevi.Add(new Mec { TurnirID = turnir.ID, MatchCode = "PAR_RR_M3", Runda = 1, Odigran = false, TipMeca = TipMeca.TurnirParova, VrijemeMeca = pocetak.AddHours(2), PlacingRange = "1-3", NazivGrupe = "Grupa A (Parovi)", Igrac1ID = shuffled3[1].Igrac1ID, Igrac1PartnerID = shuffled3[1].Igrac2ID, Igrac2ID = shuffled3[2].Igrac1ID, Igrac2PartnerID = shuffled3[2].Igrac2ID });
                return mecevi;
            }

            int S = 2;
            while (S < N) S *= 2;

            var rng = new Random();
            var shuffledPairs = parovi.OrderBy(_ => rng.Next()).ToList();

            var pairIdentifiers = Enumerable.Range(0, N).Select(i => (string?)i.ToString()).ToList();
            var distributed = BracketDrawService.RasporediSaSlobodanom(pairIdentifiers, S, rng);

            int roundsCount = (int)Math.Round(Math.Log2(S));

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

            PropagirajBye(mecevi);

            return mecevi;
        }

        public async Task PropagirajPobjednikaAsync(Mec mec)
        {
            if (mec.TipMeca == TipMeca.Zavrsnica || mec.TipMeca == TipMeca.Razigravanje || mec.TipMeca == TipMeca.Utjesni || mec.TipMeca == TipMeca.TurnirParova)
            {
                int poeniIgrac1 = mec.PoeniIgrac1 ?? 0;
                int poeniIgrac2 = mec.PoeniIgrac2 ?? 0;

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
                    await ProvjeriIGenerirajRazigravanjaAsync(mec.TurnirID);
                }

                // Propagiraj BYE (Slobodan) prolaze za sve mečeve koji su dobili protivnika
                var sviMeceviTurnira = await _context.Mecevi.Where(m => m.TurnirID == mec.TurnirID).ToListAsync();
                PropagirajBye(sviMeceviTurnira);
            }
        }

        public async Task ProvjeriIGenerirajRazigravanjaAsync(int turnirId)
        {
            var turnir = await _context.Turniri.FindAsync(turnirId);
            if (turnir == null || turnir.SistemTurnira == SistemTurnira.SingleElimination) return;

            var sviMecevi = await _context.Mecevi.Where(m => m.TurnirID == turnirId).ToListAsync();
            
            // 1. Provjeri i generiši razigravanja za glavnu završnicu (Z_ i PL_)
            var zMecevi = sviMecevi.Where(m => (m.TipMeca == TipMeca.Zavrsnica || m.TipMeca == TipMeca.Razigravanje) && (m.MatchCode.StartsWith("Z_") || m.MatchCode.StartsWith("PL_"))).ToList();
            await GenerisiRazigravanjaZaSkupinuAsync(turnir, sviMecevi, zMecevi, isUtjesni: false);

            // 2. Provjeri i generiši razigravanja za utješni turnir (UT_R i UT_PL_)
            if (turnir.SistemTurnira == SistemTurnira.DoubleEliminationUtjesni)
            {
                var utMecevi = sviMecevi.Where(m => m.TipMeca == TipMeca.Utjesni && (m.MatchCode.StartsWith("UT_PL_") || (m.MatchCode.StartsWith("UT_R") && !m.MatchCode.StartsWith("UT_RR_")))).ToList();
                await GenerisiRazigravanjaZaSkupinuAsync(turnir, sviMecevi, utMecevi, isUtjesni: true);
            }

            await _context.SaveChangesAsync();
        }

        private async Task GenerisiRazigravanjaZaSkupinuAsync(Turnir turnir, List<Mec> sviMecevi, List<Mec> meceviSkupine, bool isUtjesni)
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
                        loserId ??= SLOBODAN;
                        gubitnici.Add(loserId);
                    }

                    if (gubitnici.Count != M) continue;

                    await DbSeeder.EnsureSlobodanUserExistsAsync(_context);
                    var noviMecevi = GenerirajPlasmanFazu(turnir, L, R, gubitnici, sviMecevi, isUtjesni);
                    if (noviMecevi.Any())
                    {
                        _context.Mecevi.AddRange(noviMecevi);
                        sviMecevi.AddRange(noviMecevi);
                    }
                }
            }
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
    }
}
