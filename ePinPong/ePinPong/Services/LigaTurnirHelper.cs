using ePinPong.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ePinPong.Services
{
    public static class LigaTurnirHelper
    {
        public static int GetMastersKolo(Liga liga) => liga.BrojRegularnihTurnira + 1;

        public static bool IsMastersTurnir(Turnir turnir, Liga liga)
            => turnir.Kolo == GetMastersKolo(liga);

        public static bool IsRegularTurnir(Turnir turnir, Liga liga)
            => turnir.LigaID == liga.ID && !IsMastersTurnir(turnir, liga);

        public static IEnumerable<Turnir> GetRegularniTurniri(Liga liga)
            => liga.Turniri.Where(t => IsRegularTurnir(t, liga));

        public static IEnumerable<Turnir> GetZavrseniRegularniTurniri(Liga liga)
            => GetRegularniTurniri(liga).Where(t => t.Status == StatusTurnira.Zavrsen);

        public static bool HasMasters(Liga liga)
            => liga.Turniri.Any(t => IsMastersTurnir(t, liga));

        public static bool CanCreateRegular(Liga liga)
            => GetRegularniTurniri(liga).Count() < liga.BrojRegularnihTurnira;

        public static bool CanCreateMasters(Liga liga)
            => GetZavrseniRegularniTurniri(liga).Count() >= liga.BrojRegularnihTurnira
               && !HasMasters(liga);

        public static bool CanCreateAnyTurnir(Liga liga)
            => CanCreateRegular(liga) || CanCreateMasters(liga);

        public static DateTime GetRegularTurnirDatum(Liga liga, int kolo)
        {
            if (liga == null) throw new ArgumentNullException(nameof(liga));
            if (kolo < 1) throw new ArgumentOutOfRangeException(nameof(kolo));

            var monthBase = new DateTime(liga.DatumPocetka.Year, liga.DatumPocetka.Month, 1);
            var targetMonth = monthBase.AddMonths(kolo - 1);
            var candidate = GetLastSundayOfMonth(targetMonth.Year, targetMonth.Month);

            if (kolo == 1 && candidate < DateTime.Today)
            {
                var nextMonth = targetMonth.AddMonths(1);
                return GetLastSundayOfMonth(nextMonth.Year, nextMonth.Month);
            }

            return candidate;
        }

        public static int? GetSljedeceKolo(Liga liga)
        {
            var usedKola = GetRegularniTurniri(liga)
                .Where(t => t.Kolo.HasValue)
                .Select(t => t.Kolo!.Value)
                .ToHashSet();

            for (int i = 1; i <= liga.BrojRegularnihTurnira; i++)
            {
                if (!usedKola.Contains(i))
                    return i;
            }

            return null;
        }

        private static DateTime GetLastSundayOfMonth(int year, int month)
        {
            var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            int diff = (7 + (int)lastDay.DayOfWeek - (int)DayOfWeek.Sunday) % 7;
            return lastDay.AddDays(-diff).Date;
        }
    }
}
