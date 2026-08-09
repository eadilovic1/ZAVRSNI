using ePinPong.Models;
using System.Linq;

namespace ePinPong.Services
{
    public class TurnirCompletionService : ITurnirCompletionService
    {
        public bool EvaluateAndCloseIfFinished(Turnir turnir)
        {
            if (turnir == null || turnir.Status == StatusTurnira.Zavrsen || turnir.Mecevi == null || !turnir.Mecevi.Any())
            {
                return false;
            }

            var meceviList = turnir.Mecevi.ToList();
            var imaZavrsnicu = meceviList.Any(m => m.TipMeca == TipMeca.Zavrsnica);
            var grupniMecevi = meceviList.Where(m => m.TipMeca == TipMeca.GrupnaFaza).ToList();
            var brojGrupa = grupniMecevi.Select(m => m.NazivGrupe).Where(n => !string.IsNullOrEmpty(n)).Distinct().Count();
            var isMasters = LigaTurnirHelper.IsMastersTurnir(turnir);
            var isGroupOnly = grupniMecevi.Any() && !imaZavrsnicu && (brojGrupa == 1 || isMasters);

            if (!meceviList.All(m => m.Odigran && m.TipMeca != TipMeca.TurnirParova))
            {
                return false;
            }

            if (imaZavrsnicu || isGroupOnly || isMasters)
            {
                turnir.Status = StatusTurnira.Zavrsen;

                if (imaZavrsnicu)
                {
                    var finalMec = meceviList.FirstOrDefault(m => m.TipMeca == TipMeca.Zavrsnica && string.IsNullOrEmpty(m.WinnerNextMatchCode));
                    if (finalMec != null && finalMec.Odigran)
                    {
                        turnir.PobjednikID = finalMec.PoeniIgrac1 == 3 ? finalMec.Igrac1ID : finalMec.Igrac2ID;
                        turnir.DrugoplasiraniID = finalMec.PoeniIgrac1 == 3 ? finalMec.Igrac2ID : finalMec.Igrac1ID;
                    }
                }
                else if (grupniMecevi.Any())
                {
                    var igraciGrupe = grupniMecevi
                        .SelectMany(m => new[] { m.Igrac1ID, m.Igrac2ID })
                        .Where(id => id != null && id != BracketService.SLOBODAN)
                        .Distinct()
                        .ToList();

                    var groupStats = igraciGrupe.Select(pid =>
                    {
                        int wins = 0, setsWon = 0, setsLost = 0;
                        foreach (var gm in grupniMecevi.Where(m => m.Odigran && (m.Igrac1ID == pid || m.Igrac2ID == pid)))
                        {
                            if (gm.Igrac1ID == pid)
                            {
                                setsWon  += gm.PoeniIgrac1 ?? 0;
                                setsLost += gm.PoeniIgrac2 ?? 0;
                                if (gm.PobjednikId == pid) wins++;
                            }
                            else
                            {
                                setsWon  += gm.PoeniIgrac2 ?? 0;
                                setsLost += gm.PoeniIgrac1 ?? 0;
                                if (gm.PobjednikId == pid) wins++;
                            }
                        }
                        return new { PlayerId = pid, Wins = wins, SetDiff = setsWon - setsLost, SetsWon = setsWon };
                    })
                    .OrderByDescending(x => x.Wins)
                    .ThenByDescending(x => x.SetDiff)
                    .ThenByDescending(x => x.SetsWon)
                    .ToList();

                    if (groupStats.Count > 0) turnir.PobjednikID = groupStats[0].PlayerId;
                    if (groupStats.Count > 1) turnir.DrugoplasiraniID = groupStats[1].PlayerId;
                }

                return true;
            }

            return false;
        }
    }
}
