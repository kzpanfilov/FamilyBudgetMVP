using FamilyBudgetMVP.Models;
using SQLite;
using System.IO;

namespace FamilyBudgetMVP.Services
{
    /// <summary>
    /// Репозиторий операций: CRUD поверх SQLite (sqlite-net-pcl).
    /// Файл базы лежит в локальной папке приложения и переживает перезапуски.
    /// </summary>
    public class TransactionService
    {
        private readonly SQLiteAsyncConnection _database;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _initialized;

        // Путь к файлу базы данных в локальной папке приложения
        private static string DbPath => Path.Combine(FileSystem.AppDataDirectory, "budget.db");

        public TransactionService()
        {
            // Только создание подключения. Вся работа с БД — в InitializeAsync(),
            // чтобы не блокировать UI-поток (см. комментарий в CategoryStore).
            _database = new SQLiteAsyncConnection(DbPath);
        }

        /// <summary>Идемпотентная инициализация схемы (миграции).</summary>
        public async Task InitializeAsync()
        {
            if (_initialized)
                return;

            await _initLock.WaitAsync();
            try
            {
                if (_initialized)
                    return;

                await DbMigrations.ApplyAsync(_database);
                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
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
