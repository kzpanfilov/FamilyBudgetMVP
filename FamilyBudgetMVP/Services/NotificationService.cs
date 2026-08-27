using FamilyBudgetMVP.Services;
using Plugin.LocalNotification;

namespace FamilyBudgetMVP.Services;

public class NotificationService
{
    private readonly BudgetService _budget;
    private readonly TransactionService _txService;
    private readonly CategoryStore _categories;
    private readonly BudgetPeriodStore _periodStore;

    public NotificationService(BudgetService budget, TransactionService txService, CategoryStore categories, BudgetPeriodStore periodStore)
    {
        _budget = budget;
        _txService = txService;
        _categories = categories;
        _periodStore = periodStore;
    }

    public async Task CheckLimitsAndNotifyAsync()
    {
        var txs = await _txService.GetTransactionsAsync();
        var cats = _categories.All;
        var period = _periodStore.GetCurrent();
        var (start, endExclusive) = period.Resolve(DateTime.Today);
        var limits = _budget.CheckLimitsInRange(txs, cats, start, endExclusive).ToList();

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
