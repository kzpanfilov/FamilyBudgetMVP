using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Tests
{
    public class SummarizeTests
    {
        private static Transaction Tx(decimal amount) => new() { Description = "t", Amount = amount, Date = DateTime.Now };

        private readonly BudgetService _service = new(new FakePalette());

        [Fact]
        public void Empty_List_Gives_Zeroes()
        {
            var s = _service.Summarize(Array.Empty<Transaction>());

            Assert.Equal(0m, s.Balance);
            Assert.Equal(0m, s.Income);
            Assert.Equal(0m, s.Expense);
        }

        [Fact]
        public void Mixed_Transactions_Summed_Correctly()
        {
            var s = _service.Summarize(new[] { Tx(1000), Tx(-400), Tx(250), Tx(-50) });

            Assert.Equal(800m, s.Balance);
            Assert.Equal(1250m, s.Income);
            Assert.Equal(450m, s.Expense);
        }

        [Fact]
        public void Only_Expenses_Negative_Balance()
        {
            var s = _service.Summarize(new[] { Tx(-100), Tx(-200) });

            Assert.Equal(-300m, s.Balance);
            Assert.Equal(0m, s.Income);
            Assert.Equal(300m, s.Expense);
        }
    }
}
