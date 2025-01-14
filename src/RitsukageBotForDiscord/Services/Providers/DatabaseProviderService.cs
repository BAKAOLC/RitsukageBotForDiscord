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
            InitializeAsync().Wait();
        }

        /// <summary>
        ///     Dispose async.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore().ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Executes a "create table if not exists" on the database. It also
        ///     creates any specified indexes on the columns of the table. It uses
        ///     a schema automatically generated from the specified type. You can
        ///     later access this schema by calling GetMapping.
        /// </summary>
        /// <returns>Whether the table was created or migrated.</returns>
        public Task<CreateTableResult> CreateTableAsync<T>(CreateFlags createFlags = CreateFlags.None) where T : new()
        {
            return _connection.CreateTableAsync<T>(createFlags);
        }

        /// <summary>
        ///     Executes a "create table if not exists" on the database. It also
        ///     creates any specified indexes on the columns of the table. It uses
        ///     a schema automatically generated from the specified type. You can
        ///     later access this schema by calling GetMapping.
        /// </summary>
        /// <param name="ty">Type to reflect to a database table.</param>
        /// <param name="createFlags">Optional flags allowing implicit PK and indexes based on naming conventions.</param>
        /// <returns>Whether the table was created or migrated.</returns>
        public Task<CreateTableResult> CreateTableAsync(Type ty, CreateFlags createFlags = CreateFlags.None)
        {
            return _connection.CreateTableAsync(ty, createFlags);
        }

        /// <summary>
        ///     Executes a "create table if not exists" on the database for each type. It also
        ///     creates any specified indexes on the columns of the table. It uses
        ///     a schema automatically generated from the specified type. You can
        ///     later access this schema by calling GetMapping.
        /// </summary>
        /// <returns>
        ///     Whether the table was created or migrated for each type.
        /// </returns>
        public Task<CreateTablesResult> CreateTablesAsync<T, T2>(CreateFlags createFlags = CreateFlags.None)
            where T : new()
            where T2 : new()
        {
            return _connection.CreateTablesAsync<T, T2>(createFlags);
        }

        /// <summary>
        ///     Executes a "create table if not exists" on the database for each type. It also
        ///     creates any specified indexes on the columns of the table. It uses
        ///     a schema automatically generated from the specified type. You can
        ///     later access this schema by calling GetMapping.
        /// </summary>
        /// <returns>
        ///     Whether the table was created or migrated for each type.
        /// </returns>
        public Task<CreateTablesResult> CreateTablesAsync<T, T2, T3>(CreateFlags createFlags = CreateFlags.None)
            where T : new()
            where T2 : new()
            where T3 : new()
        {
            return _connection.CreateTablesAsync<T, T2, T3>(createFlags);
        }

        /// <summary>
        ///     Executes a "create table if not exists" on the database for each type. It also
        ///     creates any specified indexes on the columns of the table. It uses
        ///     a schema automatically generated from the specified type. You can
        ///     later access this schema by calling GetMapping.
        /// </summary>
        /// <returns>
        ///     Whether the table was created or migrated for each type.
        /// </returns>
        public Task<CreateTablesResult> CreateTablesAsync<T, T2, T3, T4>(CreateFlags createFlags = CreateFlags.None)
            where T : new()
            where T2 : new()
            where T3 : new()
            where T4 : new()
        {
            return _connection.CreateTablesAsync<T, T2, T3, T4>(createFlags);
        }

        /// <summary>
        ///     Executes a "create table if not exists" on the database for each type. It also
        ///     creates any specified indexes on the columns of the table. It uses
        ///     a schema automatically generated from the specified type. You can
        ///     later access this schema by calling GetMapping.
        /// </summary>
        /// <returns>
        ///     Whether the table was created or migrated for each type.
        /// </returns>
        public Task<CreateTablesResult> CreateTablesAsync<T, T2, T3, T4, T5>(CreateFlags createFlags = CreateFlags.None)
            where T : new()
            where T2 : new()
            where T3 : new()
            where T4 : new()
            where T5 : new()
        {
            return _connection.CreateTablesAsync<T, T2, T3, T4, T5>(createFlags);
        }

        /// <summary>
        ///     Executes a "create table if not exists" on the database for each type. It also
        ///     creates any specified indexes on the columns of the table. It uses
        ///     a schema automatically generated from the specified type. You can
        ///     later access this schema by calling GetMapping.
        /// </summary>
        /// <returns>
        ///     Whether the table was created or migrated for each type.
        /// </returns>
        public Task<CreateTablesResult> CreateTablesAsync(CreateFlags createFlags = CreateFlags.None, params Type[] types)
        {
            return _connection.CreateTablesAsync(createFlags, types);
        }

        /// <summary>
        ///     Executes a "drop table" on the database.  This is non-recoverable.
        /// </summary>
        public Task<int> DropTableAsync<T>() where T : new()
        {
            return _connection.DropTableAsync<T>();
        }

        /// <summary>
        ///     Executes a "drop table" on the database.  This is non-recoverable.
        /// </summary>
        /// <param name="map">The TableMapping used to identify the table.</param>
        public Task<int> DropTableAsync(TableMapping map)
        {
            return _connection.DropTableAsync(map);
        }

        /// <summary>Creates an index for the specified table and column.</summary>
        /// <param name="tableName">Name of the database table</param>
        /// <param name="columnName">Name of the column to index</param>
        /// <param name="unique">Whether the index should be unique</param>
        /// <returns>Zero on success.</returns>
        public Task<int> CreateIndexAsync(string tableName, string columnName, bool unique = false)
        {
            return _connection.CreateIndexAsync(tableName, columnName, unique);
        }

        /// <summary>Creates an index for the specified table and column.</summary>
        /// <param name="indexName">Name of the index to create</param>
        /// <param name="tableName">Name of the database table</param>
        /// <param name="columnName">Name of the column to index</param>
        /// <param name="unique">Whether the index should be unique</param>
        /// <returns>Zero on success.</returns>
        public Task<int> CreateIndexAsync(
            string indexName,
            string tableName,
            string columnName,
            bool unique = false)
        {
            return _connection.CreateIndexAsync(indexName, tableName, columnName, unique);
        }

        /// <summary>Creates an index for the specified table and columns.</summary>
        /// <param name="tableName">Name of the database table</param>
        /// <param name="columnNames">An array of column names to index</param>
        /// <param name="unique">Whether the index should be unique</param>
        /// <returns>Zero on success.</returns>
        public Task<int> CreateIndexAsync(string tableName, string[] columnNames, bool unique = false)
        {
            return _connection.CreateIndexAsync(tableName, columnNames, unique);
        }

        /// <summary>Creates an index for the specified table and columns.</summary>
        /// <param name="indexName">Name of the index to create</param>
        /// <param name="tableName">Name of the database table</param>
        /// <param name="columnNames">An array of column names to index</param>
        /// <param name="unique">Whether the index should be unique</param>
        /// <returns>Zero on success.</returns>
        public Task<int> CreateIndexAsync(
            string indexName,
            string tableName,
            string[] columnNames,
            bool unique = false)
        {
            return _connection.CreateIndexAsync(indexName, tableName, columnNames, unique);
        }

        /// <summary>
        ///     Creates an index for the specified object property.
        ///     e.g. CreateIndex&lt;Client&gt;(c =&gt; c.Name);
        /// </summary>
        /// <typeparam name="T">Type to reflect to a database table.</typeparam>
        /// <param name="property">Property to index</param>
        /// <param name="unique">Whether the index should be unique</param>
        /// <returns>Zero on success.</returns>
        public Task<int> CreateIndexAsync<T>(Expression<Func<T, object>> property, bool unique = false)
        {
            return _connection.CreateIndexAsync(property, unique);
        }

        /// <summary>
        ///     Inserts the given object and (and updates its
        ///     auto incremented primary key if it has one).
        /// </summary>
        /// <param name="obj">The object to insert.</param>
        /// <returns>The number of rows added to the table.</returns>
        public Task<int> InsertAsync(object obj)
        {
            return _connection.InsertAsync(obj);
        }

        /// <summary>
        ///     Inserts the given object (and updates its
        ///     auto incremented primary key if it has one).
        ///     The return value is the number of rows added to the table.
        /// </summary>
        /// <param name="obj">The object to insert.</param>
        /// <param name="objType">The type of object to insert.</param>
        /// <returns>The number of rows added to the table.</returns>
        public Task<int> InsertAsync(object obj, Type objType)
        {
            return _connection.InsertAsync(obj, objType);
        }

        /// <summary>
        ///     Inserts the given object (and updates its
        ///     auto incremented primary key if it has one).
        ///     The return value is the number of rows added to the table.
        /// </summary>
        /// <param name="obj">The object to insert.</param>
        /// <param name="extra">
        ///     Literal SQL code that gets placed into the command. INSERT {extra} INTO ...
        /// </param>
        /// <returns>The number of rows added to the table.</returns>
        public Task<int> InsertAsync(object obj, string extra)
        {
            return _connection.InsertAsync(obj, extra);
        }

        /// <summary>
        ///     Inserts the given object (and updates its
        ///     auto incremented primary key if it has one).
        ///     The return value is the number of rows added to the table.
        /// </summary>
        /// <param name="obj">The object to insert.</param>
        /// <param name="extra">
        ///     Literal SQL code that gets placed into the command. INSERT {extra} INTO ...
        /// </param>
        /// <param name="objType">The type of object to insert.</param>
        /// <returns>The number of rows added to the table.</returns>
        public Task<int> InsertAsync(object obj, string extra, Type objType)
        {
            return _connection.InsertAsync(obj, extra, objType);
        }

        /// <summary>
        ///     Inserts the given object (and updates its
        ///     auto incremented primary key if it has one).
        ///     The return value is the number of rows added to the table.
        ///     If a UNIQUE constraint violation occurs with
        ///     some pre-existing object, this function deletes
        ///     the old object.
        /// </summary>
        /// <param name="obj">The object to insert.</param>
        /// <returns>The number of rows modified.</returns>
        public Task<int> InsertOrReplaceAsync(object obj)
        {
            return _connection.InsertOrReplaceAsync(obj);
        }

        /// <summary>
        ///     Inserts the given object (and updates its
        ///     auto incremented primary key if it has one).
        ///     The return value is the number of rows added to the table.
        ///     If a UNIQUE constraint violation occurs with
        ///     some pre-existing object, this function deletes
        ///     the old object.
        /// </summary>
        /// <param name="obj">The object to insert.</param>
        /// <param name="objType">The type of object to insert.</param>
        /// <returns>The number of rows modified.</returns>
        public Task<int> InsertOrReplaceAsync(object obj, Type objType)
        {
            return _connection.InsertOrReplaceAsync(obj, objType);
        }

        /// <summary>
        ///     Updates all of the columns of a table using the specified object
        ///     except for its primary key.
        ///     The object is required to have a primary key.
        /// </summary>
        /// <param name="obj">
        ///     The object to update. It must have a primary key designated using the PrimaryKeyAttribute.
        /// </param>
        /// <returns>The number of rows updated.</returns>
        public Task<int> UpdateAsync(object obj)
        {
            return _connection.UpdateAsync(obj);
        }

        /// <summary>
        ///     Updates all of the columns of a table using the specified object
        ///     except for its primary key.
        ///     The object is required to have a primary key.
        /// </summary>
        /// <param name="obj">
        ///     The object to update. It must have a primary key designated using the PrimaryKeyAttribute.
        /// </param>
        /// <param name="objType">The type of object to insert.</param>
        /// <returns>The number of rows updated.</returns>
        public Task<int> UpdateAsync(object obj, Type objType)
        {
            return _connection.UpdateAsync(obj, objType);
        }

        /// <summary>Updates all specified objects.</summary>
        /// <param name="objects">
        ///     An <see cref="T:System.Collections.IEnumerable" /> of the objects to insert.
        /// </param>
        /// <param name="runInTransaction">
        ///     A boolean indicating if the inserts should be wrapped in a transaction
        /// </param>
        /// <returns>The number of rows modified.</returns>
        public Task<int> UpdateAllAsync(IEnumerable objects, bool runInTransaction = true)
        {
            return _connection.UpdateAllAsync(objects, runInTransaction);
        }

        /// <summary>
        ///     Deletes the given object from the database using its primary key.
        /// </summary>
        /// <param name="objectToDelete">
        ///     The object to delete. It must have a primary key designated using the PrimaryKeyAttribute.
        /// </param>
        /// <returns>The number of rows deleted.</returns>
        public Task<int> DeleteAsync(object objectToDelete)
        {
            return _connection.DeleteAsync(objectToDelete);
        }

        /// <summary>Deletes the object with the specified primary key.</summary>
        /// <param name="primaryKey">The primary key of the object to delete.</param>
        /// <returns>The number of objects deleted.</returns>
        /// <typeparam name="T">The type of object.</typeparam>
        public Task<int> DeleteAsync<T>(object primaryKey)
        {
            return _connection.DeleteAsync<T>(primaryKey);
        }

        /// <summary>Deletes the object with the specified primary key.</summary>
        /// <param name="primaryKey">The primary key of the object to delete.</param>
        /// <param name="map">The TableMapping used to identify the table.</param>
        /// <returns>The number of objects deleted.</returns>
        public Task<int> DeleteAsync(object primaryKey, TableMapping map)
        {
            return _connection.DeleteAsync(primaryKey, map);
        }

        /// <summary>
        ///     Deletes all the objects from the specified table.
        ///     WARNING WARNING: Let me repeat. It deletes ALL the objects from the
        ///     specified table. Do you really want to do that?
        /// </summary>
        /// <returns>The number of objects deleted.</returns>
        /// <typeparam name="T">The type of objects to delete.</typeparam>
        public Task<int> DeleteAllAsync<T>()
        {
            return _connection.DeleteAllAsync<T>();
        }

        /// <summary>
        ///     Deletes all the objects from the specified table.
        ///     WARNING WARNING: Let me repeat. It deletes ALL the objects from the
        ///     specified table. Do you really want to do that?
        /// </summary>
        /// <param name="map">The TableMapping used to identify the table.</param>
        /// <returns>The number of objects deleted.</returns>
        public Task<int> DeleteAllAsync(TableMapping map)
        {
            return _connection.DeleteAllAsync(map);
        }

        /// <summary>Backup the entire database to the specified path.</summary>
        /// <param name="destinationDatabasePath">Path to backup file.</param>
        /// <param name="databaseName">The name of the database to backup (usually "main").</param>
        public Task BackupAsync(string destinationDatabasePath, string databaseName = "main")
        {
            return _connection.BackupAsync(destinationDatabasePath, databaseName);
        }

        /// <summary>
        ///     Attempts to retrieve an object with the given primary key from the table
        ///     associated with the specified type. Use of this method requires that
        ///     the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
        /// </summary>
        /// <param name="pk">The primary key.</param>
        /// <returns>
        ///     The object with the given primary key. Throws a not found exception
        ///     if the object is not found.
        /// </returns>
        public Task<T> GetAsync<T>(object pk) where T : new()
        {
            return _connection.GetAsync<T>(pk);
        }

        /// <summary>
        ///     Attempts to retrieve an object with the given primary key from the table
        ///     associated with the specified type. Use of this method requires that
        ///     the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
        /// </summary>
        /// <param name="pk">The primary key.</param>
        /// <param name="map">The TableMapping used to identify the table.</param>
        /// <returns>
        ///     The object with the given primary key. Throws a not found exception
        ///     if the object is not found.
        /// </returns>
        public Task<object> GetAsync(object pk, TableMapping map)
        {
            return _connection.GetAsync(pk, map);
        }

        /// <summary>
        ///     Attempts to retrieve the first object that matches the predicate from the table
        ///     associated with the specified type.
        /// </summary>
        /// <param name="predicate">A predicate for which object to find.</param>
        /// <returns>
        ///     The object that matches the given predicate. Throws a not found exception
        ///     if the object is not found.
        /// </returns>
        public Task<T> GetAsync<T>(Expression<Func<T, bool>> predicate) where T : new()
        {
            return _connection.GetAsync(predicate);
        }

        /// <summary>
        ///     Attempts to retrieve an object with the given primary key from the table
        ///     associated with the specified type. Use of this method requires that
        ///     the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
        /// </summary>
        /// <param name="pk">The primary key.</param>
        /// <returns>
        ///     The object with the given primary key or null
        ///     if the object is not found.
        /// </returns>
        public Task<T> FindAsync<T>(object pk) where T : new()
        {
            return _connection.FindAsync<T>(pk);
        }

        /// <summary>
        ///     Attempts to retrieve an object with the given primary key from the table
        ///     associated with the specified type. Use of this method requires that
        ///     the given type have a designated PrimaryKey (using the PrimaryKeyAttribute).
        /// </summary>
        /// <param name="pk">The primary key.</param>
        /// <param name="map">The TableMapping used to identify the table.</param>
        /// <returns>
        ///     The object with the given primary key or null
        ///     if the object is not found.
        /// </returns>
        public Task<object> FindAsync(object pk, TableMapping map)
        {
            return _connection.FindAsync(pk, map);
        }

        /// <summary>
        ///     Attempts to retrieve the first object that matches the predicate from the table
        ///     associated with the specified type.
        /// </summary>
        /// <param name="predicate">A predicate for which object to find.</param>
        /// <returns>
        ///     The object that matches the given predicate or null
        ///     if the object is not found.
        /// </returns>
        public Task<T> FindAsync<T>(Expression<Func<T, bool>> predicate) where T : new()
        {
            return _connection.FindAsync(predicate);
        }

        /// <summary>
        ///     Attempts to retrieve the first object that matches the query from the table
        ///     associated with the specified type.
        /// </summary>
        /// <param name="query">The fully escaped SQL.</param>
        /// <param name="args">
        ///     Arguments to substitute for the occurences of '?' in the query.
        /// </param>
        /// <returns>
        ///     The object that matches the given predicate or null
        ///     if the object is not found.
        /// </returns>
        public Task<T> FindWithQueryAsync<T>(string query, params object[] args) where T : new()
        {
            return _connection.FindWithQueryAsync<T>(query, args);
        }

        /// <summary>
        ///     Attempts to retrieve the first object that matches the query from the table
        ///     associated with the specified type.
        /// </summary>
        /// <param name="map">The TableMapping used to identify the table.</param>
        /// <param name="query">The fully escaped SQL.</param>
        /// <param name="args">
        ///     Arguments to substitute for the occurences of '?' in the query.
        /// </param>
        /// <returns>
        ///     The object that matches the given predicate or null
        ///     if the object is not found.
        /// </returns>
        public Task<object> FindWithQueryAsync(TableMapping map, string query, params object[] args)
        {
            return _connection.FindWithQueryAsync(map, query, args);
        }

        /// <summary>
        ///     Retrieves the mapping that is automatically generated for the given type.
        /// </summary>
        /// <param name="type">
        ///     The type whose mapping to the database is returned.
        /// </param>
        /// <param name="createFlags">
        ///     Optional flags allowing implicit PK and indexes based on naming conventions
        /// </param>
        /// <returns>
        ///     The mapping represents the schema of the columns of the database and contains
        ///     methods to set and get properties of objects.
        /// </returns>
        public Task<TableMapping> GetMappingAsync(Type type, CreateFlags createFlags = CreateFlags.None)
        {
            return _connection.GetMappingAsync(type, createFlags);
        }

        /// <summary>
        ///     Retrieves the mapping that is automatically generated for the given type.
        /// </summary>
        /// <param name="createFlags">
        ///     Optional flags allowing implicit PK and indexes based on naming conventions
        /// </param>
        /// <returns>
        ///     The mapping represents the schema of the columns of the database and contains
        ///     methods to set and get properties of objects.
        /// </returns>
        public Task<TableMapping> GetMappingAsync<T>(CreateFlags createFlags = CreateFlags.None) where T : new()
        {
            return _connection.GetMappingAsync<T>(createFlags);
        }

        /// <summary>
        ///     Query the built-in sqlite table_info table for a specific tables columns.
        /// </summary>
        /// <returns>The columns contains in the table.</returns>
        /// <param name="tableName">Table name.</param>
        public Task<List<SQLiteConnection.ColumnInfo>> GetTableInfoAsync(string tableName)
        {
            return _connection.GetTableInfoAsync(tableName);
        }

        /// <summary>
        ///     Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
        ///     in the command text for each of the arguments and then executes that command.
        ///     Use this method instead of Query when you don't expect rows back. Such cases include
        ///     INSERTs, UPDATEs, and DELETEs.
        ///     You can set the Trace or TimeExecution properties of the connection
        ///     to profile execution.
        /// </summary>
        /// <param name="query">The fully escaped SQL.</param>
        /// <param name="args">
        ///     Arguments to substitute for the occurences of '?' in the query.
        /// </param>
        /// <returns>
        ///     The number of rows modified in the database as a result of this execution.
        /// </returns>
        public Task<int> ExecuteAsync(string query, params object[] args)
        {
            return _connection.ExecuteAsync(query, args);
        }

        /// <summary>Inserts all specified objects.</summary>
        /// <param name="objects">
        ///     An <see cref="T:System.Collections.IEnumerable" /> of the objects to insert.
        ///     <param name="runInTransaction" />
        ///     A boolean indicating if the inserts should be wrapped in a transaction.
        /// </param>
        /// <returns>The number of rows added to the table.</returns>
        public Task<int> InsertAllAsync(IEnumerable objects, bool runInTransaction = true)
        {
            return _connection.InsertAllAsync(objects, runInTransaction);
        }

        /// <summary>Inserts all specified objects.</summary>
        /// <param name="objects">
        ///     An <see cref="T:System.Collections.IEnumerable" /> of the objects to insert.
        /// </param>
        /// <param name="extra">
        ///     Literal SQL code that gets placed into the command. INSERT {extra} INTO ...
        /// </param>
        /// <param name="runInTransaction">
        ///     A boolean indicating if the inserts should be wrapped in a transaction.
        /// </param>
        /// <returns>The number of rows added to the table.</returns>
        public Task<int> InsertAllAsync(IEnumerable objects, string extra, bool runInTransaction = true)
        {
            return _connection.InsertAllAsync(objects, extra, runInTransaction);
        }

        /// <summary>Inserts all specified objects.</summary>
        /// <param name="objects">
        ///     An <see cref="T:System.Collections.IEnumerable" /> of the objects to insert.
        /// </param>
        /// <param name="objType">The type of object to insert.</param>
        /// <param name="runInTransaction">
        ///     A boolean indicating if the inserts should be wrapped in a transaction.
        /// </param>
        /// <returns>The number of rows added to the table.</returns>
        public Task<int> InsertAllAsync(IEnumerable objects, Type objType, bool runInTransaction = true)
        {
            return _connection.InsertAllAsync(objects, objType, runInTransaction);
        }

        /// <summary>
        ///     Executes <paramref name="action" /> within a (possibly nested) transaction by wrapping it in a SAVEPOINT. If an
        ///     exception occurs the whole transaction is rolled back, not just the current savepoint. The exception
        ///     is rethrown.
        /// </summary>
        /// <param name="action">
        ///     The <see cref="T:System.Action" /> to perform within a transaction. <paramref name="action" /> can contain any
        ///     number
        ///     of operations on the connection but should never call <see cref="M:SQLite.SQLiteConnection.Commit" /> or
        ///     <see cref="M:SQLite.SQLiteConnection.Commit" />.
        /// </param>
        public Task RunInTransactionAsync(Action<SQLiteConnection> action)
        {
            return _connection.RunInTransactionAsync(action);
        }

        /// <summary>
        ///     Returns a queryable interface to the table represented by the given type.
        /// </summary>
        /// <returns>
        ///     A queryable object that is able to translate Where, OrderBy, and Take
        ///     queries into native SQL.
        /// </returns>
        public AsyncTableQuery<T> Table<T>() where T : new()
        {
            return _connection.Table<T>();
        }

        /// <summary>
        ///     Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
        ///     in the command text for each of the arguments and then executes that command.
        ///     Use this method when return primitive values.
        ///     You can set the Trace or TimeExecution properties of the connection
        ///     to profile execution.
        /// </summary>
        /// <param name="query">The fully escaped SQL.</param>
        /// <param name="args">
        ///     Arguments to substitute for the occurences of '?' in the query.
        /// </param>
        /// <returns>
        ///     The number of rows modified in the database as a result of this execution.
        /// </returns>
        public Task<T> ExecuteScalarAsync<T>(string query, params object[] args)
        {
            return _connection.ExecuteScalarAsync<T>(query, args);
        }

        /// <summary>
        ///     Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
        ///     in the command text for each of the arguments and then executes that command.
        ///     It returns each row of the result using the mapping automatically generated for
        ///     the given type.
        /// </summary>
        /// <param name="query">The fully escaped SQL.</param>
        /// <param name="args">
        ///     Arguments to substitute for the occurences of '?' in the query.
        /// </param>
        /// <returns>
        ///     A list with one result for each row returned by the query.
        /// </returns>
        public Task<List<T>> QueryAsync<T>(string query, params object[] args) where T : new()
        {
            return _connection.QueryAsync<T>(query, args);
        }

        /// <summary>
        ///     Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
        ///     in the command text for each of the arguments and then executes that command.
        ///     It returns the first column of each row of the result.
        /// </summary>
        /// <param name="query">The fully escaped SQL.</param>
        /// <param name="args">
        ///     Arguments to substitute for the occurences of '?' in the query.
        /// </param>
        /// <returns>
        ///     A list with one result for the first column of each row returned by the query.
        /// </returns>
        public Task<List<T>> QueryScalarsAsync<T>(string query, params object[] args)
        {
            return _connection.QueryScalarsAsync<T>(query, args);
        }

        /// <summary>
        ///     Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
        ///     in the command text for each of the arguments and then executes that command.
        ///     It returns each row of the result using the specified mapping. This function is
        ///     only used by libraries in order to query the database via introspection. It is
        ///     normally not used.
        /// </summary>
        /// <param name="map">
        ///     A <see cref="T:SQLite.TableMapping" /> to use to convert the resulting rows
        ///     into objects.
        /// </param>
        /// <param name="query">The fully escaped SQL.</param>
        /// <param name="args">
        ///     Arguments to substitute for the occurences of '?' in the query.
        /// </param>
        /// <returns>
        ///     An enumerable with one result for each row returned by the query.
        /// </returns>
        public Task<List<object>> QueryAsync(TableMapping map, string query, params object[] args)
        {
            return _connection.QueryAsync(map, query, args);
        }

        /// <summary>
        ///     Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
        ///     in the command text for each of the arguments and then executes that command.
        ///     It returns each row of the result using the mapping automatically generated for
        ///     the given type.
        /// </summary>
        /// <param name="query">The fully escaped SQL.</param>
        /// <param name="args">
        ///     Arguments to substitute for the occurences of '?' in the query.
        /// </param>
        /// <returns>
        ///     An enumerable with one result for each row returned by the query.
        ///     The enumerator will call sqlite3_step on each call to MoveNext, so the database
        ///     connection must remain open for the lifetime of the enumerator.
        /// </returns>
        public Task<IEnumerable<T>> DeferredQueryAsync<T>(string query, params object[] args) where T : new()
        {
            return _connection.DeferredQueryAsync<T>(query, args);
        }

        /// <summary>
        ///     Creates a SQLiteCommand given the command text (SQL) with arguments. Place a '?'
        ///     in the command text for each of the arguments and then executes that command.
        ///     It returns each row of the result using the specified mapping. This function is
        ///     only used by libraries in order to query the database via introspection. It is
        ///     normally not used.
        /// </summary>
        /// <param name="map">
        ///     A <see cref="T:SQLite.TableMapping" /> to use to convert the resulting rows
        ///     into objects.
        /// </param>
        /// <param name="query">The fully escaped SQL.</param>
        /// <param name="args">
        ///     Arguments to substitute for the occurences of '?' in the query.
        /// </param>
        /// <returns>
        ///     An enumerable with one result for each row returned by the query.
        ///     The enumerator will call sqlite3_step on each call to MoveNext, so the database
        ///     connection must remain open for the lifetime of the enumerator.
        /// </returns>
        public Task<IEnumerable<object>> DeferredQueryAsync(
            TableMapping map,
            string query,
            params object[] args)
        {
            return _connection.DeferredQueryAsync(map, query, args);
        }

        /// <summary>
        ///     Change the encryption key for a SQLCipher database with "pragma rekey = ...".
        /// </summary>
        /// <param name="key">Encryption key plain text that is converted to the real encryption key using PBKDF2 key derivation</param>
        public Task ReKeyAsync(string key)
        {
            return _connection.ReKeyAsync(key);
        }

        /// <summary>Change the encryption key for a SQLCipher database.</summary>
        /// <param name="key">256-bit (32 byte) or 384-bit (48 bytes) encryption key data</param>
        public Task ReKeyAsync(byte[] key)
        {
            return _connection.ReKeyAsync(key);
        }

        private async Task InitializeAsync()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes()
                .Where(type => type.GetCustomAttributes<TableAttribute>().Any()).ToArray();

            await _connection.CreateTablesAsync(CreateFlags.None, types);
        }

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