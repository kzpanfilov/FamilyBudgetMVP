using System.Text;

namespace FamilyBudgetMVP.Services
{
    /// <summary>Сохранение текстового файла с системным диалогом.</summary>
    public interface IFileSaver
    {
        /// <returns>Путь сохранённого файла либо null, если пользователь отменил.</returns>
        Task<string?> SaveTextAsync(string suggestedFileName, string content);
    }

    /// <summary>Запасная реализация для платформ без диалога сохранения.</summary>
    public class FallbackFileSaver : IFileSaver
    {
        public async Task<string?> SaveTextAsync(string suggestedFileName, string content)
        {
            string dir = FileSystem.AppDataDirectory;
            string path = Path.Combine(dir, suggestedFileName);
            await File.WriteAllTextAsync(path, content, new UTF8Encoding(true));
            return path;
        }
    }
}
