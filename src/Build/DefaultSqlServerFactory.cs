﻿using PipServices3.Commons.Refer;
using PipServices3.Components.Build;
using PipServices3.SqlServer.Persistence;

namespace PipServices3.SqlServer.Build
{
    /// <summary>
    /// Creates SqlServer components by their descriptors.
    /// </summary>
    /// See <a href="https://rawgit.com/pip-services3-dotnet/pip-services3-components-dotnet/master/doc/api/class_pip_services_1_1_components_1_1_build_1_1_factory.html">Factory</a>, 
    /// <a href="https://rawgit.com/pip-services3-dotnet/pip-services3-postgres-dotnet/master/doc/api/class_pip_services_1_1_postgres_1_1_persistence_1_1_postgres_db_connection.html">SqlServerConnection</a>
    public class DefaultSqlServerFactory : Factory
    {
        public static Descriptor Descriptor = new Descriptor("pip-services", "factory", "sqlserver", "default", "1.0");
        public static Descriptor Descriptor3 = new Descriptor("pip-services3", "factory", "sqlserver", "default", "1.0");
        public static Descriptor SqlServerConnection3Descriptor = new Descriptor("pip-services3", "connection", "sqlserver", "*", "1.0");
        public static Descriptor SqlServerConnectionDescriptor = new Descriptor("pip-services", "connection", "sqlserver", "*", "1.0");

        /// <summary>
        /// Create a new instance of the factory.
        /// </summary>
        public DefaultSqlServerFactory()
        {
            RegisterAsType(SqlServerConnection3Descriptor, typeof(SqlServerConnection));
            RegisterAsType(SqlServerConnectionDescriptor, typeof(SqlServerConnection));
        }
    }
}
