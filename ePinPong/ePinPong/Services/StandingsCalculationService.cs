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
            if (turnir == null) return new List<TurnirPlasmanViewModel>();

            var registracije = turnir.Registracije
                .Where(r => r.KorisnikID != SLOBODAN && r.Korisnik != null && !JeSlobodan(r.Korisnik.Id))
                .ToList();
            if (!registracije.Any()) return new List<TurnirPlasmanViewModel>();

            var playerRanks = new Dictionary<string, (int Pozicija, int Bodovi, string Detalj)>();

            int offsetUtjesni = IzracunajOffsetUtjesni(turnir);
            int grupnaFazaPos = offsetUtjesni > 0 ? (offsetUtjesni + 1) : 1;

            // 1. Glavna završnica: finale, pa plasmani 3. do N. mjesta
            OdrediFinale(turnir, playerRanks);
            OdrediPlasman3DoN(turnir, playerRanks);

            // 2. Grupna faza / utješni turnir (za igrače koji nisu ušli u završnicu)
            OdrediGrupnuFazu(turnir, playerRanks, grupnaFazaPos);

            // 3. Izgradi finalnu, sortiranu listu plasmana
            return IzgradiFinalnuListu(turnir, playerRanks, grupnaFazaPos);
        }

        private static List<string> UredjeniIgraciMeca(Mec m)
        {
            var ordered = new List<string>();
            if (m.PobjednikId != null)
            {
                ordered.Add(m.PobjednikId);
                ordered.Add(m.GubitnikId!);
            }
            else
            {
                if (m.Igrac1ID != null) ordered.Add(m.Igrac1ID);
                if (m.Igrac2ID != null) ordered.Add(m.Igrac2ID);
            }
            return ordered;
        }

        private int IzracunajOffsetUtjesni(Turnir turnir)
        {
            var mecevi = turnir.Mecevi.ToList();
            var grupniMecevi = mecevi.Where(m => m.TipMeca == TipMeca.GrupnaFaza).ToList();
            var zavrsniMecevi = mecevi.Where(m => m.TipMeca == TipMeca.Zavrsnica).ToList();

            int brojGrupa = grupniMecevi.Select(m => m.NazivGrupe).Where(n => !string.IsNullOrEmpty(n)).Distinct().Count();
            int offsetUtjesni = brojGrupa > 0 ? (brojGrupa * 2) : 0;
            if (offsetUtjesni == 0 && zavrsniMecevi.Any())
            {
                var zMatches = zavrsniMecevi.Where(m => m.MatchCode.StartsWith(AppConstants.MatchCodePrefixes.Zavrsnica)).ToList();
                offsetUtjesni = zMatches
                    .SelectMany(m => new[] { m.Igrac1ID, m.Igrac2ID })
                    .Where(id => id != null && !JeSlobodan(id))
                    .Distinct()
                    .Count();
            }
            return offsetUtjesni;
        }

        private void OdrediFinale(Turnir turnir, Dictionary<string, (int Pozicija, int Bodovi, string Detalj)> playerRanks)
        {
            var zavrsniMecevi = turnir.Mecevi.Where(m => m.TipMeca == TipMeca.Zavrsnica).ToList();
            var zMatchesList = zavrsniMecevi.Where(m => m.MatchCode.StartsWith(AppConstants.MatchCodePrefixes.Zavrsnica)).ToList();
            var zFinale = zMatchesList.FirstOrDefault(m => string.IsNullOrEmpty(m.WinnerNextMatchCode));

            if (zFinale != null)
            {
                var winId = zFinale.PobjednikId;
                if (winId != null)
                {
                    var loseId = zFinale.GubitnikId!;

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
        }

        private void OdrediPlasman3DoN(Turnir turnir, Dictionary<string, (int Pozicija, int Bodovi, string Detalj)> playerRanks)
        {
            var mecevi = turnir.Mecevi.ToList();
            var zavrsniMecevi = mecevi.Where(m => m.TipMeca == TipMeca.Zavrsnica).ToList();
            var zMatchesList = zavrsniMecevi.Where(m => m.MatchCode.StartsWith(AppConstants.MatchCodePrefixes.Zavrsnica)).ToList();
            var razigravanjeMecevi = mecevi.Where(m => m.TipMeca == TipMeca.Razigravanje).ToList();

            bool imaPlMeceve = razigravanjeMecevi.Any(m => m.MatchCode.StartsWith(AppConstants.MatchCodePrefixes.Placement));
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
                        var loserId = m.GubitnikId;
                        if (loserId != null && !JeSlobodan(loserId) && !playerRanks.ContainsKey(loserId))
                        {
                            string opisPozicije = targetPos == 3 ? "3. mjesto 🥉 (Polufinale)" : $"{targetPos}. mjesto";
                            playerRanks[loserId] = (targetPos, DajBodoveZaPoziciju(targetPos), opisPozicije);
                        }
                    }
                }
            }
            else
            {
                var plMatchesList = razigravanjeMecevi
                    .Where(m => m.MatchCode.StartsWith(AppConstants.MatchCodePrefixes.Placement))
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
                    var orderedPlayers = UredjeniIgraciMeca(m);

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
        }

        private void OdrediGrupnuFazu(Turnir turnir, Dictionary<string, (int Pozicija, int Bodovi, string Detalj)> playerRanks, int grupnaFazaPos)
        {
            var mecevi = turnir.Mecevi.ToList();
            var grupniMecevi = mecevi.Where(m => m.TipMeca == TipMeca.GrupnaFaza).ToList();
            var zavrsniMecevi = mecevi.Where(m => m.TipMeca == TipMeca.Zavrsnica).ToList();
            var utjesniMecevi = mecevi.Where(m => m.TipMeca == TipMeca.Utjesni).ToList();
            int brojGrupa = grupniMecevi.Select(m => m.NazivGrupe).Where(n => !string.IsNullOrEmpty(n)).Distinct().Count();
            bool imaUtjesni = (turnir.SistemTurnira == SistemTurnira.DoubleEliminationUtjesni) || utjesniMecevi.Any();

            var registracije = turnir.Registracije
                .Where(r => r.KorisnikID != SLOBODAN && r.Korisnik != null && !JeSlobodan(r.Korisnik.Id))
                .ToList();
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

                    var stats = IzracunajStatistikuIgraca(meceviGrupe, igraciGrupe!);

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
                var sortedGroup = IzracunajStatistikuIgraca(grupniMecevi, grupnaFazaIds)
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
                OdrediUtjesniPlasman(turnir, playerRanks, grupnaFazaPos);
            }
        }

        private void OdrediUtjesniPlasman(Turnir turnir, Dictionary<string, (int Pozicija, int Bodovi, string Detalj)> playerRanks, int grupnaFazaPos)
        {
            var mecevi = turnir.Mecevi.ToList();
            var grupniMecevi = mecevi.Where(m => m.TipMeca == TipMeca.GrupnaFaza).ToList();
            var utjesniMecevi = mecevi.Where(m => m.TipMeca == TipMeca.Utjesni).ToList();
            var registracije = turnir.Registracije
                .Where(r => r.KorisnikID != SLOBODAN && r.Korisnik != null && !JeSlobodan(r.Korisnik.Id))
                .ToList();

            int utjesniCurrentPos = grupnaFazaPos;
            var utMatches = utjesniMecevi.ToList();

            var rrMatches = utMatches.Where(m => m.MatchCode.StartsWith(AppConstants.MatchCodePrefixes.UtjesniRoundRobin)).ToList();
            if (rrMatches.Any())
            {
                var groupList = rrMatches.ToList();
                var playersInGroup = groupList
                    .SelectMany(m => new[] { m.Igrac1ID, m.Igrac2ID })
                    .Where(id => id != null && !JeSlobodan(id))
                    .Select(id => id!)
                    .Distinct()
                    .ToList();

                var sortedStats = IzracunajStatistikuIgraca(groupList, playersInGroup)
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
                var utFinale = utMatches.FirstOrDefault(m => m.MatchCode.StartsWith(AppConstants.MatchCodePrefixes.Utjesni + "R") && string.IsNullOrEmpty(m.WinnerNextMatchCode));
                if (utFinale != null)
                {
                    var orderedUtFinalists = UredjeniIgraciMeca(utFinale);

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
                    .Where(m => m.MatchCode.StartsWith(AppConstants.MatchCodePrefixes.UtjesniPlacement))
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
                    var orderedPlayers = UredjeniIgraciMeca(m);

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
                var sortedGroup = IzracunajStatistikuIgraca(grupniMecevi, preostaliGrupnaFaza)
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

        private List<TurnirPlasmanViewModel> IzgradiFinalnuListu(Turnir turnir, Dictionary<string, (int Pozicija, int Bodovi, string Detalj)> playerRanks, int grupnaFazaPos)
        {
            var plasmani = new List<TurnirPlasmanViewModel>();
            var registracije = turnir.Registracije
                .Where(r => r.KorisnikID != SLOBODAN && r.Korisnik != null && !JeSlobodan(r.Korisnik.Id))
                .ToList();

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

        /// <summary>
        /// Zajednički helper koji za listu mečeva i listu ID-ova igrača računa
        /// (wins, set-razlika, osvojeni setovi). Koristi se i u OdrediGrupnuFazu i u IzracunajTabeleGrupa.
        /// </summary>
        public static List<(string PlayerId, int Wins, int SetDiff, int SetsWon)> IzracunajStatistikuIgraca(
            List<Mec> meceviGrupe, List<string> igraciIds)
        {
            var result = new List<(string PlayerId, int Wins, int SetDiff, int SetsWon)>();
            foreach (var pid in igraciIds)
            {
                int wins = 0, setsWon = 0, setsLost = 0;
                foreach (var m in meceviGrupe.Where(m => m.Odigran && (m.Igrac1ID == pid || m.Igrac2ID == pid)))
                {
                    if (m.Igrac1ID == pid)
                    {
                        setsWon  += m.PoeniIgrac1 ?? 0;
                        setsLost += m.PoeniIgrac2 ?? 0;
                        if (m.PobjednikId == pid) wins++;
                    }
                    else
                    {
                        setsWon  += m.PoeniIgrac2 ?? 0;
                        setsLost += m.PoeniIgrac1 ?? 0;
                        if (m.PobjednikId == pid) wins++;
                    }
                }
                result.Add((pid, wins, setsWon - setsLost, setsWon));
            }
            return result;
        }

        /// <summary>
        /// Za svaku grupu u turniru vraća sortiranu listu <see cref="GroupStandingRow"/>
        /// (po pobjede desc, set-razlika desc, osvojeni setovi desc).
        /// </summary>
        public Dictionary<string, List<GroupStandingRow>> IzracunajTabeleGrupa(Turnir turnir)
        {
            var result = new Dictionary<string, List<GroupStandingRow>>();

            var grupniMecevi = turnir.Mecevi
                .Where(m => m.TipMeca == TipMeca.GrupnaFaza)
                .ToList();

            var meceviPoGrupama = grupniMecevi
                .GroupBy(m => m.NazivGrupe ?? "Grupa")
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var grupaGroup in meceviPoGrupama)
            {
                var nazivGrupe = grupaGroup.Key;
                var meceviGrupe = grupaGroup.ToList();

                var igraciIds = meceviGrupe
                    .SelectMany(m => new[] { m.Igrac1ID, m.Igrac2ID })
                    .Where(id => id != null && !JeSlobodan(id))
                    .Distinct()
                    .Select(id => id!)
                    .ToList();

                var stats = IzracunajStatistikuIgraca(meceviGrupe, igraciIds);

                var rows = stats
                    .OrderByDescending(x => x.Wins)
                    .ThenByDescending(x => x.SetDiff)
                    .ThenByDescending(x => x.SetsWon)
                    .Select(x =>
                    {
                        // Pronađi podatke o igraču iz prvog meča u kojem učestvuje
                        var mecSaIgracem = meceviGrupe
                            .FirstOrDefault(m => m.Igrac1ID == x.PlayerId || m.Igrac2ID == x.PlayerId);
                        var igrac = mecSaIgracem == null ? null
                            : mecSaIgracem.Igrac1ID == x.PlayerId ? mecSaIgracem.Igrac1 : mecSaIgracem.Igrac2;

                        return new GroupStandingRow
                        {
                            PlayerId    = x.PlayerId,
                            ImePrezime  = igrac != null ? $"{igrac.Ime} {igrac.Prezime}" : string.Empty,
                            IsGost      = igrac?.IsGost ?? false,
                            Grad        = igrac?.Grad ?? string.Empty,
                            Pobjede     = x.Wins,
                            Porazi      = meceviGrupe.Count(m => m.Odigran && (m.Igrac1ID == x.PlayerId || m.Igrac2ID == x.PlayerId)) - x.Wins,
                            SetRazlika  = x.SetDiff,
                            OsvojeniSetovi = x.SetsWon
                        };
                    })
                    .ToList();

                result[nazivGrupe] = rows;
            }

            return result;
        }
        /// <summary>
        /// Za svaku grupu u turniru parova vraća sortiranu listu <see cref="PairStandingRow"/>
        /// (po pobjede desc, set-razlika desc, osvojeni setovi desc).
        /// Analogno <see cref="IzracunajTabeleGrupa"/> koji radi za singl grupe.
        /// </summary>
        public Dictionary<string, List<PairStandingRow>> IzracunajTabeleGrupaParova(Turnir turnir)
        {
            var result = new Dictionary<string, List<PairStandingRow>>();

            var paroviMecevi = turnir.Mecevi
                .Where(m => m.TipMeca == TipMeca.TurnirParova)
                .ToList();

            var meceviPoGrupama = paroviMecevi
                .GroupBy(m => m.NazivGrupe ?? "Grupa A (Parovi)")
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var grupaGroup in meceviPoGrupama)
            {
                var nazivGrupe = grupaGroup.Key;
                var meceviGrupe = grupaGroup.ToList();

                // Gradi skup unikatnih parova:
                // 1. Iz TurnirParovi (registrovani parovi)
                // 2. Iz ID-ova igrača na mečevima (fallback ako par nije u TurnirParovi)
                var paroviSet = new List<(string I1Id, string I2Id, ApplicationUser? I1, ApplicationUser? I2)>();

                foreach (var p in turnir.TurnirParovi)
                {
                    if (p.Igrac1ID != null && p.Igrac2ID != null)
                    {
                        if (!paroviSet.Any(x => (x.I1Id == p.Igrac1ID && x.I2Id == p.Igrac2ID)
                                             || (x.I1Id == p.Igrac2ID && x.I2Id == p.Igrac1ID)))
                            paroviSet.Add((p.Igrac1ID, p.Igrac2ID, p.Igrac1, p.Igrac2));
                    }
                }

                foreach (var m in meceviGrupe)
                {
                    if (m.Igrac1ID != null && m.Igrac1PartnerID != null)
                    {
                        if (!paroviSet.Any(x => (x.I1Id == m.Igrac1ID && x.I2Id == m.Igrac1PartnerID)
                                             || (x.I1Id == m.Igrac1PartnerID && x.I2Id == m.Igrac1ID)))
                            paroviSet.Add((m.Igrac1ID, m.Igrac1PartnerID, m.Igrac1, m.Igrac1Partner));
                    }
                    if (m.Igrac2ID != null && m.Igrac2PartnerID != null)
                    {
                        if (!paroviSet.Any(x => (x.I1Id == m.Igrac2ID && x.I2Id == m.Igrac2PartnerID)
                                             || (x.I1Id == m.Igrac2PartnerID && x.I2Id == m.Igrac2ID)))
                            paroviSet.Add((m.Igrac2ID, m.Igrac2PartnerID, m.Igrac2, m.Igrac2Partner));
                    }
                }

                var stats = IzracunajStatistikuParova(meceviGrupe, paroviSet);

                var rows = stats
                    .OrderByDescending(x => x.Pobjede)
                    .ThenByDescending(x => x.SetRazlika)
                    .ThenByDescending(x => x.OsvojeniSetovi)
                    .Select(x => new PairStandingRow
                    {
                        Igrac1Id       = x.I1Id,
                        Igrac2Id       = x.I2Id,
                        Igrac1         = x.I1,
                        Igrac2         = x.I2,
                        Pobjede        = x.Pobjede,
                        Porazi         = x.Porazi,
                        SetRazlika     = x.SetRazlika,
                        OsvojeniSetovi = x.OsvojeniSetovi
                    })
                    .ToList();

                result[nazivGrupe] = rows;
            }

            return result;
        }

        /// <summary>
        /// Privatni helper koji za svaki par iz <paramref name="paroviSet"/> računa
        /// pobjede, poraze, set-razliku i osvojene setove na osnovu odigranih mečeva grupe.
        /// </summary>
        private static List<(string I1Id, string I2Id, ApplicationUser? I1, ApplicationUser? I2,
                              int Pobjede, int Porazi, int SetRazlika, int OsvojeniSetovi)>
            IzracunajStatistikuParova(
                List<Mec> meceviGrupe,
                List<(string I1Id, string I2Id, ApplicationUser? I1, ApplicationUser? I2)> paroviSet)
        {
            var result = new List<(string, string, ApplicationUser?, ApplicationUser?,
                                   int, int, int, int)>();

            foreach (var par in paroviSet)
            {
                int pobjede = 0, porazi = 0, osvojeniSetovi = 0, izgubljeniSetovi = 0;

                foreach (var m in meceviGrupe.Where(m => m.Odigran))
                {
                    // Da li je ovaj par na strani 1 meča (igrač1+partner1)
                    bool isSide1 = (m.Igrac1ID == par.I1Id && m.Igrac1PartnerID == par.I2Id)
                                || (m.Igrac1ID == par.I2Id && m.Igrac1PartnerID == par.I1Id);

                    // Da li je ovaj par na strani 2 meča (igrač2+partner2)
                    bool isSide2 = (m.Igrac2ID == par.I1Id && m.Igrac2PartnerID == par.I2Id)
                                || (m.Igrac2ID == par.I2Id && m.Igrac2PartnerID == par.I1Id);

                    if (isSide1)
                    {
                        int p1 = m.PoeniIgrac1 ?? 0, p2 = m.PoeniIgrac2 ?? 0;
                        osvojeniSetovi  += p1;
                        izgubljeniSetovi += p2;
                        if (p1 > p2) pobjede++; else porazi++;
                    }
                    else if (isSide2)
                    {
                        int p1 = m.PoeniIgrac1 ?? 0, p2 = m.PoeniIgrac2 ?? 0;
                        osvojeniSetovi  += p2;
                        izgubljeniSetovi += p1;
                        if (p2 > p1) pobjede++; else porazi++;
                    }
                }

                result.Add((par.I1Id, par.I2Id, par.I1, par.I2,
                            pobjede, porazi, osvojeniSetovi - izgubljeniSetovi, osvojeniSetovi));
            }

            return result;
        }
    }
}
