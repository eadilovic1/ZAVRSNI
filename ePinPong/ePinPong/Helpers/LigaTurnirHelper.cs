using ePinPong.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ePinPong.Helpers
{
    public static class LigaTurnirHelper
    {
        public static int GetMastersKolo(Liga liga) => liga.BrojRegularnihTurnira + 1;

        public static Turnir BuildStandardTurnir(Liga liga, int kolo, string organizatorId, bool isMasters = false)
        {
            if (liga == null) throw new ArgumentNullException(nameof(liga));

            DateTime targetSunday = isMasters
                ? GetNextTurnirDatumForLiga(liga)
                : GetRegularTurnirDatum(liga, kolo);

            DateTime datumPocetka = targetSunday.AddHours(10);
            DateTime datumKraja = targetSunday.AddHours(18);

            string naziv = isMasters
                ? "ZAVRŠNI MASTERS"
                : $"{liga.Naziv} - Kolo {kolo}";

            string opis = isMasters
                ? $"Završni Masters turnir za {liga.Naziv}."
                : $"Mjesečni ligaški turnir za {liga.Naziv}. Kolo {kolo} od {liga.BrojRegularnihTurnira}. Nakon odigravanja svih kola, najbolji igrači će se plasirati na završni Masters.";

            return new Turnir
            {
                Naziv = naziv,
                Status = StatusTurnira.Planiran,
                DatumPocetka = datumPocetka,
                DatumKraja = datumKraja,
                MaxIgraca = 64,
                Lokacija = "Klupska Dvorana ePinPong",
                Opis = opis,
                LigaID = liga.ID,
                Kolo = isMasters ? GetMastersKolo(liga) : kolo,
                OrganizatorId = organizatorId ?? string.Empty,
                SlikaUrl = AppConstants.DefaultTurnirSlikaUrl
            };
        }

        public static bool IsMastersTurnir(Turnir turnir, Liga liga)
            => turnir.Kolo == GetMastersKolo(liga);

        public static bool IsMastersTurnir(Turnir turnir)
            => turnir != null && turnir.Liga != null && turnir.Kolo.HasValue && turnir.Kolo.Value == GetMastersKolo(turnir.Liga);

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

        public static bool IsLigaOkoncana(Liga liga)
        {
            if (liga == null || liga.Turniri == null) return false;
            return GetZavrseniRegularniTurniri(liga).Count() >= liga.BrojRegularnihTurnira
                   && HasMasters(liga)
                   && liga.Turniri.Any(t => IsMastersTurnir(t, liga) && t.Status == StatusTurnira.Zavrsen);
        }

        public static DateTime GetDefaultStandaloneTurnirDatum()
        {
            var today = DateTime.UtcNow.Date;
            var candidate = GetLastSundayOfMonth(today.Year, today.Month);
            if (candidate < today)
            {
                var nextMonth = today.AddMonths(1);
                candidate = GetLastSundayOfMonth(nextMonth.Year, nextMonth.Month);
            }
            return candidate;
        }

        public static DateTime GetNextTurnirDatumForLiga(Liga liga)
        {
            if (liga == null) throw new ArgumentNullException(nameof(liga));

            if (liga.Turniri != null && liga.Turniri.Any())
            {
                var lastTurnirDate = liga.Turniri.Max(t => t.DatumPocetka);
                var targetMonth = new DateTime(lastTurnirDate.Year, lastTurnirDate.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
                return GetLastSundayOfMonth(targetMonth.Year, targetMonth.Month);
            }
            else
            {
                var targetMonth = new DateTime(liga.DatumPocetka.Year, liga.DatumPocetka.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var candidate = GetLastSundayOfMonth(targetMonth.Year, targetMonth.Month);
                if (candidate < DateTime.UtcNow.Date)
                {
                    var today = DateTime.UtcNow.Date;
                    var nextMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    candidate = GetLastSundayOfMonth(nextMonth.Year, nextMonth.Month);
                    if (candidate < today)
                    {
                        nextMonth = nextMonth.AddMonths(1);
                        candidate = GetLastSundayOfMonth(nextMonth.Year, nextMonth.Month);
                    }
                }
                return candidate;
            }
        }

        public static DateTime GetRegularTurnirDatum(Liga liga, int kolo)
        {
            if (liga == null) throw new ArgumentNullException(nameof(liga));
            if (kolo < 1) throw new ArgumentOutOfRangeException(nameof(kolo));

            // Ako je već dodijeljeno kolo i postoje turniri u ligi, koristimo datum 1 mjesec nakon zadnjeg turnira
            if (liga.Turniri != null && liga.Turniri.Any())
            {
                return GetNextTurnirDatumForLiga(liga);
            }

            var monthBase = new DateTime(liga.DatumPocetka.Year, liga.DatumPocetka.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var targetMonth = monthBase.AddMonths(kolo - 1);
            var candidate = GetLastSundayOfMonth(targetMonth.Year, targetMonth.Month);

            if (kolo == 1 && candidate < DateTime.UtcNow.Date)
            {
                var nextMonth = targetMonth.AddMonths(1);
                return GetLastSundayOfMonth(nextMonth.Year, nextMonth.Month);
            }

            return candidate;
        }

        public static DateTime GetMastersTurnirDatum(Liga liga)
        {
            return GetNextTurnirDatumForLiga(liga);
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

        public static DateTime GetLastSundayOfMonth(int year, int month)
        {
            var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Utc);
            int diff = (7 + (int)lastDay.DayOfWeek - (int)DayOfWeek.Sunday) % 7;
            return lastDay.AddDays(-diff).Date;
        }
    }
}
