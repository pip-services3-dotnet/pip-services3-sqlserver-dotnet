using PipServices3.Commons.Convert;
using PipServices3.Commons.Data;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace PipServices3.SqlServer.Persistence
{
    public class IdentifiableJsonSqlServerPersistence<T, K>: IdentifiableSqlServerPersistence<T, K>
        where T : IIdentifiable<K>, new()
        where K : class
    {
        public IdentifiableJsonSqlServerPersistence(string tableName)
            : base(tableName)
        { }

        /// <summary>
        /// Adds DML statement to automatically create JSON(B) table
        /// </summary>
        /// <param name="idType">type of the id column (default: VARCHAR(32))</param>
        /// <param name="dataType">type of the data column (default: NVARCHAR(MAX))</param>
        protected void EnsureTable(string idType = "VARCHAR(32)", string dataType = "NVARCHAR(MAX)")
        { 
            var query = "CREATE TABLE " + QuoteIdentifier(_tableName)
            + " ([id] " + idType + " PRIMARY KEY, [data] " + dataType + ")";

            AutoCreateObject(query);
        }

        /// <summary>
        /// Converts object value from internal to public format.
        /// </summary>
        /// <param name="value">an object in internal format to convert.</param>
        /// <returns>converted object in public format.</returns>
        protected override T ConvertToPublic(AnyValueMap map)
        {
            if (map != null && map.TryGetValue("data", out object value) && value != null)
            {
                return JsonConverter.FromJson<T>(value.ToString());
            }

            return default;
        }

        /// <summary>
        /// Convert object value from public to internal format.
        /// </summary>
        /// <param name="value">an object in public format to convert.</param>
        /// <returns>converted object in internal format.</returns>
        protected override AnyValueMap ConvertFromPublic(T value)
        {
            if (value == null) return null;
            return AnyValueMap.FromTuples("id", value.Id, "data", JsonConverter.ToJson(value));
        }

        protected override void AddParameter(SqlCommand cmd, string name, object value)
        {
            if (value is T || value is Dictionary<string, object>)
            {
                cmd.Parameters.AddWithValue(name, JsonConverter.ToJson(value));
                return;
            }

            base.AddParameter(cmd, name, value);
        }

        ///// <summary>
        ///// Updates only few selected fields in a data item.
        ///// </summary>
        ///// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        ///// <param name="id">an id of data item to be updated.</param>
        ///// <param name="data">a map with fields to be updated.</param>
        ///// <returns>updated item</returns>
        public override async Task<T> UpdatePartially(string correlationId, K id, AnyValueMap data)
        {
            if (data == null || id == null)
                return default;

            var map = data;
            var columns = map.Keys.ToList();
            var values = map.Values.ToList();

            var set = "[data]";
            for (int index = 1; index <= columns.Count; index++)
            {
                var column = columns[index - 1];
                set = "JSON_MODIFY(" + set + ",'$." + column + "',@Param" + index + ")";
            }

            values.Add(id);

            var query = "UPDATE " + QuoteIdentifier(_tableName) + " SET [data]=" + set + " OUTPUT INSERTED.* WHERE [id]=@Param" + values.Count;

            var result = (await ExecuteReaderAsync(query, values)).FirstOrDefault();

            _logger.Trace(correlationId, "Updated partially in {0} with id = {1}", _tableName, id);

            var newItem = ConvertToPublic(result);
            return newItem;
        }
    }
}
