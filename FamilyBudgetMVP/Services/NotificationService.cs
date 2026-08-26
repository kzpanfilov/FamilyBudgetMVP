using FamilyBudgetMVP.Services;
using Plugin.LocalNotification;

namespace FamilyBudgetMVP.Services;

public class NotificationService
{
    private readonly BudgetService _budget;
    private readonly TransactionService _txService;
    private readonly CategoryStore _categories;

    public NotificationService(BudgetService budget, TransactionService txService, CategoryStore categories)
    {
        _budget = budget;
        _txService = txService;
        _categories = categories;
    }

    public async Task CheckLimitsAndNotifyAsync()
    {
        var txs = await _txService.GetTransactionsAsync();
        var cats = _categories.All;
        var limits = _budget.CheckMonthlyLimits(txs, cats).ToList();

        foreach (var lim in limits)
        {
            if (lim.Exceeded)
            {
                await SendNotification(
                    $"⚠️ Превышен лимит «{lim.Category}»",
                    $"Расходы: {lim.Spent:N0}₽ из {lim.Limit:N0}₽. Перерасход: {lim.Spent - lim.Limit:N0}₽");
            }
            else if (lim.Approaching)
            {
                var pct = (int)(lim.Spent / lim.Limit * 100);
                await SendNotification(
                    $"🔔 Лимит «{lim.Category}» на подходе",
                    $"Потрачено {pct}% ({lim.Spent:N0}₽ из {lim.Limit:N0}₽). Осталось {lim.Limit - lim.Spent:N0}₽");
            }
        }
    }

    private static int _notificationCounter = 1000;

    private static Task SendNotification(string title, string description)
    {
        var request = new NotificationRequest
        {
            NotificationId = Interlocked.Increment(ref _notificationCounter),
            Title = title,
            Description = description
        };

        return request.Show();
    }
}
