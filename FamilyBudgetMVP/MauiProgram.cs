using FamilyBudgetMVP.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace FamilyBudgetMVP;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseSkiaSharp()
			.UseLocalNotification()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});
        builder.Services.AddSingleton<TransactionService>();
        builder.Services.AddSingleton<CategoryStore>();
        builder.Services.AddSingleton<ScenarioService>();
        builder.Services.AddSingleton<BenefitsService>();

        // Палитра для графиков берётся из хранилища категорий
        builder.Services.AddSingleton<Services.ICategoryPalette>(sp => sp.GetRequiredService<CategoryStore>());

        builder.Services.AddSingleton<BudgetService>();
        builder.Services.AddSingleton<NotificationService>();
        builder.Services.AddSingleton<LockService>();
        builder.Services.AddTransient<FamilyBudgetMVP.Views.LockPage>();

        // Модалка добавления — transient: каждый раз новый экземпляр с пустой формой
        builder.Services.AddTransient<FamilyBudgetMVP.Views.AddTransactionPage>();

#if WINDOWS
        builder.Services.AddSingleton<FamilyBudgetMVP.Services.IFileSaver, FamilyBudgetMVP.Platforms.Windows.WindowsFileSaver>();
#else
        builder.Services.AddSingleton<FamilyBudgetMVP.Services.IFileSaver, FamilyBudgetMVP.Services.FallbackFileSaver>();
#endif

#if DEBUG
		builder.Logging.AddDebug();
#endif

#if WINDOWS
        // Необработанные исключения (включая stowed WinUI/XAML) — в файловый лог,
        // чтобы падение при старте можно было диагностировать по стеку
        Microsoft.UI.Xaml.Application.Current.UnhandledException += (_, e) =>
            Services.LogService.Error(e.Exception, $"WinUI: {e.Message}");

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                Services.LogService.Error(ex, "AppDomain");
        };
#endif

		var app = builder.Build();

		// Прогрев: инициализируем БД и кэш категорий в фоне (без UI-потока),
		// чтобы первая вкладка не ждала миграций
		Task.Run(async () =>
		{
			try
			{
				await app.Services.GetRequiredService<TransactionService>().InitializeAsync();
				await app.Services.GetRequiredService<CategoryStore>().InitializeAsync();
			}
			catch (Exception ex)
			{
				Services.LogService.Error(ex, "Фоновая инициализация сервисов");
			}
		});

		return app;
	}
}
