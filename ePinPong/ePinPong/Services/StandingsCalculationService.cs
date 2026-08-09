using ePinPong.Models;
using ePinPong.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ePinPong.Services
{
    public class StandingsCalculationService : IStandingsCalculationService
    {
        private const string SLOBODAN = BracketService.SLOBODAN;

        private static bool JeSlobodan(string? id) => id == SLOBODAN;

        public (int L, int R) ParsirajRange(string? range)
        {
            if (string.IsNullOrEmpty(range)) return (0, 0);
            var parts = range.Split('-');
            if (parts.Length == 2
                && int.TryParse(parts[0], out int L)
                && int.TryParse(parts[1], out int R))
                return (L, R);
            return (0, 0);
        }

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
                var zPoRundama = zMatchesList.GroupBy(m => m.Runda).OrderByDescending(g => g.Key).ToList();
                int maxRunda = zPoRundama.Any() ? zPoRundama.First().Key : 0;

                foreach (var rundaGroup in zPoRundama)
                {
                    int r = rundaGroup.Key;
                    if (r == maxRunda) continue;

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
                var plMatchesList = razigravanjeMecevi
                    .Where(m => m.MatchCode.StartsWith("PL_"))
                    .Select(m => {
                        var (l, r) = ParsirajRange(m.PlacingRange);
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

            bool isMasters = turnir.Liga != null && turnir.Kolo.HasValue && turnir.Kolo.Value == LigaTurnirHelper.GetMastersKolo(turnir.Liga);

            if (isMasters)
            {
                var meceviPoGrupama = grupniMecevi.GroupBy(m => m.NazivGrupe).OrderBy(g => g.Key).ToList();
                int overallPos = 1;

                foreach (var grupaGroup in meceviPoGrupama)
                {
                    string gNaziv = grupaGroup.Key ?? "Grupa";
                    var meceviGrupe = grupaGroup.ToList();
                    var igraciGrupe = meceviGrupe
                        .SelectMany(m => new[] { m.Igrac1ID, m.Igrac2ID })
                        .Where(id => id != null && !JeSlobodan(id) && !playerRanks.ContainsKey(id))
                        .Distinct()
                        .ToList();

                    var stats = new List<(string PlayerId, int Wins, int SetDiff, int SetsWon)>();
                    foreach (var pid in igraciGrupe)
                    {
                        int wins = 0, setsWon = 0, setsLost = 0;
                        foreach (var m in meceviGrupe.Where(m => m.Odigran && (m.Igrac1ID == pid || m.Igrac2ID == pid)))
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
                        stats.Add((pid, wins, setsWon - setsLost, setsWon));
                    }

                    var sorted = stats
                        .OrderByDescending(x => x.Wins)
                        .ThenByDescending(x => x.SetDiff)
                        .ThenByDescending(x => x.SetsWon)
                        .ToList();

                    int groupRank = 1;
                    foreach (var item in sorted)
                    {
                        string opis = groupRank switch
                        {
                            1 => $"1. mjesto 🏆 ({gNaziv})",
                            2 => $"2. mjesto 🥈 ({gNaziv})",
                            3 => $"3. mjesto 🥉 ({gNaziv})",
                            _ => $"{groupRank}. mjesto ({gNaziv})"
                        };
                        playerRanks[item.PlayerId] = (overallPos, DajBodoveZaPoziciju(overallPos), opis);
                        overallPos++;
                        groupRank++;
                    }
                }
            }
            else if (!zavrsniMecevi.Any() && !utjesniMecevi.Any() && grupniMecevi.Any() && brojGrupa == 1)
            {
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
                foreach (var pid in grupnaFazaIds)
                {
                    playerRanks[pid] = (grupnaFazaPos, DajBodoveZaPoziciju(grupnaFazaPos), $"{grupnaFazaPos}. mjesto (Grupna faza)");
                }
            }
            else
            {
                int utjesniCurrentPos = grupnaFazaPos;
                var utMatches = utjesniMecevi.ToList();

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

                    var utPlMatches = utMatches
                        .Where(m => m.MatchCode.StartsWith("UT_PL_"))
                        .Select(m => {
                            var (l, r) = ParsirajRange(m.PlacingRange);
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
    }
}
