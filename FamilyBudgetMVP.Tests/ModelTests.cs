using FamilyBudgetMVP.Models;

namespace FamilyBudgetMVP.Tests
{
    public class TransactionTests
    {
        [Fact]
        public void IsIncome_True_For_Positive_Amount()
        {
            Assert.True(new Transaction { Amount = 100 }.IsIncome);
        }

        [Fact]
        public void IsIncome_False_For_Negative_Amount()
        {
            Assert.False(new Transaction { Amount = -100 }.IsIncome);
        }

        [Fact]
        public void FormattedAmount_Positive_With_Plus()
        {
            var tx = new Transaction { Amount = 500 };
            Assert.StartsWith("+", tx.FormattedAmount);
            Assert.EndsWith("₽", tx.FormattedAmount);
        }

        [Fact]
        public void FormattedAmount_Negative_No_Plus()
        {
            var tx = new Transaction { Amount = -500 };
            Assert.DoesNotContain("+", tx.FormattedAmount);
            Assert.EndsWith("₽", tx.FormattedAmount);
        }

        [Fact]
        public void IsRecurring_False_For_None()
        {
            Assert.False(new Transaction { RecurrenceType = Recurrence.None }.IsRecurring);
        }

        [Fact]
        public void IsRecurring_True_For_Weekly()
        {
            Assert.True(new Transaction { RecurrenceType = Recurrence.Weekly }.IsRecurring);
        }

        [Fact]
        public void DateShort_Formats_As_DdMmYyyy()
        {
            var tx = new Transaction { Date = new DateTime(2026, 3, 5) };
            Assert.Equal("05.03.2026", tx.DateShort);
        }

        [Fact]
        public void TimeText_Formats_As_HhMm()
        {
            var tx = new Transaction { Date = new DateTime(2026, 1, 1, 14, 5, 0) };
            Assert.Equal("14:05", tx.TimeText);
        }
    }

    public class CategoryTests
    {
        [Fact]
        public void LimitText_With_Limit_Shows_Amount()
        {
            var cat = new Category { MonthlyLimit = 15000 };
            Assert.Contains("15\u00A0000", cat.LimitText);
            Assert.Contains("лимит", cat.LimitText);
        }

        [Fact]
        public void LimitText_Without_Limit_Shows_None()
        {
            var cat = new Category { MonthlyLimit = 0 };
            Assert.Equal("без лимита", cat.LimitText);
        }

        [Fact]
        public void Default_Icon_Is_Package()
        {
            Assert.Equal("📦", new Category().Icon);
        }
    }

    public class RecurrenceTests
    {
        [Theory]
        [InlineData(Recurrence.None, "Не повторяется")]
        [InlineData(Recurrence.Weekly, "Еженедельно")]
        [InlineData(Recurrence.Monthly, "Ежемесячно")]
        [InlineData(Recurrence.Quarterly, "Раз в квартал")]
        public void Display_Shows_Correct_Russian(string type, string expected)
        {
            Assert.Equal(expected, Recurrence.Display(type));
        }

        [Fact]
        public void NextAfter_None_Returns_Null()
        {
            Assert.Null(Recurrence.NextAfter(DateTime.Today, Recurrence.None));
        }

        [Fact]
        public void NextAfter_Weekly_Adds_7_Days()
        {
            var date = new DateTime(2026, 1, 1);
            Assert.Equal(date.AddDays(7), Recurrence.NextAfter(date, Recurrence.Weekly));
        }

        [Fact]
        public void NextAfter_Monthly_Adds_1_Month()
        {
            var date = new DateTime(2026, 1, 15);
            Assert.Equal(date.AddMonths(1), Recurrence.NextAfter(date, Recurrence.Monthly));
        }

        [Fact]
        public void NextAfter_Quarterly_Adds_3_Months()
        {
            var date = new DateTime(2026, 1, 15);
            Assert.Equal(date.AddMonths(3), Recurrence.NextAfter(date, Recurrence.Quarterly));
        }

        [Fact]
        public void OccurrencesAfter_None_Yields_Nothing()
        {
            var result = Recurrence.OccurrencesAfter(
                DateTime.Today, Recurrence.None, null,
                DateTime.Today, DateTime.Today.AddDays(30));
            Assert.Empty(result);
        }

        [Fact]
        public void OccurrencesAfter_Weekly_Generates_Dates()
        {
            var start = new DateTime(2026, 1, 1);
            var from = new DateTime(2026, 1, 1);
            var through = new DateTime(2026, 1, 31);

            var result = Recurrence.OccurrencesAfter(start, Recurrence.Weekly, null, from, through).ToList();

            Assert.Equal(4, result.Count);
            Assert.All(result, d => Assert.True(d > from));
        }

        [Fact]
        public void OccurrencesAfter_Respects_EndDate()
        {
            var start = new DateTime(2026, 1, 1);
            var endDate = new DateTime(2026, 1, 15);
            var from = new DateTime(2026, 1, 1);
            var through = new DateTime(2026, 1, 31);

            var result = Recurrence.OccurrencesAfter(start, Recurrence.Weekly, endDate, from, through).ToList();

            Assert.All(result, d => Assert.True(d <= endDate));
        }
    }
}
