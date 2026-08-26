namespace FamilyBudgetMVP.Models
{
    /// <summary>Типы периодичности повторяющихся платежей.</summary>
    public static class Recurrence
    {
        public const string None = "none";
        public const string Weekly = "weekly";
        public const string Monthly = "monthly";
        public const string Quarterly = "quarterly";

        /// <summary>Все допустимые значения для UI-пикеров.</summary>
        public static readonly string[] All = { None, Weekly, Monthly, Quarterly };

        /// <summary>Человекочитаемое название для UI.</summary>
        public static string Display(string type) => type switch
        {
            Weekly => "Еженедельно",
            Monthly => "Ежемесячно",
            Quarterly => "Раз в квартал",
            _ => "Не повторяется"
        };

        /// <summary>Следующая дата после текущего вхождения (или null для none).</summary>
        public static DateTime? NextAfter(DateTime occurrence, string type) => type switch
        {
            Weekly => occurrence.AddDays(7),
            Monthly => occurrence.AddMonths(1),
            Quarterly => occurrence.AddMonths(3),
            _ => null
        };

        /// <summary>
        /// Все будущие вхождения повторяющегося платежа в интервале
        /// (fromInclusive..throughInclusive]. Базовая операция уже случилась на дате start,
        /// поэтому генерируются только последующие.
        /// </summary>
        public static IEnumerable<DateTime> OccurrencesAfter(
            DateTime start, string type, DateTime? endDate, DateTime fromExclusive, DateTime throughInclusive)
        {
            if (type == None)
                yield break;

            var date = start;
            while (true)
            {
                var next = NextAfter(date, type);
                if (next == null || next > throughInclusive)
                    yield break;

                if (endDate != null && next > endDate.Value)
                    yield break;

                if (next > fromExclusive)
                    yield return next.Value;

                date = next.Value;
            }
        }
    }
}
