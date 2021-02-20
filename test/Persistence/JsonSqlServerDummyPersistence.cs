using PipServices3.Commons.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PipServices3.SqlServer.Persistence
{
    public class JsonSqlServerDummyPersistence: IdentifiableJsonSqlServerPersistence<Dummy, string>, IDummyPersistence
    {
        public JsonSqlServerDummyPersistence()
            : base("dummies_json")
        {
        }


        protected override void DefineSchema()
        {
            ClearSchema();
            EnsureTable();
            EnsureSchema("ALTER TABLE [dummies_json] ADD [data_key] AS JSON_VALUE([data],'$.key')");
            EnsureIndex($"{_tableName}_key", new Dictionary<string, bool> { { "data_key", true } }, new IndexOptions { Unique = true });
        }

        public async Task<DataPage<Dummy>> GetPageByFilterAsync(string correlationId, FilterParams filter, PagingParams paging)
        {
            return await base.GetPageByFilterAsync(correlationId, ComposeFilter(filter), paging, null, null);
        }

        public async Task<long> GetCountByFilterAsync(string correlationId, FilterParams filter)
        {
            return await base.GetCountByFilterAsync(correlationId, ComposeFilter(filter));
        }

        private string ComposeFilter(FilterParams filter)
        {
            filter ??= new FilterParams();
            var key = filter.GetAsNullableString("key");

            var filterCondition = "";
            if (key != null)
                filterCondition += "JSON_VALUE([data],'$.key')='" + key + "'";
            
            return filterCondition;
        }
    }
}
