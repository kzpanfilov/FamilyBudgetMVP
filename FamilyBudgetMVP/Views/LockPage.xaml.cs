using FamilyBudgetMVP.Services;

namespace FamilyBudgetMVP.Views;

public partial class LockPage : ContentPage
{
    private readonly LockService _lock;

    public LockPage(LockService lockService)
    {
        _lock = lockService;
        InitializeComponent();
    }

    private async void OnUnlockClicked(object? sender, EventArgs e)
    {
        var pin = PinEntry.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(pin))
        {
            ErrorLabel.Text = "Введите PIN-код";
            return;
        }

        if (await _lock.VerifyPinAsync(pin))
        {
            ErrorLabel.Text = "";
            await Shell.Current.GoToAsync("//Main");
        }
        else
        {
            ErrorLabel.Text = "Неверный PIN-код";
            PinEntry.Text = "";
        }
    }
}
