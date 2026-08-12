using ePinPong.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ePinPong.Services
{
    public class BracketDrawService : IBracketDrawService
    {
        private readonly IRandomProvider _rng;

        public BracketDrawService(IRandomProvider rng)
        {
            _rng = rng;
        }

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

            var grupeIgraca = new List<List<string>>();
            for (int i = 0; i < ukGrupa; i++) grupeIgraca.Add(new List<string>());

            int expectedPot4Count = groupSizes.Sum(size => Math.Max(0, size - 3));
            if (useQualityGrouping)
            {
                grupeIgraca = DistributeOrderedPlayersIntoGroups(igracIds, groupSizes);
            }
            else if (pot1.Count == ukGrupa && pot2.Count == ukGrupa && pot3.Count == ukGrupa && pot4.Count == expectedPot4Count)
            {
                var shuffledPot1 = pot1.OrderBy(a => _rng.Next()).ToList();
                var shuffledPot2 = pot2.OrderBy(a => _rng.Next()).ToList();
                var shuffledPot3 = pot3.OrderBy(a => _rng.Next()).ToList();
                var shuffledPot4 = pot4.OrderBy(a => _rng.Next()).ToList();

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
                var shuffledIgracIds = igracIds.OrderBy(a => _rng.Next()).ToList();
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

        public static List<string?> RasporediSaSlobodanom(List<string?> players, int bracketSize, Func<int> nextInt, string? filler = BracketService.SLOBODAN)
        {
            int pCount = players.Count;
            int slobodanCount = bracketSize - pCount;
            int pairCount = bracketSize / 2;

            var result = new string?[bracketSize];
            for (int i = 0; i < bracketSize; i++) result[i] = filler;

            if (pCount == 0) return result.ToList();

            var pairIndices = Enumerable.Range(0, pairCount).OrderBy(_ => nextInt()).ToList();

            if (slobodanCount <= pairCount)
            {
                var slobodanPairs = new HashSet<int>(pairIndices.Take(slobodanCount));
                int playerIdx = 0;

                for (int i = 0; i < pairCount; i++)
                {
                    if (slobodanPairs.Contains(i))
                    {
                        result[2 * i] = players[playerIdx++];
                        result[2 * i + 1] = filler;
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
                var playerPairs = new HashSet<int>(pairIndices.Take(pCount));
                int playerIdx = 0;

                for (int i = 0; i < pairCount; i++)
                {
                    if (playerPairs.Contains(i))
                    {
                        result[2 * i] = players[playerIdx++];
                        result[2 * i + 1] = filler;
                    }
                    else
                    {
                        result[2 * i] = filler;
                        result[2 * i + 1] = filler;
                    }
                }
            }

            return result.ToList();
        }

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
                return Enumerable.Repeat(4, groupCount - 3)
                    .Concat(Enumerable.Repeat(3, 3))
                    .ToList();
            }

            if (remainder == 2)
            {
                return Enumerable.Repeat(4, groupCount - 2)
                    .Concat(Enumerable.Repeat(3, 2))
                    .ToList();
            }

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
    }
}
