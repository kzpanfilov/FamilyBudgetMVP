using FamilyBudgetMVP.Models;

namespace FamilyBudgetMVP.Services
{
    /// <summary>
    /// Хранилище настройки периода бюджета. По умолчанию — календарный месяц,
    /// пользователь может задать свои границы (например, 21 → 4: с 21-го числа
    /// по 4-е число следующего месяца).
    /// </summary>
    public sealed class BudgetPeriodStore
    {
        private const string StartDayKey = "budget.period_start_day";
        private const string EndDayKey = "budget.period_end_day";

        public BudgetPeriod GetCurrent() => new(
            ClampDay(Preferences.Get(StartDayKey, BudgetPeriod.CalendarMonth.StartDay)),
            ClampDay(Preferences.Get(EndDayKey, BudgetPeriod.CalendarMonth.EndDay)));

        public void Save(BudgetPeriod period)
        {
            Preferences.Set(StartDayKey, ClampDay(period.StartDay));
            Preferences.Set(EndDayKey, ClampDay(period.EndDay));
        }

        public void ResetToCalendarMonth() => Save(BudgetPeriod.CalendarMonth);

        private static int ClampDay(int day) => Math.Clamp(day, 1, 31);
    }
}