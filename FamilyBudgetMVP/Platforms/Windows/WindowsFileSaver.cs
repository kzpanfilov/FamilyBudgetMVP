using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using FamilyBudgetMVP.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Application = Microsoft.Maui.Controls.Application;
using WinWindow = Microsoft.UI.Xaml.Window;

namespace FamilyBudgetMVP.Platforms.Windows
{
    /// <summary>Диалог сохранения Windows (WinUI FileSavePicker).</summary>
    public class WindowsFileSaver : IFileSaver
    {
        public async Task<string?> SaveTextAsync(string suggestedFileName, string content)
        {
            var window = Application.Current?.Windows?.FirstOrDefault()?.Handler?.PlatformView as WinWindow;
            if (window == null)
                throw new InvalidOperationException("Окно приложения недоступно.");

            var picker = new FileSavePicker();

            // Пикуеру нужно окно-владелец
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));

            picker.SuggestedFileName = suggestedFileName;
            picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });

            var file = await picker.PickSaveFileAsync();
            if (file == null)
                return null;

            // UTF-8 с BOM — чтобы русский Excel корректно открыл кириллицу
            byte[] payload = Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(content))
                .ToArray();

            await FileIO.WriteBytesAsync(file, payload);
            return file.Path;
        }
    }
}
