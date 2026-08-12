using System;
using System.Collections.Generic;
using System.Linq;

namespace ePinPong.Helpers
{
    public static class StandingsExtensions
    {
        /// <summary>
        /// Sortira kolekciju po sportskom plasmanu: pobjede (desc), set-razlika (desc), osvojeni setovi (desc).
        /// </summary>
        public static IOrderedEnumerable<T> BySportskiPlasman<T>(
            this IEnumerable<T> source,
            Func<T, int> winsSelector,
            Func<T, int> setDiffSelector,
            Func<T, int> setsWonSelector)
        {
            return source
                .OrderByDescending(winsSelector)
                .ThenByDescending(setDiffSelector)
                .ThenByDescending(setsWonSelector);
        }

        /// <summary>
        /// Specijalizovani preopterećeni metod za torku statistike igrača (Wins, SetDiff, SetsWon).
        /// </summary>
        public static IOrderedEnumerable<(string PlayerId, int Wins, int SetDiff, int SetsWon)> BySportskiPlasman(
            this IEnumerable<(string PlayerId, int Wins, int SetDiff, int SetsWon)> source)
        {
            return source.BySportskiPlasman(x => x.Wins, x => x.SetDiff, x => x.SetsWon);
        }
    }
}
