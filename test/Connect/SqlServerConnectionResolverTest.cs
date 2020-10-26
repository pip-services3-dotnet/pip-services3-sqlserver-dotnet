using PipServices3.Commons.Config;
using System.Threading.Tasks;
using Xunit;

namespace PipServices3.SqlServer.Connect
{
    public class SqlServerConnectionResolverTest
    {
        [Fact]
        public async Task TestConnectionConfig()
        {
            var dbConfig = ConfigParams.FromTuples(
                "connection.host", "localhost",
                "connection.port", 1433,
                "connection.database", "test",
                "connection.ssl", true,
                "credential.username", "sa",
                "credential.password", "pwd#123"
            );

            var resolver = new SqlServerSqlServerResolver();
            resolver.Configure(dbConfig);

            var connectionString = await resolver.ResolveAsync(null);
            Assert.Contains("Data Source='localhost'", connectionString);
            Assert.DoesNotContain("Port", connectionString);
            Assert.Contains("Initial Catalog='test'", connectionString);
            Assert.Contains("User Id='sa'", connectionString);
            Assert.Contains("Password='pwd#123'", connectionString);
            Assert.DoesNotContain("SSL Mode=", connectionString);
        }
    }
}
