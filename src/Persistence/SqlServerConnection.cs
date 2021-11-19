using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PipServices3.Commons.Config;
using PipServices3.Commons.Errors;
using PipServices3.Commons.Refer;
using PipServices3.Commons.Run;
using PipServices3.Components.Log;
using PipServices3.SqlServer.Connect;
using System.Linq;
using System.Data.SqlClient;
using Renci.SshNet;

namespace PipServices3.SqlServer.Persistence
{
    /// <summary>
    /// SqlServer connection using plain driver.
    /// 
    /// By defining a connection and sharing it through multiple persistence components
    /// you can reduce number of used database connections.
    /// 
    /// ### Configuration parameters ###
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
    public class SqlServerConnection : IReferenceable, IReconfigurable, IOpenable
    {
        private ConfigParams _config;
        
        private ConfigParams _defaultConfig = ConfigParams.FromTuples(
            "options.connect_timeout", 15,
            "options.connect_retry_count", 1,
            "options.connect_retry_interval", 10,
            "options.max_pool_size", 3
        );

        /// <summary>
        /// The connection resolver.
        /// </summary>
        protected SqlServerSqlServerResolver _connectionResolver = new SqlServerSqlServerResolver();

        /// <summary>
        /// The configuration options.
        /// </summary>
        protected ConfigParams _options = new ConfigParams();

        /// <summary>
        /// The SSH configuration object.
        /// </summary>
        private ConfigParams _sshConfigs = new ConfigParams();

        /// <summary>
        /// The SqlServer connection object.
        /// </summary>
        protected SqlConnection _connection;

        /// <summary>
        /// The database name.
        /// </summary>
        protected string _databaseName;

        /// <summary>
        /// The database name.
        /// </summary>
        private string _databaseServer;
        
        /// <summary>
        /// The flag enabled ssh.
        /// </summary>
        private bool _sshEnabled;
        
        private SshClient _sshClient;

        private string _sshPort;

        /// <summary>
        /// The logger.
        /// </summary>
        protected CompositeLogger _logger = new CompositeLogger();

        /// <summary>
        /// Creates a new instance of the connection component.
        /// </summary>
        public SqlServerConnection()
        { }

        /// <summary>
        /// Gets SqlServer connection object.
        /// </summary>
        /// <returns>The SqlServer connection object.</returns>
        public SqlConnection GetConnection()
        {
            return _connection;
        }

        /// <summary>
        /// Gets the name of the connected database.
        /// </summary>
        /// <returns>The name of the connected database.</returns>
        public string GetDatabaseName()
        {
            return _databaseName;
        }

        /// <summary>
        /// Sets references to dependent components.
        /// </summary>
        /// <param name="references">references to locate the component dependencies.</param>
        public void SetReferences(IReferences references)
        {
            _logger.SetReferences(references);
            _connectionResolver.SetReferences(references);
        }

        /// <summary>
        /// Configures component by passing configuration parameters.
        /// </summary>
        /// <param name="config">configuration parameters to be set.</param>
        public virtual void Configure(ConfigParams config)
        {
            _config = config.SetDefaults(_defaultConfig);
            _options = _options.Override(_config.GetSection("options"));

            _databaseServer = _config.GetAsNullableString("connection.host");
            _sshConfigs = _sshConfigs.Override(_config.GetSection("ssh"));
            _sshEnabled = _sshConfigs.GetAsBooleanWithDefault("enabled", false);

            _connectionResolver.Configure(_config);
        }

        /// <summary>
        /// Checks if the component is opened.
        /// </summary>
        /// <returns>true if the component has been opened and false otherwise.</returns>
        public virtual bool IsOpen()
        {
            return _connection != null;
        }

        /// <summary>
        /// Opens the component.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        public virtual async Task OpenAsync(string correlationId)
        {
            if (_sshEnabled)
            {
                await MsSqlWithSshOpenAsync(correlationId);
            }
            else
            {
                var connectionString = await _connectionResolver.ResolveAsync(correlationId);

                _logger.Trace(correlationId, "Connecting to sqlserver...");

                try
                {
                    var settings = ComposeSettings();
                    var connString = connectionString.TrimEnd(';') + ";" + JoinParams(settings);

                    _connection = new SqlConnection(connString);
                    _databaseName = _connection.Database;

                    // Try to connect
                    await _connection.OpenAsync();

                    _logger.Debug(correlationId, "Connected to sqlserver database {0}", _databaseName);
                }
                catch (Exception ex)
                {
                    throw new ConnectionException(correlationId, "CONNECT_FAILED", "Connection to sqlserver failed", ex);
                }
            }
        }
        
        private async Task MsSqlWithSshOpenAsync(string correlationId)
        {
            var sshHost = _sshConfigs.GetAsNullableString("host");
            var sshUsername = _sshConfigs.GetAsNullableString("username");
            var sshPassword = _sshConfigs.GetAsNullableString("password");
            var sshKeyFile = _sshConfigs.GetAsNullableString("key_file_path");
            var sshKeepAliveInterval = _sshConfigs.GetAsNullableTimeSpan("keep_alive_interval");

            var (sshClient, localPort) = ConnectSsh(sshHost, sshUsername, sshPassword, sshKeyFile, 
                databaseServer: _databaseServer, sshKeepAliveInterval: sshKeepAliveInterval);
            
            _sshClient = sshClient;
            _sshPort = localPort.ToString();
            
            _config.Set("connection.host", "127.0.0.1");
            _config.Set("connection.port", _sshPort);
            _connectionResolver.Configure(_config);

            var connectionString = await _connectionResolver.ResolveAsync(correlationId);

            _logger.Trace(correlationId, "Connecting to sqlserver...");

            try
            {
                var settings = ComposeSettings();
                var connString = connectionString.TrimEnd(';') + ";" + JoinParams(settings);

                _connection = new SqlConnection(connString);
                _databaseName = _connection.Database;

                // Try to connect
                await _connection.OpenAsync();

                _logger.Debug(correlationId, "Connected to sqlserver database {0}", _databaseName);
            }
            catch (Exception ex)
            {
                _logger.Error(correlationId, ex.ToString());
                throw new ConnectionException(correlationId, "CONNECT_FAILED", "Connection to sqlserver failed", ex);
            }
        }

        private ConfigParams ComposeSettings()
        {
            var maxPoolSize = _options.GetAsNullableInteger("max_pool_size");
            var connectTimeout = _options.GetAsNullableInteger("connect_timeout");
            var connectRetryCount = _options.GetAsNullableInteger("connect_retry_count");
            var connectRetryInterval = _options.GetAsNullableInteger("connect_retry_interval");

            ConfigParams settings = new ConfigParams();

            if (maxPoolSize.HasValue) settings["Max Pool Size"] = maxPoolSize.Value.ToString();
            if (connectTimeout.HasValue) settings["Connection Timeout"] = connectTimeout.Value.ToString();
            if (connectRetryCount.HasValue) settings["ConnectRetryCount"] = connectRetryCount.Value.ToString();
            if (connectRetryInterval.HasValue) settings["ConnectRetryInterval"] = connectRetryInterval.Value.ToString();

            return settings;
        }

        private static string JoinParams(ConfigParams config)
        { 
            return string.Join(";", config.Select(x => string.Format("{0}={1}", x.Key, x.Value))); 
        }
        
        /// <summary>
        /// Closes component and frees used resources.
        /// </summary>
        /// <param name="correlationId">(optional) transaction id to trace execution through call chain.</param>
        public virtual async Task CloseAsync(string correlationId)
        {
            if (_sshEnabled)
            {
                if (_sshClient.IsConnected)
                {
                    _sshClient.Disconnect();
                }
            }
            
            // Todo: Properly close the connection
            _connection.Close();

            _connection = null;
            _databaseName = null;

            await Task.Delay(0);
        }
        
        private static (SshClient SshClient, uint Port) ConnectSsh(string sshHostName, string sshUserName, string sshPassword = null,
            string sshKeyFile = null, string sshPassPhrase = null, int sshPort = 22, string databaseServer = "localhost", int databasePort = 1433,
            TimeSpan? sshKeepAliveInterval = null)
        {
            // check arguments
            if (string.IsNullOrEmpty(sshHostName))
                throw new ArgumentException($"{nameof(sshHostName)} must be specified.", nameof(sshHostName));
            if (string.IsNullOrEmpty(sshUserName))
                throw new ArgumentException($"{nameof(sshUserName)} must be specified.", nameof(sshUserName));
            if (string.IsNullOrEmpty(sshPassword) && string.IsNullOrEmpty(sshKeyFile))
                throw new ArgumentException($"One of {nameof(sshPassword)} and {nameof(sshKeyFile)} must be specified.");
            if (string.IsNullOrEmpty(databaseServer))
                throw new ArgumentException($"{nameof(databaseServer)} must be specified.", nameof(databaseServer));

            // define the authentication methods to use (in order)
            var authenticationMethods = new List<AuthenticationMethod>();
            if (!string.IsNullOrEmpty(sshKeyFile))
            {
                authenticationMethods.Add(new PrivateKeyAuthenticationMethod(sshUserName,
                    new PrivateKeyFile(sshKeyFile, string.IsNullOrEmpty(sshPassPhrase) ? null : sshPassPhrase)));
            }
            if (!string.IsNullOrEmpty(sshPassword))
            {
                authenticationMethods.Add(new PasswordAuthenticationMethod(sshUserName, sshPassword));
            }

            // connect to the SSH server
            var sshClient = new SshClient(new ConnectionInfo(sshHostName, sshPort, sshUserName, authenticationMethods.ToArray())
            {
                Timeout = TimeSpan.FromSeconds(30),
            });

            if (sshKeepAliveInterval.HasValue)
                sshClient.KeepAliveInterval = sshKeepAliveInterval.Value;

            sshClient.Connect();

            // forward a local port to the database server and port, using the SSH server
            var forwardedPort = new ForwardedPortLocal("127.0.0.1", databaseServer, (uint) databasePort);
            sshClient.AddForwardedPort(forwardedPort);
            forwardedPort.Start();

            return (sshClient, forwardedPort.BoundPort);
        }
    }
}
