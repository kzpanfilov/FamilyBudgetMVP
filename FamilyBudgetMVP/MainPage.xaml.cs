using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;
using System.Collections.ObjectModel;

namespace FamilyBudgetMVP;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _dbService;
    private ObservableCollection<Transaction> _transactions = new();

    public MainPage(DatabaseService dbService)
    {
        InitializeComponent();
        _dbService = dbService;
        
        // Привязываем коллекцию к UI
        TransactionsList.ItemsSource = _transactions;

        LoadData();
    }

    private async void LoadData()
    {
        try 
        {
            var data = await _dbService.GetTransactionsAsync();
            _transactions.Clear();
            foreach (var t in data)
            {
                _transactions.Add(t);
            }

            // Считаем баланс
            decimal balance = data.Sum(t => t.Amount);
            BalanceLabel.Text = $"{balance:F2} ₽";
            BalanceLabel.TextColor = balance >= 0 ? Colors.Green : Colors.Red;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки данных: {ex.Message}");
        }
    }
}