using System.Globalization;
using FamilyBudgetMVP.Models;
using Microcharts;
using SkiaSharp;

namespace FamilyBudgetMVP.Services
{
    /// <summary>Баланс и агрегаты по операциям.</summary>
    public record BudgetSummary(decimal Balance, decimal Income, decimal Expense);

    /// <summary>Состояние месячного лимита категории.</summary>
    public record LimitStatus(string Category, decimal Spent, decimal Limit)
    {
        public bool Exceeded => Spent > Limit;
        public bool Approaching => !Exceeded && Limit > 0 && Spent >= Limit * 0.85m;
    }

    /// <summary>
    /// Вся финансовая логика приложения: подсчёты, группировка истории,
    /// подготовка данных графика, лимиты. Не зависит от UI и БД.
    /// </summary>
    public class BudgetService
    {
        // Цвет перерасхода (совпадает с ExpenseRed из палитры приложения)
        private const string ExceededColorHex = "#E5484D";

        // Цвет линии динамики (Teal500)
        private const string TrendColorHex = "#14B8A6";

        private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

        private readonly ICategoryPalette _palette;

        public BudgetService(ICategoryPalette palette)
        {
            _palette = palette;
        }

        public BudgetSummary Summarize(IEnumerable<Transaction> transactions)
        {
            var list = transactions.ToList();

            return new BudgetSummary(
                Balance: list.Sum(t => t.Amount),
                Income: list.Where(t => t.Amount > 0).Sum(t => t.Amount),
                Expense: -list.Where(t => t.Amount < 0).Sum(t => t.Amount));
        }

        /// <summary>
        /// Суммарный баланс за месяц с учётом повторяющихся операций.
        /// Прошлые вхождения повторяющихся платежей проецируются на месяц,
        /// чтобы баланс отражал реальные траты, а не только одну запись в БД.
        /// </summary>
        public BudgetSummary SummarizeMonth(IEnumerable<Transaction> transactions, int? year = null, int? month = null)
        {
            var now = DateTime.Today;
            int y = year ?? now.Year;
            int m = month ?? now.Month;

            var monthStart = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);

            var list = transactions.ToList();
            decimal totalBalance = 0, totalIncome = 0, totalExpense = 0;

            foreach (var tx in list)
            {
                if (!tx.IsRecurring)
                {
                    // Обычная операция: считаем только если попадает в месяц
                    if (tx.Date >= monthStart && tx.Date < monthEnd)
                    {
                        totalBalance += tx.Amount;
                        if (tx.Amount > 0) totalIncome += tx.Amount;
                        else totalExpense -= tx.Amount;
                    }
                }
                else
                {
                    // Повторяющаяся: считаем базовое + все проецируемые вхождения в месяц
                    // OccurrencesAfter генерирует только БУДУЩИЕ вхождения (после базовой даты),
                    // поэтому базовое вхождение обрабатываем отдельно
                    if (tx.Date >= monthStart && tx.Date < monthEnd)
                    {
                        totalBalance += tx.Amount;
                        if (tx.Amount > 0) totalIncome += tx.Amount;
                        else totalExpense -= tx.Amount;
                    }

                    // Проецируем будущие вхождения (от вчерашнего дня — чтобы захватить вчерашнее базовое)
                    var fromExclusive = tx.Date.Date < monthStart.Date
                        ? monthStart.Date.AddDays(-1)
                        : tx.Date.Date;

                    foreach (var occurrence in Recurrence.OccurrencesAfter(
                        tx.Date, tx.RecurrenceType, tx.RecurEndDate,
                        fromExclusive, monthEnd.AddDays(-1)))
                    {
                        if (occurrence >= monthStart && occurrence < monthEnd)
                        {
                            totalBalance += tx.Amount;
                            if (tx.Amount > 0) totalIncome += tx.Amount;
                            else totalExpense -= tx.Amount;
                        }
                    }
                }
            }

            return new BudgetSummary(totalBalance, totalIncome, totalExpense);
        }

        // --- Расходы по категориям за текущий месяц ---

        public List<ChartEntry> BuildMonthExpenseEntries(
            IEnumerable<Transaction> transactions,
            IReadOnlyList<Category>? categories = null,
            int? year = null,
            int? month = null,
            string? defaultValueLabelHex = null)
        {
            var now = DateTime.Today;
            int y = year ?? now.Year;
            int m = month ?? now.Month;

            return transactions
                .Where(t => t.Amount < 0 && t.Date.Year == y && t.Date.Month == m)
                .GroupBy(t => t.Category)
                .Select(g => new { Category = g.Key, Total = -g.Sum(t => t.Amount) })
                .OrderByDescending(x => x.Total)
                .Select(x =>
                {
                    float value = Convert.ToSingle(x.Total);
                    bool exceeded = IsLimitExceeded(x.Category, x.Total, categories);

                    return new ChartEntry(value)
                    {
                        Label = x.Category,
                        ValueLabel = value.ToString("N0"),
                        Color = exceeded ? SKColor.Parse(ExceededColorHex) : _palette.GetChartColor(x.Category),
                        ValueLabelColor = exceeded
                            ? SKColor.Parse(ExceededColorHex)
                            : SKColor.Parse(defaultValueLabelHex ?? "#1F2A2E")
                    };
                })
                .ToList();
        }

        // --- Динамика расходов по дням ---

        public List<ChartEntry> BuildDailyExpenseEntries(IEnumerable<Transaction> transactions, int days = 30)
        {
            var today = DateTime.Today;
            var from = today.AddDays(-(days - 1));

            var sums = transactions
                .Where(t => t.Amount < 0 && t.Date.Date >= from)
                .GroupBy(t => t.Date.Date)
                .ToDictionary(g => g.Key, g => -g.Sum(t => t.Amount));

            var result = new List<ChartEntry>();
            for (var day = from; day <= today; day = day.AddDays(1))
            {
                result.Add(new ChartEntry((float)sums.GetValueOrDefault(day.Date))
                {
                    Label = day.ToString("d.MM"),
                    ValueLabel = string.Empty, // без подписей: важна форма тренда
                    Color = SKColor.Parse(TrendColorHex)
                });
            }

            return result;
        }

        // --- Лимиты ---

        public static bool IsLimitExceeded(string category, decimal spentThisMonth, IReadOnlyList<Category>? categories)
        {
            var limit = categories?.FirstOrDefault(c => c.Name == category)?.MonthlyLimit ?? 0;
            return limit > 0 && spentThisMonth > limit;
        }

        /// <summary>Категории с превышенным или близким к превышению лимитом.</summary>
        public List<LimitStatus> CheckMonthlyLimits(IEnumerable<Transaction> transactions, IReadOnlyList<Category> categories)
        {
            var now = DateTime.Today;

            return categories
                .Where(c => c.MonthlyLimit > 0)
                .Select(c =>
                {
                    var spent = -transactions
                        .Where(t => t.Amount < 0 &&
                                    t.Category == c.Name &&
                                    t.Date.Year == now.Year &&
                                    t.Date.Month == now.Month)
                        .Sum(t => t.Amount);

                    return new LimitStatus(c.Name, spent, c.MonthlyLimit);
                })
                .Where(s => s.Exceeded || s.Approaching)
                .ToList();
        }

        // --- Группировка истории ---

        // Группировка истории по дням: Сегодня / Вчера / d MMMM [yyyy]
        public List<TransactionsByDay> GroupByDay(IEnumerable<Transaction> transactions, bool newestFirst = true)
        {
            var today = DateTime.Today;
            var result = new List<TransactionsByDay>();

            var days = transactions.GroupBy(t => t.Date.Date);
            days = newestFirst
                ? days.OrderByDescending(g => g.Key)
                : days.OrderBy(g => g.Key);

            foreach (var g in days)
            {
                string title;
                if (g.Key == today) title = "Сегодня";
                else if (g.Key == today.AddDays(-1)) title = "Вчера";
                else if (g.Key.Year == today.Year) title = g.Key.ToString("d MMMM", RuCulture);
                else title = g.Key.ToString("d MMMM yyyy", RuCulture);

                var items = newestFirst
                    ? g.OrderByDescending(t => t.Date).ToList()
                    : g.OrderBy(t => t.Date).ToList();

                result.Add(new TransactionsByDay
                {
                    Title = title,
                    Items = items
                });
            }

            return result;
        }
    }
}
