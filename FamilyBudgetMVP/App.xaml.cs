using FamilyBudgetMVP.Models;
using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState activationState)
	{
		var window = new Window();

		var ctx = activationState.Context;
		var services = ctx.Services;
		var dbService = services.GetRequiredService<DatabaseService>();

		var mainPage = new MainPage(dbService); 
        
		window.Page = mainPage;
		return window;
	}
}