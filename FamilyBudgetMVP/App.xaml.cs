using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP;

public partial class App : Application
{
	private readonly LockService _lock;

	public App(LockService lockService)
	{
		_lock = lockService;
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}

	protected override async void OnStart()
	{
		base.OnStart();

		if (!_lock.IsEnabled)
			return;

		if (!await _lock.HasPinAsync())
			return;

		// Перекидываем на страницу блокировки
		await Shell.Current.GoToAsync("//lock");
	}
}
