﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PipServices3.Commons.Config;
using PipServices3.Commons.Data;
using PipServices3.Commons.Errors;
using PipServices3.Commons.Refer;
using PipServices3.Commons.Run;
using PipServices3.Components.Log;
using System.Data;
using System.Text;
using System.Linq;
using PipServices3.Commons.Convert;
using PipServices3.Commons.Reflect;
using System.Reflection;
using System.Runtime.Serialization;
using System.Data.SqlClient;

namespace PipServices3.SqlServer.Persistence
{
    /// <summary>
    /// Abstract persistence component that stores data in SqlServer
    /// and is based using Mongoose object relational mapping.
    /// 
    /// This is the most basic persistence component that is only
    /// able to store data items of any type.Specific CRUD operations 
    /// over the data items must be implemented in child classes by 
    /// accessing <c>this._collection</c> or <c>this._model</c> properties.
    /// 
    /// ### Configuration parameters ###
    /// 
    /// - collection:                  (optional) SqlServer collection name
    /// 
    /// connection(s):
    /// - discovery_key:             (optional) a key to retrieve the connection from <a href="https://pip-services3-dotnet.github.io/pip-services3-components-dotnet/interface_pip_services_1_1_components_1_1_connect_1_1_i_discovery.html">IDiscovery</a>
    /// - host:                      host name or IP address
    /// - port:                      port number (default: 27017)
    /// - uri:                       resource URI or connection string with all parameters in it
    /// 
    /// credential(s):
    /// - store_key:                 (optional) a key to retrieve the credentials from <a href="https://pip-services3-dotnet.github.io/pip-services3-components-dotnet/interface_pip_services_1_1_components_1_1_auth_1_1_i_credential_store.html">ICredentialStore</a>
    /// - username:                  (optional) user name
    /// - password:                  (optional) user password
    /// 
    /// options:
    /// - max_pool_size:             (optional) maximum connection pool size (default: 2)
    /// - keep_alive:                (optional) enable connection keep alive (default: true)
    /// - connect_timeout:           (optional) connection timeout in milliseconds (default: 5 sec)
    /// - auto_reconnect:            (optional) enable auto reconnection (default: true)
    /// - max_page_size:             (optional) maximum page size (default: 100)
    /// - debug:                     (optional) enable debug output (default: false).
    /// 
    /// ### References ###
    /// 
    /// - *:logger:*:*:1.0           (optional) <a href="https://pip-services3-dotnet.github.io/pip-services3-components-dotnet/interface_pip_services_1_1_components_1_1_log_1_1_i_logger.html">ILogger</a> components to pass log messages
    /// - *:discovery:*:*:1.0        (optional) <a href="https://pip-services3-dotnet.github.io/pip-services3-components-dotnet/interface_pip_services_1_1_components_1_1_connect_1_1_i_discovery.html">IDiscovery</a> services
    /// - *:credential-store:*:*:1.0 (optional) Credential stores to resolve credentials
    /// </summary>
    /// <typeparam name="T">the class type</typeparam>
    /// <example>
    /// <code>
    /// class MySqlServerPersistence: SqlServerPersistence<MyData> 
    /// {
    ///     public MySqlServerPersistence()
    ///     {
    ///         base("mydata");
    ///     }
    ///     public MyData getByName(string correlationId, string name)
    ///     {
    ///         var builder = Builders<BeaconV1>.Filter;
    ///         var filter = builder.Eq(x => x.Name, name);
    ///         var result = await _collection.Find(filter).FirstOrDefaultAsync();
    ///         return result;
    ///     }
    ///     public MyData set(String correlatonId, MyData item)
    ///     {
    ///         var filter = Builders<T>.Filter.Eq(x => x.Id, item.Id);
    ///         var options = new FindOneAndReplaceOptions<T>
    ///         {
    ///             ReturnDocument = ReturnDocument.After,
    ///             IsUpsert = true
    ///         };
    ///         var result = await _collection.FindOneAndReplaceAsync(filter, item, options);
    ///         return result;
    ///     }
    /// }
    /// 
    /// var persistence = new MySqlServerPersistence();
    /// persistence.Configure(ConfigParams.fromTuples(
    /// "host", "localhost",
    /// "port", 27017 ));
    /// 
    /// persitence.Open("123");
    /// var mydata = new MyData("ABC");
    /// persistence.Set("123", mydata);
    /// persistence.GetByName("123", "ABC");
    /// Console.Out.WriteLine(item);                   // Result: { name: "ABC" }
    /// </code>
    /// </example>
    public class SqlServerPersistence<T> : IReferenceable, IUnreferenceable, IReconfigurable, IOpenable, ICleanable where T : new()
    {
        private static ConfigParams _defaultConfig = ConfigParams.FromTuples(
            "collection", null,
            "dependencies.connection", "*:connection:postgres:*:1.0",

            // connections.*
            // credential.*

            "options.max_pool_size", 2,
            "options.keep_alive", 1,
            "options.connect_timeout", 5,
            "options.auto_reconnect", true,
            "options.max_page_size", 100,
            "options.debug", true
        );

        private readonly List<string> _autoObjects = new List<string>();


        /// <summary>
        /// The SqlServer connection.
        /// </summary>
        protected SqlServerConnection _connection;

        /// <summary>
        /// The SqlServer connection component.
        /// </summary>
        protected SqlConnection _client;

        /// <summary>
        /// The SqlServer database name.
        /// </summary>
        protected string _databaseName;

        /// <summary>
        /// The SqlServer table name.
        /// </summary>
        protected string _tableName;

        /// <summary>
        /// Maximum page size
        /// </summary>
        protected int _maxPageSize = 100;

        /// <summary>
        /// The dependency resolver.
        /// </summary>
        protected DependencyResolver _dependencyResolver = new DependencyResolver(_defaultConfig);

        /// <summary>
        /// The logger.
        /// </summary>
        protected CompositeLogger _logger = new CompositeLogger();

        private ConfigParams _config;
        private IReferences _references;
        private bool _localConnection;
        private bool _opened;
        private Dictionary<string, string> _namesMap = new Dictionary<string, string>(); 

        /// <summary>
        /// Creates a new instance of the persistence component.
        /// </summary>
        /// <param name="tableName">(optional) a tableName name.</param>
        public SqlServerPersistence(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                throw new ArgumentNullException(nameof(tableName));

            _tableName = tableName;

            _namesMap = CreateNamesMap();
        }

        private Dictionary<string, string> CreateNamesMap()
        {
            var attrType = typeof(DataMemberAttribute);
            var result = new Dictionary<string, string>();

            foreach (PropertyInfo prop in typeof(T).GetProperties())
            {
                var memberName = (prop.GetCustomAttributes(attrType, true).FirstOrDefault() is DataMemberAttribute dataMemberAttr) 
                    ? dataMemberAttr.Name 
                    : prop.Name.ToLower();

                result.Add(prop.Name, memberName);
            }

            return result;
        }

        /// <summary>
        /// Configures component by passing configuration parameters.
        /// </summary>
        /// <param name="config">configuration parameters to be set.</param>
        public virtual void Configure(ConfigParams config)
        {
            _config = config.SetDefaults(_defaultConfig);
            _dependencyResolver.Configure(_config);

            _tableName = config.GetAsStringWithDefault("collection", _tableName);
            _tableName = config.GetAsStringWithDefault("table", _tableName);
            _maxPageSize = config.GetAsIntegerWithDefault("options.max_page_size", _maxPageSize);
        }

        /// <summary>
        /// Sets references to dependent components.
        /// </summary>
        /// <param name="references">references to locate the component dependencies.</param>
        public virtual void SetReferences(IReferences references)
        {
            _references = references;

            _logger.SetReferences(references);
            _dependencyResolver.SetReferences(references);

            // Get connection
            _connection = _dependencyResolver.GetOneOptional("connection") as SqlServerConnection;
            _localConnection = _connection == null;

            // Or create a local one
            if (_connection == null)
                _connection = CreateLocalConnection();
        }

        /// <summary>
        /// Unsets (clears) previously set references to dependent components.
        /// </summary>
        public virtual void UnsetReferences()
        {
            _connection = null;
        }

        private SqlServerConnection CreateLocalConnection()
        {
            var connection = new SqlServerConnection();

            if (_config != null)
                connection.Configure(_config);

            if (_references != null)
                connection.SetReferences(_references);

            return connection;
        }

        protected void EnsureIndex(string name, Dictionary<string, bool> keys, IndexOptions options)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("CREATE");

            if (options.Unique)
                builder.Append(" UNIQUE");

            builder.Append(" INDEX ")
                .Append(name)
                .Append(" ON ")
                .Append(QuoteIdentifier(_tableName));

            if (!string.IsNullOrWhiteSpace(options.Type))
                builder.Append(" ")
                    .Append(options.Type);

            var fields = string.Join(", ", keys.Select(x => QuoteIdentifier(x.Key) + (x.Value ? "" : " DESC")));

            builder.Append("(").Append(fields).Append(")");

            AutoCreateObject(builder.ToString());
        }

        /// <summary>
        /// Adds index definition to create it on opening
        /// </summary>
        /// <param name="dmlStatement">DML statement to autocreate database object</param>
        protected void AutoCreateObject(string dmlStatement)
        {
            _autoObjects.Add(dmlStatement);
        }

        /// <summary>
        /// Converts object value from internal to public format.
        /// </summary>
        /// <param name="map">an object in internal format to convert</param>
        /// <returns>converted object in public format</returns>
        protected virtual T ConvertToPublic(AnyValueMap map)
        {
            var newMap = ConvertDateTimeToUtc(map);
            newMap = ConvertNamesToPublic(newMap);

            var item = new T();
            ObjectWriter.SetProperties(item, newMap);
            
            return item;
        }

        protected virtual AnyValueMap ConvertFromPublic(T value)
        {
            var map = new AnyValueMap(MapConverter.ToMap(value));
            map = ConvertNamesFromPublic(map);
            
            return map;
        }

        protected string QuoteIdentifier(string value)
        {
            if (value == null || value == "") return value;

            if (value[0] == '[') return value;

            return '[' + value.Replace(".", "].[") + ']';
        }

        private AnyValueMap ConvertNamesToPublic(AnyValueMap map)
        {
            AnyValueMap newMap = new AnyValueMap();

            foreach (var key in map.Keys)
            {
                var name = _namesMap
                    .Where(x => x.Value == key)
                    .Select(x => x.Key)
                    .FirstOrDefault();

                newMap[name] = map[key];
            }

            return newMap;
        }

        private AnyValueMap ConvertNamesFromPublic(AnyValueMap map)
        {
            AnyValueMap newMap = new AnyValueMap();

            foreach (var key in map.Keys)
            {
                var name = _namesMap
                    .Where(x => x.Key == key)
                    .Select(x => x.Value)
                    .FirstOrDefault();

                newMap[name] = map[key];
            }

            return newMap;
        }

        private AnyValueMap ConvertDateTimeToUtc(AnyValueMap map)
        {
            AnyValueMap newMap = new AnyValueMap();
            foreach (var key in map.Keys)
            {
                if (map[key] is DateTime time)
                {
                    newMap[key] = DateTime.SpecifyKind(time, DateTimeKind.Utc);
                }
                else
                {
                    newMap[key] = map[key];
                }
            }
            return newMap;
        }

        /// <summary>
        /// Checks if the component is opened.
        /// </summary>
        /// <returns>true if the component has been opened and false otherwise.</returns>
        public virtual bool IsOpen()
        {
            return _opened;
        }

        /// <summary>
        /// Opens the component.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        public async virtual Task OpenAsync(string correlationId)
        {
            if (IsOpen()) return;

            if (_connection == null)
            {
                _connection = CreateLocalConnection();
                _localConnection = true;
            }

            if (_localConnection)
                await _connection.OpenAsync(correlationId);

            if (_connection.IsOpen() == false)
                throw new InvalidStateException(correlationId, "CONNECTION_NOT_OPENED", "Database connection is not opened");

            _client = _connection.GetConnection();
            _databaseName = _connection.GetDatabaseName();

            // Recreate objects
            await AutoCreateObjectsAsync(correlationId);

            _opened = true;
        }

        /// <summary>
        /// Closes component and frees used resources.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        public virtual async Task CloseAsync(string correlationId)
        {
            if (IsOpen())
            {
                if (_connection == null)
                    throw new InvalidStateException(correlationId, "NO_CONNECTION", "SqlServer connection is missing");

                _opened = false;

                if (_localConnection)
                    await _connection.CloseAsync(correlationId);
            }
        }

        /// <summary>
        /// Clears component state.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        public virtual async Task ClearAsync(string correlationId)
        {
            // Return error if collection is not set
            if (string.IsNullOrWhiteSpace(_tableName))
                throw new Exception("Table name is not defined");

            try
            {
                await ExecuteNonQuery("TRUNCATE TABLE " + QuoteIdentifier(_tableName));
            }
            catch (Exception ex)
            {
                throw new ConnectionException(correlationId, "CONNECT_FAILED", "Connection to sqlserver failed")
                    .WithCause(ex);
            }
        }

        protected async Task AutoCreateObjectsAsync(string correlationId)
        {
            if (_autoObjects == null || _autoObjects.Count == 0)
                return;

            // If table already exists then exit
            if (await TableExistAsync(_tableName))
                return;

            _logger.Debug(correlationId, "Table {0} does not exist. Creating database objects...", _tableName);

            // Run all DML commands
            try
            {
                foreach (var dml in _autoObjects)
                {
                    await ExecuteNonQuery(dml);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(correlationId, ex, "Failed to autocreate database object");
                throw;
            }
        }

        /// <summary>
        /// Generates a list of column names to use in SQL statements like: "column1,column2,column3"
        /// </summary>
        /// <param name="map">key-value map</param>
        /// <returns>a generated list of column names</returns>
        protected string GenerateColumns(AnyValueMap map)
        {
            return GenerateColumns(map.Keys.ToList());
        }

        /// <summary>
        /// Generates a list of column names to use in SQL statements like: "column1,column2,column3"
        /// </summary>
        /// <param name="values">an array with column values</param>
        /// <returns>a generated list of column names</returns>
        protected string GenerateColumns(IEnumerable<string> values)
        {
            return string.Join(",", values.Select(x => QuoteIdentifier(x)));
        }

        /// <summary>
        /// Generates a list of value parameters to use in SQL statements like: "@Param1,@Param2,@Param3"
        /// </summary>
        /// <param name="map">key-value map</param>
        /// <returns>a generated list of value parameters</returns>
        protected string GenerateParameters(AnyValueMap map)
        {
            return GenerateParameters(map.Keys.ToList());
        }

        /// <summary>
        /// Generates a list of value parameters to use in SQL statements like: "@Param1,@Param2,@Param3"
        /// </summary>
        /// <param name="values">an array with column values</param>
        /// <returns>a generated list of value parameters</returns>
        protected string GenerateParameters<K>(IEnumerable<K> values)
        {
            List<string> result = new List<string>();

            var index = 1;
            foreach (var value in values)
            {
                result.Add("@Param" + index);
                index++;
            }

            return string.Join(",", result);
        }

        /// <summary>
        /// Generates a list of column sets to use in UPDATE statements like: column1=@Param1,column2=@Param2
        /// </summary>
        /// <param name="map">a key-value map with columns and values</param>
        /// <returns></returns>
        protected string GenerateSetParameters(AnyValueMap map)
        {
            return GenerateSetParameters(map.Keys.ToList());
        }

        /// <summary>
        /// Generates a list of column sets to use in UPDATE statements like: column1=@Param1,column2=@Param2
        /// </summary>
        /// <param name="values">an array with column names</param>
        /// <returns></returns>
        protected string GenerateSetParameters(IEnumerable<string> values)
        {
            var result = "";
            var index = 1;
            foreach (var column in values)
            {
                if (result != "") result += ",";
                result += QuoteIdentifier(column) + "=@Param" + index;
                index++;
            }

            return result;
        }

        /// <summary>
        /// Generates a list of column parameters
        /// </summary>
        /// <param name="map">a key-value map with columns and values</param>
        /// <returns>generated list of column values</returns>
        protected List<object> GenerateValues(AnyValueMap map)
        {
            return map.Values.ToList();
        }

        /// <summary>
        /// Gets a page of data items retrieved by a given filter and sorted according to sort parameters.
        /// 
        /// This method shall be called by a public getPageByFilter method from child
        /// class that receives FilterParams and converts them into a filter function.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        /// <param name="filterDefinition">(optional) a filter JSON object</param>
        /// <param name="paging">(optional) paging parameters</param>
        /// <param name="sortDefinition">(optional) sorting JSON object</param>
        /// <returns>data page of results by filter.</returns>
        public virtual async Task<DataPage<T>> GetPageByFilterAsync(string correlationId, string filter,
                PagingParams paging = null, string sort = null, string select = null)
        {
            select = string.IsNullOrWhiteSpace(select) ? "*" : select;
            var query = string.Format("SELECT {0} FROM {1}", select, QuoteIdentifier(_tableName));

            // Adjust max item count based on configuration
            paging = paging ?? new PagingParams();
            var skip = paging.GetSkip(-1);
            var take = paging.GetTake(_maxPageSize);
            var pagingEnabled = paging.Total;

            if (!string.IsNullOrWhiteSpace(filter))
                query += " WHERE " + filter;

            if (!string.IsNullOrWhiteSpace(filter))
                query += " ORDER BY " + sort;
            else
                query += " ORDER BY 1";

            if (skip < 0) skip = 0;
            query += " OFFSET " + skip + " ROWS FETCH NEXT " + take + " ROWS ONLY";

            var result = await ExecuteReaderAsync(query);

            var items = result.Select(map => ConvertToPublic(map)).ToList();

            long? total = pagingEnabled ? (long?)await GetCountByFilterAsync(correlationId, filter) : null;

            return new DataPage<T>
            {
                Data = items,
                Total = total
            };
        }

        /// <summary>
        /// Gets a number of data items retrieved by a given filter.
        /// 
        /// This method shall be called by a public getCountByFilter method from child class that
        /// receives FilterParams and converts them into a filter function.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        /// <param name="filter">(optional) a filter JSON object</param>
        /// <returns></returns>
        protected virtual async Task<long> GetCountByFilterAsync(string correlationId, string filter)
        {
            var query = "SELECT COUNT(*) AS count FROM " + QuoteIdentifier(_tableName);

            if (!string.IsNullOrWhiteSpace(filter))
                query += " WHERE " + filter;

            var count = await ExecuteScalarAsync<long>(query);

            _logger.Trace(correlationId, "Counted {0} items in {1}", count, _tableName);

            return count;
        }

        /// <summary>
        /// Gets a list of data items retrieved by a given filter and sorted according to sort parameters.
        /// 
        /// This method shall be called by a public getListByFilter method from child class that
        /// receives FilterParams and converts them into a filter function.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        /// <param name="filter">(optional) a filter JSON object</param>
        /// <param name="sort">(optional) sorting JSON object</param>
        /// <param name="select">(optional) projection JSON object</param>
        /// <returns>data list</returns>
        protected async Task<List<T>> GetListByFilterAsync(string correlationId, string filter,
            string sort = null, string select = null)
        {
            select = string.IsNullOrWhiteSpace(select) ? "*" : select;
            var query = string.Format("SELECT {0} FROM {1}", select, QuoteIdentifier(_tableName));

            if (!string.IsNullOrWhiteSpace(filter))
                query += " WHERE " + filter;

            if (!string.IsNullOrWhiteSpace(filter))
                query += " ORDER BY " + sort;

            var result = await ExecuteReaderAsync(query);

            var items = result.Select(map => ConvertToPublic(map)).ToList();

            _logger.Trace(correlationId, $"Retrieved {items.Count} from {_tableName}");

            return items;
        }

        /// <summary>
        /// Gets a random item from items that match to a given filter.
        /// 
        /// This method shall be called by a public getOneRandom method from child class
        /// that receives FilterParams and converts them into a filter function.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        /// <param name="filterDefinition">(optional) a filter JSON object</param>
        /// <returns>a random item by filter.</returns>
        protected virtual async Task<T> GetOneRandomAsync(string correlationId, string filter)
        {
            var count = await GetCountByFilterAsync(correlationId, filter);

            if (count <= 0)
            {
                _logger.Trace(correlationId, "Nothing found for filter {0}", filter);
                return default;
            }

            var pos = new Random().Next(0, Convert.ToInt32(count) - 1);

            var query = "SELECT * FROM " + QuoteIdentifier(_tableName);

            if (!string.IsNullOrWhiteSpace(filter))
                query += " WHERE " + filter;

            query += string.Format(" OFFSET {0} LIMIT 1", pos);

            var items = await ExecuteReaderAsync(query);

            var item = items.FirstOrDefault();

            if (item == null)
                _logger.Trace(correlationId, "Random item wasn't found from {0}", _tableName);
            else
                _logger.Trace(correlationId, "Retrieved random item from {0}", _tableName);

            return ConvertToPublic(item);
        }

        /// <summary>
        /// Creates a data item.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        /// <param name="item">an item to be created.</param>
        /// <returns>created item.</returns>
        public virtual async Task<T> CreateAsync(string correlationId, T item)
        {
            if (item == null)
            {
                return default;
            }

            var map = ConvertFromPublic(item);
            var columns = GenerateColumns(map);
            var @params = GenerateParameters(map);

            var query = "INSERT INTO " + QuoteIdentifier(_tableName) + " (" + columns + ") OUTPUT INSERTED.* VALUES (" + @params + ")";

            var result = await ExecuteReaderAsync(query, map);

            var newItem = result != null && result.Count == 1
                ? ConvertToPublic(result[0]) : default;

            _logger.Trace(correlationId, "Created in {0} item {1}", _tableName, newItem);

            return newItem;
        }

        /// <summary>
        /// Deletes data items that match to a given filter.
        /// 
        /// This method shall be called by a public deleteByFilter method from child
        /// class that receives FilterParams and converts them into a filter function.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        /// <param name="filterDefinition">(optional) a filter JSON object.</param>
        public virtual async Task DeleteByFilterAsync(string correlationId, string filter)
        {
            var query = "DELETE FROM " + QuoteIdentifier(_tableName);

            if (!string.IsNullOrWhiteSpace(filter))
                query += " WHERE " + filter;

            var deletedCount = await ExecuteNonQuery(query);

            _logger.Trace(correlationId, $"Deleted {deletedCount} from {_tableName}");
        }

        private async Task<bool> TableExistAsync(string tableName)
        {
            var result = await ExecuteScalarAsync<bool>("SELECT CASE WHEN OBJECT_ID('" + tableName + "', 'U') IS NOT NULL THEN 1 ELSE 0 END");
            return result;
        }

        protected virtual void SetParameters(SqlCommand cmd, IEnumerable<object> values)
        {
            if (values != null && values.Count() > 0)
            {
                int index = 1;
                foreach (var value in values)
                {
                    AddParameter(cmd, "Param" + index, value);
                    index++;
                }
            }
        }

        protected virtual void AddParameter(SqlCommand cmd, string name, object value)
        {
            cmd.Parameters.AddWithValue(name, value);
        }

        protected async Task<int> ExecuteNonQuery(string cmdText, AnyValueMap map)
        {
            return await ExecuteNonQuery(cmdText, map.Values);
        }
        
        protected async Task<int> ExecuteNonQuery(string cmdText, IEnumerable<object> values = null)
        {
            using (var cmd = new SqlCommand(cmdText, _client))
            {
                SetParameters(cmd, values);
                return await cmd.ExecuteNonQueryAsync();
            }
        }

        protected async Task<List<AnyValueMap>> ExecuteReaderAsync(string cmdText, AnyValueMap map)
        {
            return await ExecuteReaderAsync(cmdText, map.Values);
        }

        protected async Task<List<AnyValueMap>> ExecuteReaderAsync(string cmdText, IEnumerable<object> values = null)
        {
            using (var cmd = new SqlCommand(cmdText, _client))
            {
                SetParameters(cmd, values);

                return await ExecuteReaderAsync(cmd);
            }
        }

        protected static async Task<List<AnyValueMap>> ExecuteReaderAsync(SqlCommand cmd)
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                DataTable table = new DataTable();
                table.Load(reader);

                List<AnyValueMap> result = new List<AnyValueMap>();
                foreach (DataRow row in table.Rows)
                {
                    AnyValueMap map = new AnyValueMap();
                    foreach (DataColumn column in table.Columns)
                    {
                        var value = row[column];
                        if (row[column] != DBNull.Value)
                        {
                            map[column.ColumnName] = value;
                        }
                    }

                    result.Add(map);
                }

                return result;
            }
        }

        private async Task<R> ExecuteScalarAsync<R>(string cmdText)
        {
            using (var cmd = new SqlCommand(cmdText, _client))
            {
                var result = await cmd.ExecuteScalarAsync();
                return (R)Convert.ChangeType(result, typeof(R));
            }
        }
    }
}
