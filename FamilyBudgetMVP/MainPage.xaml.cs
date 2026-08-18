using System.Collections.ObjectModel;
using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;
using System.Globalization;

using Microcharts;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls; // Microcharts использует эту библиотеку для отрисовки

namespace FamilyBudgetMVP;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _dbService;
    private ObservableCollection<Transaction> _transactions = new();

    public MainPage(DatabaseService dbService)
    {
        InitializeComponent();
        _dbService = dbService;
        TransactionsList.ItemsSource = _transactions;
        
        // Загружаем данные при старте
        LoadDataAsync();
    }

    private async void LoadDataAsync()
    {
        try 
        {
            var data = await _dbService.GetTransactionsAsync();
        
            _transactions.Clear();
            foreach (var t in data)
            {
                _transactions.Add(t);
            }

            UpdateBalance();
        
            // ✅ ДОБАВЛЕНО: Обновляем график после загрузки данных
            UpdateChart(); 
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки: {ex.Message}");
            BalanceLabel.Text = "Ошибка БД";
        }
    }

    private void UpdateChart()
    {
        var entries = GenerateChartEntries();

        // 1. Создаем сам график (логика данных)
        var chart = new BarChart
        {
            Entries = entries,
            LabelOrientation = Orientation.Horizontal,
            ValueLabelOrientation = Orientation.Horizontal,
            BackgroundColor = SKColors.Transparent,
            //Padding = new Thickness(10),
            MaxValue = (float)(entries.Any() ? entries.Max(e => e.Value) * 1.2 : 100)
        };

        // 2. Создаем холст (UI элемент), который умеет рисовать графики Microcharts
        var canvasView = new SKCanvasView
        {
            HorizontalOptions = LayoutOptions.FillAndExpand,
            VerticalOptions = LayoutOptions.FillAndExpand
        };

        // 3. Подписываемся на событие рисования: когда холст готов, скажи графику "нарисуйся здесь"
        canvasView.PaintSurface += (sender, args) =>
        {
            var surface = args.Surface;
            var skCanvas = surface.Canvas;
        
            // Очищаем холст
            skCanvas.Clear(SKColors.Transparent);

            // Рисуем наш график на этом холсте
            var skRect = args.Info.Rect;

            var left = skRect.Left;
            var top = skRect.Top;
            var right = skRect.Right;
            var  bottom = skRect.Bottom;

            var canvasRect = new SKRect(left, top, right, bottom);
            var width = canvasRect.Width;
            var  height = canvasRect.Height;

            chart.Draw(skCanvas, (int)width, (int)height);
        };

        // 4. Кладем холст в наш ContentView
        ChartView.Content = canvasView;
    }


    private void UpdateBalance()
    {
        decimal balance = _transactions.Sum(t => t.Amount);
        BalanceLabel.Text = $"{balance:F2} ₽";
        BalanceLabel.TextColor = balance >= 0 ? Colors.Green : Colors.Red;
    }

    private List<ChartEntry> GenerateChartEntries()
    {
        var entries = new List<ChartEntry>();

        // Группируем транзакции по категориям и суммируем Amount
        var grouped = _transactions
            .Where(t => t.Amount < 0) // Берем только расходы (отрицательные суммы)
            .GroupBy(t => t.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
            .OrderByDescending(x => x.Total) // Сортируем от самых больших трат
            .ToList();

        foreach (var group in grouped)
        {
            // Для графика нам нужно положительное число (высота столбца)
            float value = Convert.ToSingle(Math.Abs(group.Total)); 
        
            var entry = new ChartEntry(value)
            {
                Label = group.Category,
                ValueLabel = value.ToString("N0"), // Например: "3 500"
                Color = SKColor.Parse(GetCategoryColor(group.Category)) // Цвет столбца
            };
        
            entries.Add(entry);
        }

        return entries;
    }

// Простой метод для подбора цвета (чтобы не было одинаковых)
    private string GetCategoryColor(string category)
    {
        return category switch
        {
            "Продукты" => "#FF5722",   // Оранжевый
            "Транспорт" => "#2196F3", // Синий
            "Жилье" => "#9C27B0",     // Фиолетовый
            "Развлечения" => "#FFC107",// Желтый
            "Здоровье" => "#4CAF50",   // Зеленый
            _ => "#757575"            // Серый для "Разное"
        };
    }
    
    // Обработчик кнопки "Добавить"
    private async void OnAddTransactionClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DescriptionEntry.Text) || string.IsNullOrWhiteSpace(AmountEntry.Text))
        {
            await DisplayAlert("Ошибка", "Заполните описание и сумму!", "OK");
            return;
        }

        if (!decimal.TryParse(AmountEntry.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal amount))
        {
            await DisplayAlert("Ошибка", "Введите корректное число!", "OK");
            return;
        }

        try 
        {
            var newTransaction = new Transaction
            {
                Description = DescriptionEntry.Text,
                Amount = amount,
                // ✅ Берем выбранную категорию из Picker
                Category = CategoryPicker.SelectedItem?.ToString() ?? "Разное", 
                Date = DateTime.Now
            };

            await _dbService.SaveTransactionAsync(newTransaction);

            _transactions.Add(newTransaction);
            UpdateBalance();
            UpdateChart();

            DescriptionEntry.Text = string.Empty;
            AmountEntry.Text = string.Empty;
            CategoryPicker.SelectedIndex = -1; // Сброс выбора
        
            DescriptionEntry.Unfocus();
            AmountEntry.Unfocus();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка БД", $"Не удалось сохранить: {ex.Message}", "OK");
        }
    }

    private async void OnDeleteTransactionClicked(object sender, EventArgs e)
    {
        // Получаем кнопку, которая была нажата
        var button = sender as Button;
    
        // Получаем данные транзакции из BindingContext кнопки
        var transaction = button?.BindingContext as Transaction;

        if (transaction == null) return;

        // Спрашиваем подтверждение (хороший тон UX)
        bool confirm = await DisplayAlert("Подтверждение", $"Удалить запись: {transaction.Description}?", "Да", "Нет");

        if (!confirm) return;

        try 
        {
            await _dbService.DeleteTransactionAsync(transaction.Id);
        
            // Удаляем из коллекции (UI обновится автоматически)
            _transactions.Remove(transaction);
        
            UpdateBalance();
            UpdateChart();
        
            await DisplayAlert("Успех", "Запись удалена", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", $"Не удалось удалить: {ex.Message}", "OK");
        }
    }

}