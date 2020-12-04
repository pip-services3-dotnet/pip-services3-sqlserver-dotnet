using PipServices3.Commons.Config;
using System;
using Xunit;

namespace PipServices3.SqlServer.Persistence
{
	/// <summary>
	/// Unit tests for the <c>SqlServerConnectionTest</c> class
	/// </summary>
	[Collection("Sequential")]
	public class SqlServerConnectionTest : IDisposable
	{
		private SqlServerConnection Db { get; }

		private string sqlserverUri;
		private string sqlserverHost;
		private string sqlserverPort;
		private string sqlserverDatabase;
		private string sqlserverUsername;
		private string sqlserverPassword;

		public SqlServerConnectionTest()
		{
			Db = new SqlServerConnection();

			sqlserverUri = Environment.GetEnvironmentVariable("SQLSERVER_URI");
			sqlserverHost = Environment.GetEnvironmentVariable("SQLSERVER_SERVICE_HOST") ?? "localhost";
			sqlserverPort = Environment.GetEnvironmentVariable("SQLSERVER_SERVICE_PORT") ?? "1433";
			sqlserverDatabase = Environment.GetEnvironmentVariable("SQLSERVER_DB") ?? "master";
			sqlserverUsername = Environment.GetEnvironmentVariable("SQLSERVER_USER") ?? "sa";
			sqlserverPassword = Environment.GetEnvironmentVariable("SQLSERVER_PASS") ?? "sqlserver_123";
			if (sqlserverUri == null && sqlserverHost == null)
				return;

			if (Db == null) return;
		}

		public void Dispose()
		{
			Db.CloseAsync(null).Wait();
		}

		[Fact]
		public void TestOpenAsync_Success()
		{
			Db.Configure(ConfigParams.FromTuples(
				"connection.uri", sqlserverUri,
				"connection.host", sqlserverHost,
				"connection.port", sqlserverPort,
				"connection.database", sqlserverDatabase,
				"credential.username", sqlserverUsername,
				"credential.password", sqlserverPassword
			));

			Db.OpenAsync(null).Wait();

			var actual = Db.IsOpen();

			Assert.True(actual);
		}

		//[Fact]
		//public void TestOpenAsync_Failure()
		//{
		//    Db.CloseAsync(null).Wait();

		//    Db.Configure(ConfigParams.FromTuples(
		//        "connection.uri", sqlserverUri,
		//        "connection.host", sqlserverHost,
		//        "connection.port", "1234",
		//        "connection.database", sqlserverDatabase
		//    ));

		//    var ex = Assert.Throws<AggregateException>(() => Db.OpenAsync(null).Wait());
		//    Assert.Equal("Connection to sqlserver failed", ex.InnerException.Message);
		//}
	}
}
