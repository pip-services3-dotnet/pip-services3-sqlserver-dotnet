
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using PipServices3.Commons.Data;

namespace PipServices3.SqlServer.Persistence
{
    public class SqlServerDummyPersistence : IdentifiableSqlServerPersistence<Dummy, string>, IDummyPersistence
    {
        public SqlServerDummyPersistence()
            : base("dummies")
        {
            AutoCreateObject("CREATE TABLE [dummies] ([id] VARCHAR(32) PRIMARY KEY, [key] VARCHAR(50), [content] VARCHAR(MAX), create_time_utc DATETIME)"); 
            EnsureIndex("dummies_key", new Dictionary<string, bool> { { "key", true } }, new IndexOptions { Unique = true });
        }

        public async Task<DataPage<Dummy>> GetPageByFilterAsync(string correlationId, FilterParams filter, PagingParams paging)
        {
            filter ??= new FilterParams();
            var key = filter.GetAsNullableString("key");
            
            var filterCondition = "";
            if (key != null)
                filterCondition += "key='" + key + "'";

            return await base.GetPageByFilterAsync(correlationId, filterCondition, paging, null, null);
        }

        public async Task<long> GetCountByFilterAsync(string correlationId, FilterParams filter)
        {
            filter ??= new FilterParams();
            var key = filter.GetAsNullableString("key");

            var filterCondition = "";
            if (key != null)
                filterCondition += "key='" + key + "'";

            return await base.GetCountByFilterAsync(correlationId, filterCondition);
        }

    }
}