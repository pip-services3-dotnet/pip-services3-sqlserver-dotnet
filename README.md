# <img src="https://uploads-ssl.webflow.com/5ea5d3315186cf5ec60c3ee4/5edf1c94ce4c859f2b188094_logo.svg" alt="Pip.Services Logo" width="200"> <br/> SQLServer components for .Net

This module is a part of the [Pip.Services](http://pipservices.org) polyglot microservices toolkit.

The module contains the following packages:

- **Build** - a standard factory for constructing components
- **Connect** - instruments for configuring connections to the database.
- **Persistence** - abstract classes for working with the database that can be used for connecting to collections and performing basic CRUD operations


<a name="links"></a> Quick links:

* [Configuration](https://www.pipservices.org/recipies/configuration)
* [API Reference](https://pip-services3-dotnet.github.io/pip-services3-sqlserver-dotnet/)
* [Change Log](CHANGELOG.md)
* [Get Help](https://www.pipservices.org/community/help)
* [Contribute](https://www.pipservices.org/community/contribute)

## Use

Install the dotnet package as
```bash
dotnet add package PipServices3.Sqlserver
```

As an example, lets create persistence for the following data object.

```cs
using PipServices3.Commons.Data;

class MyObject : IIdentifiable<string>
{
    public string Id { get; set; }
    public string key;
    public int value;
}
```

The persistence component shall implement the following interface with a basic set of CRUD operations.

```cs
interface IMyPersistance
{
    void GetPageByFilter(string correlationId, FilterParams filter, PagingParams paging);

    void GetOneById(string correlationId, string id);

    void GetOneByKey(string correlationId, string id);

    void Create(string correlationId, MyObject item);

    void Update(string correlationId, MyObject item);

    void DeleteById(string correlationId, string id);
}
```

To implement sql server persistence component you shall inherit `IdentifiableSqlServerPersistence`. 
Most CRUD operations will come from the base class. You only need to override `getPageByFilter` method with a custom filter function.
And implement a `getOneByKey` custom persistence method that doesn't exist in the base class.

```cs
using PipServices3.SqlServer.Persistence;


class MySqlServerPersistence : IdentifiableJsonSqlServerPersistence<MyObject, string>
{
    public MySqlServerPersistence() : base("myobjects")
    {
        this.AutoCreateObject("CREATE TABLE [myobjects] ([id] VARCHAR(32) PRIMARY KEY, [key] VARCHAR(50), [value] NVARCHAR(255)");

        IndexOptions options = new IndexOptions();
        options.Unique = true;
        options.Type = "unique";

        Dictionary<string, bool> keys = new Dictionary<string, bool>{
            {"[key]", true},
        };

        this.EnsureIndex("myobjects_key", keys, options);
    }

    private string ComposeFilter(FilterParams filter)
    {
        filter = filter != null ? filter : new FilterParams();

        List<string> criteria = new List<string>();

        string id = filter.GetAsNullableString("id");
        if (id != null)
            criteria.Add("[id]='" + id + "'");

        string tempIds = filter.GetAsNullableString("ids");
        if (tempIds != null)
        {
            string[] ids = tempIds.Split(",");
            filters.Add("[id] IN ('" + string.Join("','", ids) + "')");
        }

        string key = filter.GetAsNullableString("key");
        if (key != null)
            criteria.Add("[key]='" + key + "'");

        return criteria.Count > 0 ? string.Join(" AND ", criteria) : null;

    }

    void GetPageByFilter(string correlationId, FilterParams filter, PagingParams paging)
    {
        base.GetPageByFilterAsync(correlationId, this.ComposeFilter(filter), paging, select: "id");
    }

    async void GetOneByKey(string correlationId, string key)
    {
        string query = "SELECT * FROM " + this.QuoteIdentifier(this._tableName) + " WHERE [key]=@1";
        List<string> param = new List<string> { key };

        var result = await this.ExecuteReaderAsync(query, param);
        AnyValueMap item = result != null && result[0] != null ? result[0] : null;

        if (item == null)
            this._logger.Trace(correlationId, "Nothing found from %s with key = %s", this._tableName, key);
        else
            this._logger.Trace(correlationId, "Retrieved from %s with key = %s", this._tableName, key);

        item = this.ConvertToPublic(item);

    }
}
```

Alternatively you can store data in non-relational format using `IdentificableJsonSqlServerPersistence`.
It stores data in tables with two columns - `id` with unique object id and `data` with object data serialized as JSON.
To access data fields you shall use `JSON_VALUE([data],'$.field')` expression.

```cs
class MySqlServerPersistence : IdentifiableJsonSqlServerPersistence<MyObject, string>
{
    public MySqlServerPersistence() : base("myobjects")
    {
        IndexOptions options = new IndexOptions();
        options.Unique = true;
        options.Type = "unique";

        Dictionary<string, bool> keys = new Dictionary<string, bool>{
            {"data_key", true},
        };

        this.EnsureTable();
        this.AutoCreateObject("ALTER TABLE [myobjects] ADD [data_key] AS JSON_VALUE([data],'$.key')");
        this.EnsureIndex("myobjects_key", keys, options);
    }

    private string ComposeFilter(FilterParams filter)
    {
        filter = filter != null ? filter : new FilterParams();

        List<string> criteria = new List<string>();

        string id = filter.GetAsNullableString("id");
        if (id != null)
            criteria.Add("JSON_VALUE([data],'$.id')='" + id + "'");

        string tempIds = filter.GetAsNullableString("ids");
        if (tempIds != null)
        {
            string[] ids = tempIds.Split(",");
            filters.Add("JSON_VALUE([data],'$.id') IN ('" + string.Join("','", ids) + "')");
        }

        string key = filter.GetAsNullableString("key");
        if (key != null)
            criteria.Add("JSON_VALUE([data],'$.key')='" + key + "'");

        return criteria.Count > 0 ? string.Join(" AND ", criteria) : null;

    }

    DataPage<MyObject> GetPageByFilter(string correlationId, FilterParams filter, PagingParams paging)
    {
        return base.GetPageByFilterAsync(correlationId, this.ComposeFilter(filter), paging, "id").Result;
    }

    async void GetOneByKey(string correlationId, string key)
    {
        string query = "SELECT * FROM " + this.QuoteIdentifier(this._tableName) + " WHERE JSON_VALUE([data],'$.key')=@1";
        List<string> param = new List<string> { key };

        var result = await this.ExecuteReaderAsync(query, param);
        AnyValueMap item = result != null && result[0] != null ? result[0] : null;

        if (item == null)
            this._logger.Trace(correlationId, "Nothing found from %s with key = %s", this._tableName, key);
        else
            this._logger.Trace(correlationId, "Retrieved from %s with key = %s", this._tableName, key);

        item = this.ConvertToPublic(item);
    }
}
```

Configuration for your microservice that includes sqlserver persistence may look the following way.

```yaml
...
{{#if SQLSERVER_ENABLED}}
- descriptor: pip-services:connection:sqlserver:con1:1.0
  table: {{SQLSERVER_TABLE}}{{#unless SQLSERVER_TABLE}}myobjects{{/unless}}
  connection:
    uri: {{{SQLSERVER_SERVICE_URI}}}
    host: {{{SQLSERVER_SERVICE_HOST}}}{{#unless SQLSERVER_SERVICE_HOST}}localhost{{/unless}}
    port: {{SQLSERVER_SERVICE_PORT}}{{#unless SQLSERVER_SERVICE_PORT}}1433{{/unless}}
    database: {{SQLSERVER_DB}}{{#unless SQLSERVER_DB}}app{{/unless}}
  credential:
    username: {{SQLSERVER_USER}}
    password: {{SQLSERVER_PASS}}
    
- descriptor: myservice:persistence:sqlserver:default:1.0
  dependencies:
    connection: pip-services:connection:sqlserver:con1:1.0
{{/if}}
...
```

## Develop

For development you shall install the following prerequisites:
* Core .NET SDK 3.1+
* Visual Studio Code or another IDE of your choice
* Docker

Restore dependencies:
```bash
dotnet restore src/src.csproj
```

Compile the code:
```bash
dotnet build src/src.csproj
```

Run automated tests:
```bash
dotnet restore test/test.csproj
dotnet test test/test.csproj
```

Generate API documentation:
```bash
./docgen.ps1
```

Before committing changes run dockerized build and test as:
```bash
./build.ps1
./test.ps1
./clear.ps1
```

## Contacts

The library is created and maintained by **Sergey Seroukhov**.

