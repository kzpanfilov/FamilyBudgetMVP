using FamilyBudgetMVP.Models;

namespace FamilyBudgetMVP.Tests
{
    public class BudgetPeriodTests
    {
        // Календарный месяц: 1-е … последний день месяца

        [Theory]
        [InlineData(2026, 8, 15, 2026, 8, 1, 2026, 9, 1)]
        [InlineData(2026, 8, 1, 2026, 8, 1, 2026, 9, 1)]
        [InlineData(2026, 8, 31, 2026, 8, 1, 2026, 9, 1)]
        [InlineData(2026, 2, 10, 2026, 2, 1, 2026, 3, 1)] // февраль
        public void CalendarMonth_Resolves_To_Full_Calendar_Month(
            int y, int m, int d, int sy, int sm, int sd, int ey, int em, int ed)
        {
            var (start, endExclusive) = BudgetPeriod.CalendarMonth.Resolve(new DateTime(y, m, d));

            Assert.Equal(new DateTime(sy, sm, sd), start);
            Assert.Equal(new DateTime(ey, em, ed), endExclusive);
        }

        // «С 21-го числа каждого месяца по 4-е число следующего». 21 > 4 → перенос в следующий месяц.

        [Fact]
        public void CustomPeriod_StartDay_After_EndDay_Spans_Next_Month()
        {
            var period = new BudgetPeriod(21, 4);

            var (start, endExclusive) = period.Resolve(new DateTime(2026, 8, 27, 14, 30, 0));

            Assert.Equal(new DateTime(2026, 8, 21), start);
            Assert.Equal(new DateTime(2026, 9, 5), endExclusive); // 4-е включительно → 5-е не включено
        }

        [Fact]
        public void CustomPeriod_Date_Before_StartDay_Uses_Previous_Month_Period()
        {
            var period = new BudgetPeriod(21, 4);

            // 5-е августа ещё относится к периоду, начавшемуся 21 июля
            var (start, endExclusive) = period.Resolve(new DateTime(2026, 8, 5));

            Assert.Equal(new DateTime(2026, 7, 21), start);
            Assert.Equal(new DateTime(2026, 8, 5), endExclusive);
        }

        [Fact]
        public void CustomPeriod_On_EndDay_Inclusive()
        {
            var period = new BudgetPeriod(21, 4);

            var (start, endExclusive) = period.Resolve(new DateTime(2026, 9, 4));

            Assert.Equal(new DateTime(2026, 8, 21), start);
            Assert.Equal(new DateTime(2026, 9, 5), endExclusive);
        }

        [Fact]
        public void CustomPeriod_FormatRange_Same_Year()
        {
            var period = new BudgetPeriod(21, 4);

            Assert.Equal("21 авг — 4 сент", period.FormatRange(new DateTime(2026, 8, 27)));
        }

        [Fact]
        public void CalendarMonth_FormatRange()
        {
            Assert.Equal("1 авг — 31 авг", BudgetPeriod.CalendarMonth.FormatRange(new DateTime(2026, 8, 15)));
        }
    }
}