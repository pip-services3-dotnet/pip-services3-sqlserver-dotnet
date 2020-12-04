using PipServices3.Commons.Config;
using PipServices3.SqlServer.Fixtures;
using System;
using System.Threading.Tasks;
using Xunit;

namespace PipServices3.SqlServer.Persistence
{
	/// <summary>
	/// Unit tests for the <c>SqlServerPersistenceTest</c> class
	/// </summary>
	[Collection("Sequential")]
	public class SqlServerPersistenceTest : IDisposable
	{
		private SqlServerDummyPersistence persistence;
		private DummyPersistenceFixture fixture;

		private string sqlserverUri;
		private string sqlserverHost;
		private string sqlserverPort;
		private string sqlserverDatabase;
		private string sqlserverUsername;
		private string sqlserverPassword;

		public SqlServerPersistenceTest()
		{
			sqlserverUri = Environment.GetEnvironmentVariable("SQLSERVER_URI");
			sqlserverHost = Environment.GetEnvironmentVariable("SQLSERVER_SERVICE_HOST") ?? "localhost";
			sqlserverPort = Environment.GetEnvironmentVariable("SQLSERVER_SERVICE_PORT") ?? "1433";
			sqlserverDatabase = Environment.GetEnvironmentVariable("SQLSERVER_DB") ?? "master";
			sqlserverUsername = Environment.GetEnvironmentVariable("SQLSERVER_USER") ?? "sa";
			sqlserverPassword = Environment.GetEnvironmentVariable("SQLSERVER_PASS") ?? "sqlserver_123";

			if (sqlserverUri == null && sqlserverHost == null)
				return;

			var dbConfig = ConfigParams.FromTuples(
				"connection.uri", sqlserverUri,
				"connection.host", sqlserverHost,
				"connection.port", sqlserverPort,
				"connection.database", sqlserverDatabase,
				"credential.username", sqlserverUsername,
				"credential.password", sqlserverPassword
			);

			persistence = new SqlServerDummyPersistence();
			persistence.Configure(dbConfig);

			fixture = new DummyPersistenceFixture(persistence);

			persistence.OpenAsync(null).Wait();
			persistence.ClearAsync(null).Wait();
		}

		public void Dispose()
		{
			persistence.CloseAsync(null).Wait();
		}

		[Fact]
		public async Task TestCrudOperations()
		{
			await fixture.TestCrudOperationsAsync();
		}

		[Fact]
		public async Task TestBatchOperations()
		{
			await fixture.TestBatchOperationsAsync();
		}
	}
}
