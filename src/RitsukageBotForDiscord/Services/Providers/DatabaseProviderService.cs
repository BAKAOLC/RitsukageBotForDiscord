using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using SQLite;

namespace RitsukageBot.Services.Providers
{
    /// <summary>
    ///     Database provider service.
    /// </summary>
    public class DatabaseProviderService : IDisposable, IAsyncDisposable
    {
        private readonly SQLiteAsyncConnection _connection;

        /// <summary>
        ///     Constructor.
        /// </summary>
        /// <param name="configuration"></param>
        /// <exception cref="Exception"></exception>
        public DatabaseProviderService(IConfiguration configuration)
        {
            var databasePath = configuration.GetValue<string>("Sqlite");
            if (string.IsNullOrEmpty(databasePath)) throw new("Database path is not set.");

            _connection = new(databasePath);

            // ReSharper disable once AsyncApostle.AsyncWait
            InitializeAsync().Wait();
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Get or create an object in the database.
        /// </summary>
        /// <param name="pk"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public async Task<(bool, T)> GetOrCreateAsync<T>(object pk) where T : new()
        {
            try
            {
                var result = await GetAsync<T>(pk).ConfigureAwait(false);
                return (true, result);
            }
            catch
            {
                // ignored
                return (false, new());
            }
        }

        /// <summary>
        ///     Insert or update an object in the database.
        /// </summary>
        /// <param name="obj"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public async Task<int> InsertOrUpdateAsync<T>(T obj) where T : new()
        {
            if (obj == null) return 0;
            var mapping = await _connection.GetMappingAsync(Orm.GetType(obj));
            var pk = mapping.PK ?? throw new NotSupportedException("Cannot update " + mapping.TableName + ": it has no PK");
            if (await FindAsync<T>(pk.GetValue(obj)).ConfigureAwait(false) != null)
                return await UpdateAsync(obj).ConfigureAwait(false);
            return await InsertAsync(obj).ConfigureAwait(false);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.CreateTableAsync{T}" />
        public Task<CreateTableResult> CreateTableAsync<T>(CreateFlags createFlags = CreateFlags.None) where T : new()
        {
            return _connection.CreateTableAsync<T>(createFlags);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.CreateTableAsync(Type, CreateFlags)" />
        public Task<CreateTableResult> CreateTableAsync(Type ty, CreateFlags createFlags = CreateFlags.None)
        {
            return _connection.CreateTableAsync(ty, createFlags);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.CreateTablesAsync{T, T2}(CreateFlags)" />
        public Task<CreateTablesResult> CreateTablesAsync<T, T2>(CreateFlags createFlags = CreateFlags.None)
            where T : new()
            where T2 : new()
        {
            return _connection.CreateTablesAsync<T, T2>(createFlags);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.CreateTablesAsync{T, T2, T3}(CreateFlags)" />
        public Task<CreateTablesResult> CreateTablesAsync<T, T2, T3>(CreateFlags createFlags = CreateFlags.None)
            where T : new()
            where T2 : new()
            where T3 : new()
        {
            return _connection.CreateTablesAsync<T, T2, T3>(createFlags);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.CreateTablesAsync{T, T2, T3, T4}(CreateFlags)" />
        public Task<CreateTablesResult> CreateTablesAsync<T, T2, T3, T4>(CreateFlags createFlags = CreateFlags.None)
            where T : new()
            where T2 : new()
            where T3 : new()
            where T4 : new()
        {
            return _connection.CreateTablesAsync<T, T2, T3, T4>(createFlags);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.CreateTablesAsync{T, T2, T3, T4, T5}(CreateFlags)" />
        public Task<CreateTablesResult> CreateTablesAsync<T, T2, T3, T4, T5>(CreateFlags createFlags = CreateFlags.None)
            where T : new()
            where T2 : new()
            where T3 : new()
            where T4 : new()
            where T5 : new()
        {
            return _connection.CreateTablesAsync<T, T2, T3, T4, T5>(createFlags);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.CreateTablesAsync(CreateFlags, Type[])" />
        public Task<CreateTablesResult> CreateTablesAsync(CreateFlags createFlags = CreateFlags.None,
            params Type[] types)
        {
            return _connection.CreateTablesAsync(createFlags, types);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.DropTableAsync{T}" />
        public Task<int> DropTableAsync<T>() where T : new()
        {
            return _connection.DropTableAsync<T>();
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.DropTableAsync(TableMapping)" />
        public Task<int> DropTableAsync(TableMapping map)
        {
            return _connection.DropTableAsync(map);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.CreateIndexAsync(string, string, bool)" />
        public Task<int> CreateIndexAsync(string tableName, string columnName, bool unique = false)
        {
            return _connection.CreateIndexAsync(tableName, columnName, unique);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.CreateIndexAsync(string, string, string, bool)" />
        public Task<int> CreateIndexAsync(
            string indexName,
            string tableName,
            string columnName,
            bool unique = false)
        {
            return _connection.CreateIndexAsync(indexName, tableName, columnName, unique);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.CreateIndexAsync(string, string[], bool)" />
        public Task<int> CreateIndexAsync(string tableName, string[] columnNames, bool unique = false)
        {
            return _connection.CreateIndexAsync(tableName, columnNames, unique);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.CreateIndexAsync(string, string, string[], bool)" />
        public Task<int> CreateIndexAsync(
            string indexName,
            string tableName,
            string[] columnNames,
            bool unique = false)
        {
            return _connection.CreateIndexAsync(indexName, tableName, columnNames, unique);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.CreateIndexAsync{T}(Expression{Func{T, object}}, bool)" />
        public Task<int> CreateIndexAsync<T>(Expression<Func<T, object>> property, bool unique = false)
        {
            return _connection.CreateIndexAsync(property, unique);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.InsertAsync(object)" />
        public Task<int> InsertAsync(object obj)
        {
            return _connection.InsertAsync(obj);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.InsertAsync(object, Type)" />
        public Task<int> InsertAsync(object obj, Type objType)
        {
            return _connection.InsertAsync(obj, objType);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.InsertAsync(object, string)" />
        public Task<int> InsertAsync(object obj, string extra)
        {
            return _connection.InsertAsync(obj, extra);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.InsertAsync(object, string, Type)" />
        public Task<int> InsertAsync(object obj, string extra, Type objType)
        {
            return _connection.InsertAsync(obj, extra, objType);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.InsertOrReplaceAsync(object)" />
        public Task<int> InsertOrReplaceAsync(object obj)
        {
            return _connection.InsertOrReplaceAsync(obj);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.InsertOrReplaceAsync(object, Type)" />
        public Task<int> InsertOrReplaceAsync(object obj, Type objType)
        {
            return _connection.InsertOrReplaceAsync(obj, objType);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.UpdateAsync(object)" />
        public Task<int> UpdateAsync(object obj)
        {
            return _connection.UpdateAsync(obj);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.UpdateAsync(object, Type)" />
        public Task<int> UpdateAsync(object obj, Type objType)
        {
            return _connection.UpdateAsync(obj, objType);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.UpdateAllAsync(IEnumerable, bool)" />
        public Task<int> UpdateAllAsync(IEnumerable objects, bool runInTransaction = true)
        {
            return _connection.UpdateAllAsync(objects, runInTransaction);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.DeleteAsync(object)" />
        public Task<int> DeleteAsync(object objectToDelete)
        {
            return _connection.DeleteAsync(objectToDelete);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.DeleteAsync{T}(object)" />
        public Task<int> DeleteAsync<T>(object primaryKey)
        {
            return _connection.DeleteAsync<T>(primaryKey);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.DeleteAsync(object, TableMapping)" />
        public Task<int> DeleteAsync(object primaryKey, TableMapping map)
        {
            return _connection.DeleteAsync(primaryKey, map);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.DeleteAllAsync{T}()" />
        public Task<int> DeleteAllAsync<T>()
        {
            return _connection.DeleteAllAsync<T>();
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.DeleteAllAsync(TableMapping)" />
        public Task<int> DeleteAllAsync(TableMapping map)
        {
            return _connection.DeleteAllAsync(map);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.BackupAsync(string, string)" />
        public Task BackupAsync(string destinationDatabasePath, string databaseName = "main")
        {
            return _connection.BackupAsync(destinationDatabasePath, databaseName);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.GetAsync{T}(object)" />
        public Task<T> GetAsync<T>(object pk) where T : new()
        {
            return _connection.GetAsync<T>(pk);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.GetAsync(object, TableMapping)" />
        public Task<object> GetAsync(object pk, TableMapping map)
        {
            return _connection.GetAsync(pk, map);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.GetAsync{T}(Expression{Func{T, bool}})" />
        public Task<T> GetAsync<T>(Expression<Func<T, bool>> predicate) where T : new()
        {
            return _connection.GetAsync(predicate);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.FindAsync{T}(object)" />
        public Task<T> FindAsync<T>(object pk) where T : new()
        {
            return _connection.FindAsync<T>(pk);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.FindAsync(object, TableMapping)" />
        public Task<object> FindAsync(object pk, TableMapping map)
        {
            return _connection.FindAsync(pk, map);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.FindAsync{T}(Expression{Func{T, bool}})" />
        public Task<T> FindAsync<T>(Expression<Func<T, bool>> predicate) where T : new()
        {
            return _connection.FindAsync(predicate);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.FindWithQueryAsync{T}(string, object[])" />
        public Task<T> FindWithQueryAsync<T>(string query, params object[] args) where T : new()
        {
            return _connection.FindWithQueryAsync<T>(query, args);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.FindWithQueryAsync(TableMapping, string, object[])" />
        public Task<object> FindWithQueryAsync(TableMapping map, string query, params object[] args)
        {
            return _connection.FindWithQueryAsync(map, query, args);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.GetMappingAsync(Type, CreateFlags)" />
        public Task<TableMapping> GetMappingAsync(Type type, CreateFlags createFlags = CreateFlags.None)
        {
            return _connection.GetMappingAsync(type, createFlags);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.GetMappingAsync{T}(CreateFlags)" />
        public Task<TableMapping> GetMappingAsync<T>(CreateFlags createFlags = CreateFlags.None) where T : new()
        {
            return _connection.GetMappingAsync<T>(createFlags);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.GetTableInfoAsync(string)" />
        public Task<List<SQLiteConnection.ColumnInfo>> GetTableInfoAsync(string tableName)
        {
            return _connection.GetTableInfoAsync(tableName);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.ExecuteAsync(string, object[])" />
        public Task<int> ExecuteAsync(string query, params object[] args)
        {
            return _connection.ExecuteAsync(query, args);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.InsertAllAsync(IEnumerable, bool)" />
        public Task<int> InsertAllAsync(IEnumerable objects, bool runInTransaction = true)
        {
            return _connection.InsertAllAsync(objects, runInTransaction);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.InsertAllAsync(IEnumerable, string, bool)" />
        public Task<int> InsertAllAsync(IEnumerable objects, string extra, bool runInTransaction = true)
        {
            return _connection.InsertAllAsync(objects, extra, runInTransaction);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.InsertAllAsync(IEnumerable, Type, bool)" />
        public Task<int> InsertAllAsync(IEnumerable objects, Type objType, bool runInTransaction = true)
        {
            return _connection.InsertAllAsync(objects, objType, runInTransaction);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.RunInTransactionAsync(Action{SQLiteConnection})" />
        public Task RunInTransactionAsync(Action<SQLiteConnection> action)
        {
            return _connection.RunInTransactionAsync(action);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.Table{T}" />
        public AsyncTableQuery<T> Table<T>() where T : new()
        {
            return _connection.Table<T>();
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.ExecuteScalarAsync{T}(string, object[])" />
        public Task<T> ExecuteScalarAsync<T>(string query, params object[] args)
        {
            return _connection.ExecuteScalarAsync<T>(query, args);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.QueryAsync{T}(string, object[])" />
        public Task<List<T>> QueryAsync<T>(string query, params object[] args) where T : new()
        {
            return _connection.QueryAsync<T>(query, args);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.QueryScalarsAsync{T}(string, object[])" />
        public Task<List<T>> QueryScalarsAsync<T>(string query, params object[] args)
        {
            return _connection.QueryScalarsAsync<T>(query, args);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.QueryAsync(TableMapping, string, object[])" />
        public Task<List<object>> QueryAsync(TableMapping map, string query, params object[] args)
        {
            return _connection.QueryAsync(map, query, args);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.DeferredQueryAsync{T}(string, object[])" />
        public Task<IEnumerable<T>> DeferredQueryAsync<T>(string query, params object[] args) where T : new()
        {
            return _connection.DeferredQueryAsync<T>(query, args);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.DeferredQueryAsync(TableMapping, string, object[])" />
        public Task<IEnumerable<object>> DeferredQueryAsync(
            TableMapping map,
            string query,
            params object[] args)
        {
            return _connection.DeferredQueryAsync(map, query, args);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.ReKeyAsync(string)" />
        public Task ReKeyAsync(string key)
        {
            return _connection.ReKeyAsync(key);
        }

        /// <inheritdoc cref="SQLiteAsyncConnection.ReKeyAsync(byte[])" />
        public Task ReKeyAsync(byte[] key)
        {
            return _connection.ReKeyAsync(key);
        }

        private async Task InitializeAsync()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes()
                .Where(type => type.GetCustomAttributes<TableAttribute>().Any()).ToArray();

            await _connection.CreateTablesAsync(CreateFlags.None, types).ConfigureAwait(false);
        }

        /// <summary>
        ///     Releases all resources used by the <see cref="DatabaseProviderService" /> object.
        /// </summary>
        ~DatabaseProviderService()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!disposing) return;
            _connection.CloseAsync().RunSynchronously();
        }

        private async ValueTask DisposeAsyncCore()
        {
            await _connection.CloseAsync().ConfigureAwait(false);
        }
    }
}