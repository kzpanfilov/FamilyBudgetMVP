using FamilyBudgetMVP.Services;
using Microsoft.Extensions.Logging;
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
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});
        builder.Services.AddSingleton<TransactionService>();
        builder.Services.AddSingleton<CategoryStore>();
        builder.Services.AddSingleton<BudgetService>();

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

		return builder.Build();
	}
}
