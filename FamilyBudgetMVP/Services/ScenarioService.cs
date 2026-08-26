using FamilyBudgetMVP.Models;
using SQLite;
using System.IO;

namespace FamilyBudgetMVP.Services
{
    /// <summary>Репозиторий сценариев «что если» (та же база, что у операций).</summary>
    public class ScenarioService
    {
        private readonly SQLiteAsyncConnection _database;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _initialized;

        private static string DbPath => Path.Combine(FileSystem.AppDataDirectory, "budget.db");

        public ScenarioService()
        {
            // Только подключение: инициализация схемы — в InitializeAsync()
            _database = new SQLiteAsyncConnection(DbPath);
        }

        /// <summary>Идемпотентная инициализация (миграции общие с операциями).</summary>
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

        public async Task<List<Scenario>> GetAllAsync()
        {
            await InitializeAsync();
            return await _database.Table<Scenario>().OrderByDescending(s => s.CreatedAt).ToListAsync();
        }

        public async Task<int> SaveAsync(Scenario scenario)
        {
            await InitializeAsync();
            return scenario.Id == 0
                ? await _database.InsertAsync(scenario)
                : await _database.UpdateAsync(scenario);
        }

        public async Task<int> DeleteAsync(int id)
        {
            await InitializeAsync();
            return await _database.DeleteAsync<Scenario>(id);
        }
    }
}
