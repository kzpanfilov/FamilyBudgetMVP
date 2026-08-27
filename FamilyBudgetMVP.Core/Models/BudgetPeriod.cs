using System.Globalization;

namespace FamilyBudgetMVP.Models
{
    /// <summary>
    /// Период бюджета: отрезок, за который считаются баланс, доходы и расходы.
    /// По умолчанию — текущий календарный месяц, но можно задать свои границы,
    /// например: с 21-го числа каждого месяца по 4-е число следующего.
    /// </summary>
    public sealed record BudgetPeriod(int StartDay, int EndDay)
    {
        /// <summary>Период по умолчанию: календарный месяц (1-е … последний день).</summary>
        public static BudgetPeriod CalendarMonth { get; } = new(1, 31);

        private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

        /// <summary>
        /// Границы текущего периода: начало (включительно) и конец (исключительно).
        /// Период — «самый недавний начавшись»: если день сегодня меньше начала,
        /// текущим считается период, стартовавший в прошлом месяце.
        /// </summary>
        public (DateTime StartInclusive, DateTime EndExclusive) Resolve(DateTime asOf)
        {
            var today = asOf.Date;

            // Месяц, в котором начинается текущий период
            int delta = today.Day >= StartDay ? 0 : -1;
            var startMonth = new DateTime(today.Year, today.Month, 1).AddMonths(delta);

            var start = ClampToMonth(startMonth, StartDay);

            // Конец: в том же месяце, если EndDay >= StartDay, иначе — в следующем
            var endMonth = EndDay >= StartDay ? startMonth : startMonth.AddMonths(1);
            var endExclusive = ClampToMonth(endMonth, EndDay).AddDays(1);

            return (start, endExclusive);
        }

        /// <summary>Диапазон период. Форматирует границы текстом для UI, например «21 авг – 4 сен».</summary>
        public string FormatRange(DateTime asOf)
        {
            var (start, endExclusive) = Resolve(asOf);
            var endExclusiveDay = endExclusive.AddDays(-1);

            var startStr = $"{start.Day} {ShortMonth(start)}";
            var endStr = start.Year == endExclusiveDay.Year
                ? $"{endExclusiveDay.Day} {ShortMonth(endExclusiveDay)}"
                : $"{endExclusiveDay.Day} {ShortMonth(endExclusiveDay)} {endExclusiveDay.Year}";

            return $"{startStr} — {endStr}";
        }

        private static string ShortMonth(DateTime date) =>
            date.ToString("MMM", RuCulture).TrimEnd('.');

        private static DateTime ClampToMonth(DateTime month, int day)
        {
            int last = DateTime.DaysInMonth(month.Year, month.Month);
            return new DateTime(month.Year, month.Month, Math.Min(day, last));
        }
    }
}