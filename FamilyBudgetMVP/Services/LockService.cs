namespace FamilyBudgetMVP.Services;

public class LockService
{
    private const string PinKey = "app_pin";
    private const string EnabledKey = "lock_enabled";

    public bool IsEnabled
    {
        get => Preferences.Get(EnabledKey, false);
        set => Preferences.Set(EnabledKey, value);
    }

    public async Task<bool> HasPinAsync()
    {
        try
        {
            var pin = await SecureStorage.GetAsync(PinKey);
            return !string.IsNullOrEmpty(pin);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> VerifyPinAsync(string pin)
    {
        try
        {
            var stored = await SecureStorage.GetAsync(PinKey);
            return stored == pin;
        }
        catch
        {
            return false;
        }
    }

    public async Task SetPinAsync(string pin)
    {
        await SecureStorage.SetAsync(PinKey, pin);
    }

    public async Task RemovePinAsync()
    {
        SecureStorage.Remove(PinKey);
    }
}
