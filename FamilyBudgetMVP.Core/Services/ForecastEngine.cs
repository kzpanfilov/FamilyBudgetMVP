using System.Globalization;
using FamilyBudgetMVP.Models;

namespace FamilyBudgetMVP.Services
{
    /// <summary>Результат прогноза: когда закончатся деньги и остаток на конец горизонта.</summary>
    public sealed record ForecastResult(
        DateTime AsOf,
        decimal StartBalance,
        decimal DailyBurn,
        bool RunsOut,
        DateTime RunoutDate,
        DateTime HorizonEnd,
        decimal HorizonEndBalance)
    {
        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        /// <summary>Готовый текст для карточки прогноза на дашборде.</summary>
        public string RunwayText => RunsOut
            ? $"Денег хватит до {RunoutDate.ToString("d MMMM", Ru)}"
            : "Хватит на весь срок";
    }

    /// <summary>
    /// Прогноз остатка: баланс сейчас + средний темп переменных трат за 30 дней
    /// + будущие вхождения повторяющихся платежей. День исчерпания — первый день,
    /// когда проецируемый баланс уходит ниже нуля.
    /// </summary>
    public static class ForecastEngine
    {
        public const int DefaultHorizonDays = 45;
        private const int BurnWindowDays = 30;

        public static ForecastResult Project(IEnumerable<Transaction> transactions, DateTime asOf, int horizonDays = DefaultHorizonDays) =>
            ProjectCore(transactions, asOf, horizonDays, extraDailyDelta: _ => 0m, oneTimeOnNextDay: null);

        internal static ForecastResult ProjectCore(
            IEnumerable<Transaction> transactions,
            DateTime asOf,
            int horizonDays,
            Func<DateTime, decimal> extraDailyDelta,
            decimal? oneTimeOnNextDay)
        {
            var list = transactions.ToList();

            // Баланс на текущий момент: только прошедшие операции
            decimal startBalance = list.Where(t => t.Date.Date <= asOf.Date).Sum(t => t.Amount);

            // Средние переменные траты в день по последним 30 дням (без повторяющихся:
            // их будущее учтено отдельно, чтобы не задваивать)
            var windowFrom = asOf.Date.AddDays(-(BurnWindowDays - 1));
            decimal burn = -list
                .Where(t => t.Amount < 0 && !t.IsRecurring && t.Date.Date >= windowFrom && t.Date.Date <= asOf.Date)
                .Sum(t => t.Amount);
            decimal dailyBurn = burn / BurnWindowDays;

            // Расписание будущих вхождений повторяющихся платежей
            var horizonEnd = asOf.Date.AddDays(horizonDays);
            var scheduled = new List<(DateTime Day, decimal Amount)>();
            foreach (var tx in list.Where(t => t.IsRecurring && t.Date.Date <= asOf.Date))
            {
                foreach (var day in Recurrence.OccurrencesAfter(tx.Date, tx.RecurrenceType, tx.RecurEndDate, asOf.Date, horizonEnd))
                    scheduled.Add((day, tx.Amount));
            }

            decimal balance = startBalance;
            bool runsOut = false;
            DateTime runoutDate = horizonEnd;

            for (var day = NextDay(asOf.Date); day <= horizonEnd; day = day.AddDays(1))
            {
                balance += extraDailyDelta(day);

                if (day == asOf.Date.AddDays(1) && oneTimeOnNextDay.HasValue)
                    balance += oneTimeOnNextDay.Value;

                balance += scheduled.Where(s => s.Day == day).Sum(s => s.Amount);
                balance -= dailyBurn;

                if (!runsOut && balance < 0)
                {
                    runsOut = true;
                    runoutDate = day;
                }
            }

            return new ForecastResult(asOf.Date, startBalance, dailyBurn, runsOut, runoutDate, horizonEnd, balance);
        }

        // Прогноз строится со следующего дня после asOf
        private static DateTime NextDay(DateTime asOfDate) => asOfDate.AddDays(1);

        /// <summary>
        /// Прогноз остатка на месяц с учётом повторяющихся платежей.
        /// Обёртка над Project с горизонтом до конца текущего месяца.
        /// </summary>
        public static ForecastResult ProjectMonth(IEnumerable<Transaction> transactions, DateTime asOf)
        {
            int daysInMonth = DateTime.DaysInMonth(asOf.Year, asOf.Month);
            int horizon = daysInMonth - asOf.Day;
            if (horizon < 1) horizon = 1;
            return Project(transactions, asOf, horizon);
        }
    }
}
