using System.Globalization;
using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Views
{
    /// <summary>
    /// Сценарии «что если»: сравнение прогноза «сейчас» и по сценарию,
    /// сохранение и загрузка сценариев (ТЗ MVP, этап 4).
    /// </summary>
    public partial class ScenariosPage : ContentPage
    {
        private readonly TransactionService _txService;
        private readonly ScenarioService _scenarios;
        private readonly BudgetService _budgetService;

        private List<Transaction> _transactions = new();

        public ScenariosPage(TransactionService txService, ScenarioService scenarios, BudgetService budgetService)
        {
            InitializeComponent();
            _txService = txService;
            _scenarios = scenarios;
            _budgetService = budgetService;

            // Freemium: сценарии — премиум-функция (сейчас флаг открыт)
            if (!FeatureGate.IsUnlocked(Feature.Scenarios))
            {
                ContentHost.Children.Clear();
                ContentHost.Children.Add(new Label
                {
                    Text = "🔒  Сценарии доступны в премиум-версии",
                    FontFamily = "OpenSansSemibold",
                    FontSize = 16,
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 48, 0, 0)
                });
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                await _txService.InitializeAsync();
                _transactions = await _txService.GetTransactionsAsync();
                await RefreshNowCardAsync();
                await ReloadSavedAsync();
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Сценарии: загрузка");
                NowRunwayLabel.Text = "Ошибка загрузки данных";
            }
        }

        private Task RefreshNowCardAsync()
        {
            var f = ForecastEngine.Project(_transactions, DateTime.Today);
            NowRunwayLabel.Text = f.RunwayText;
            NowEndLabel.Text = $"Остаток на {f.HorizonEnd:d MMMM}: {f.HorizonEndBalance:N0} ₽";
            return Task.CompletedTask;
        }

        private static decimal? ParseAmount(string? raw) => decimal.TryParse(
            (raw ?? string.Empty).Trim().Replace('.', ','),
            NumberStyles.Number | NumberStyles.AllowLeadingSign,
            CultureInfo.GetCultureInfo("ru-RU"), out decimal value)
                ? value
                : null;

        private Scenario ReadForm() => new()
        {
            Name = NameEntry.Text?.Trim() ?? string.Empty,
            IncomeChange = ParseAmount(IncomeChangeEntry.Text) ?? 0m,
            ExpenseChange = ParseAmount(ExpenseChangeEntry.Text) ?? 0m,
            OneTimeSubsidy = ParseAmount(SubsidyEntry.Text) ?? 0m
        };

        private void ShowComparison(Scenario scenario)
        {
            (ForecastResult @base, ForecastResult with) =
                ScenarioEngine.Compare(_transactions, scenario, DateTime.Today);

            CompareCard.IsVisible = true;

            BaseLine.Text = $"Сейчас: {@base.RunwayText.ToLower()}, " +
                            $"остаток на {@base.HorizonEnd:d MMM} — {@base.HorizonEndBalance:N0} ₽";

            ScenarioLine.Text = $"«{scenario.Name}»: {with.RunwayText.ToLower()}, " +
                                $"остаток на {with.HorizonEnd:d MMM} — {with.HorizonEndBalance:N0} ₽";

            decimal delta = with.HorizonEndBalance - @base.HorizonEndBalance;
            string sign = delta >= 0 ? "+" : "−";
            DeltaLine.Text = delta == 0
                ? "Разницы нет"
                : $"{sign}{Math.Abs(delta):N0} ₽ к остатку на конец срока";
        }

        private void OnCalculateClicked(object? sender, EventArgs e)
        {
            var scenario = ReadForm();
            scenario.Name = string.IsNullOrEmpty(scenario.Name) ? "Сценарий" : scenario.Name;
            ShowComparison(scenario);
        }

        private async void OnSaveClicked(object? sender, EventArgs e)
        {
            var scenario = ReadForm();

            if (string.IsNullOrEmpty(scenario.Name))
            {
                await DisplayAlertAsync("Нужно название", "Дайте сценарию название, чтобы сохранить.", "OK");
                return;
            }

            try
            {
                await _scenarios.SaveAsync(scenario);
                await ReloadSavedAsync();
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Сценарии: сохранение");
                await DisplayAlertAsync("Ошибка", "Не удалось сохранить сценарий.", "OK");
            }
        }

        private async Task ReloadSavedAsync()
        {
            var saved = await _scenarios.GetAllAsync();

            ScenariosList.Children.Clear();

            foreach (var s in saved)
            {
                var row = BuildScenarioRow(s);
                ScenariosList.Children.Add(row);
            }
        }

        private Border BuildScenarioRow(Scenario scenario)
        {
            var parts = new List<string>();
            if (scenario.IncomeChange != 0) parts.Add($"доход {(scenario.IncomeChange > 0 ? "+" : "")}{scenario.IncomeChange:N0}");
            if (scenario.ExpenseChange != 0) parts.Add($"расход +{scenario.ExpenseChange:N0}");
            if (scenario.OneTimeSubsidy != 0) parts.Add($"субсидия {scenario.OneTimeSubsidy:N0}");
            string summary = parts.Count == 0 ? "без изменений" : string.Join(", ", parts);

            var delete = new Button
            {
                Text = "✕",
                Style = (Style)Application.Current!.Resources["GhostDelete"],
                VerticalOptions = LayoutOptions.Center
            };
            delete.Clicked += async (_, _) => await OnDeleteScenarioAsync(scenario);

            var content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                Children =
                {
                    new VerticalStackLayout
                    {
                        Spacing = 2,
                        Children =
                        {
                            new Label
                            {
                                Text = scenario.Name,
                                FontFamily = "OpenSansSemibold",
                                FontSize = 15,
                                TextColor = (Color)Application.Current.Resources["InkPrimary"]
                            },
                            new Label
                            {
                                Text = summary,
                                FontSize = 12,
                                TextColor = (Color)Application.Current.Resources["InkSecondary"]
                            }
                        }
                    },
                    delete
                }
            };
            content.ColumnDefinitions[1].Width = GridLength.Auto;

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await OnScenarioTappedAsync(scenario);
            content.GestureRecognizers.Add(tap);

            return new Border
            {
                Style = (Style)Application.Current.Resources["Card"],
                Content = content
            };
        }

        private async Task OnScenarioTappedAsync(Scenario scenario)
        {
            FormTitle.Text = scenario.Name;
            NameEntry.Text = scenario.Name;
            IncomeChangeEntry.Text = scenario.IncomeChange == 0 ? string.Empty : scenario.IncomeChange.ToString("N0");
            ExpenseChangeEntry.Text = scenario.ExpenseChange == 0 ? string.Empty : scenario.ExpenseChange.ToString("N0");
            SubsidyEntry.Text = scenario.OneTimeSubsidy == 0 ? string.Empty : scenario.OneTimeSubsidy.ToString("N0");

            ShowComparison(scenario);
            await Task.CompletedTask;
        }

        private async Task OnDeleteScenarioAsync(Scenario scenario)
        {
            bool confirm = await DisplayAlertAsync("Удаление", $"Удалить сценарий «{scenario.Name}»?", "Да", "Нет");
            if (!confirm)
                return;

            try
            {
                await _scenarios.DeleteAsync(scenario.Id);
                await ReloadSavedAsync();
            }
            catch (Exception ex)
            {
                LogService.Error(ex, "Сценарии: удаление");
            }
        }
    }
}
