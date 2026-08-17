using FamilyBudgetMVP.Models;
using SQLite;
using System.IO;

namespace FamilyBudgetMVP.Services
{
    public class DatabaseService
    {
        private readonly SQLiteAsyncConnection _database;

        // Путь к файлу базы данных в локальной папке приложения
        private static string DbPath => Path.Combine(FileSystem.AppDataDirectory, "budget.db");

        public DatabaseService()
        {
            // Создаем подключение. Если файла нет — он создастся.
            _database = new SQLiteAsyncConnection(DbPath);

            // Создаем таблицу, если её нет. Это безопасно вызывать много раз.
            _database.CreateTableAsync<Transaction>().Wait();
        }

        public Task<List<Transaction>> GetTransactionsAsync()
        {
            return _database.Table<Transaction>().ToListAsync();
        }

        public Task<int> SaveTransactionAsync(Transaction transaction)
        {
            if (transaction.Id == 0)
            {
                // Вставка новой записи
                return _database.InsertAsync(transaction);
            }
            else
            {
                // Обновление существующей
                return _database.UpdateAsync(transaction);
            }
        }

        public Task<int> DeleteTransactionAsync(int id)
        {
            return _database.DeleteAsync<Transaction>(id);
        }
    }
}