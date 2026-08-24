using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Tests
{
    public class CsvBuilderTests
    {
        [Fact]
        public void Header_Line_Present()
        {
            var csv = CsvBuilder.BuildTransactionsCsv(Array.Empty<Transaction>());

            Assert.StartsWith("Дата;Тип;Категория;Сумма;Описание", csv);
            Assert.Equal(string.Empty, csv.Split('\n')[1]); // только заголовок + пустая строка
        }

        [Fact]
        public void Row_Fields_In_Order()
        {
            var tx = new Transaction
            {
                Date = new DateTime(2026, 8, 24, 14, 30, 0),
                Amount = -1234.5m,
                Category = "Продукты",
                Description = "Молоко и хлеб"
            };

            var line = CsvBuilder.BuildTransactionsCsv(new[] { tx })
                .Split('\n')[1]
                .TrimEnd('\r');

            // N2 для ru-RU ставит неразрывный пробел между разрядами
            Assert.Equal("24.08.2026 14:30;Расход;Продукты;-1\u00A0234,50;Молоко и хлеб", line);
        }

        [Fact]
        public void Values_With_Semicolons_Are_Quoted_And_Escaped()
        {
            var tx = new Transaction
            {
                Date = new DateTime(2026, 8, 24, 9, 0, 0),
                Amount = 100,
                Category = "Разное",
                Description = "Кофе; \"с собой\""
            };

            var line = CsvBuilder.BuildTransactionsCsv(new[] { tx })
                .Split('\n')[1]
                .TrimEnd('\r');

            Assert.Contains("\"Кофе; \"\"с собой\"\"\"", line);
        }

        [Fact]
        public void Income_Type_Label()
        {
            var tx = new Transaction
            {
                Date = DateTime.Now,
                Amount = 700,
                Category = "Разное",
                Description = "подарок"
            };

            var line = CsvBuilder.BuildTransactionsCsv(new[] { tx });

            Assert.Contains(";Доход;", line);
        }
    }
}
