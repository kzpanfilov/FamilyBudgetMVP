using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP;

public partial class App : Application
{
	private readonly DatabaseService _dbService;

	public App(DatabaseService dbService)
	{
		InitializeComponent();
		_dbService = dbService;

		// Тестовая запись: только если таблица пуста (простая проверка)
		CheckAndSeedDataAsync(); 

		//MainPage = new MainPage(dbService);
	}

	private async void CheckAndSeedDataAsync()
	{
		var count = await _dbService.GetTransactionsAsync();
		if (count.Count == 0)
		{
			var testTx = new Transaction
			{
				Description = "Тестовая запись при старте",
				Amount = 1000,
				Category = "Зарплата",
				Date = DateTime.Now
			};
			await _dbService.SaveTransactionAsync(testTx);
		}
	}

	protected override Window CreateWindow(IActivationState activationState)
	{
		var window = base.CreateWindow(activationState);
        
		// ✅ Создаем страницу здесь, передавая зависимости
		// Если DatabaseService зарегистрирован в MauiProgram.cs, MAUI сам его подставит
		var mainPage = new AppShell(); 
        
		window.Page = mainPage;
		return window;
	}
}