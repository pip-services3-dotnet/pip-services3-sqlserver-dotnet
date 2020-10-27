﻿using PipServices3.Commons.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PipServices3.SqlServer.Persistence
{
    public class JsonSqlServerDummyPersistence: IdentifiableJsonSqlServerPersistence<Dummy, string>, IDummyPersistence
    {
        public JsonSqlServerDummyPersistence()
            : base("dummies_json")
        {
            EnsureTable();
            AutoCreateObject("ALTER TABLE [dummies_json] ADD [data_key] AS JSON_VALUE([data],'$.key')");
            EnsureIndex("dummies_json_key", new Dictionary<string, bool> { { "data_key", true } }, new IndexOptions { Unique = true });
        }

        public async Task<DataPage<Dummy>> GetPageByFilterAsync(string correlationId, FilterParams filter, PagingParams paging)
        {
            filter ??= new FilterParams();
            var key = filter.GetAsNullableString("key");

            var filterCondition = "";
            if (key != null)
                filterCondition += "JSON_VALUE([data],'$.key')='" + key + "'";

            return await base.GetPageByFilterAsync(correlationId, filterCondition, paging, null, null);
        }

        public async Task<long> GetCountByFilterAsync(string correlationId, FilterParams filter)
        {
            filter ??= new FilterParams();
            var key = filter.GetAsNullableString("key");

            var filterCondition = "";
            if (key != null)
                filterCondition += "JSON_VALUE([data],'$.key')='" + key + "'";

            return await base.GetCountByFilterAsync(correlationId, filterCondition);
        }
    }
}