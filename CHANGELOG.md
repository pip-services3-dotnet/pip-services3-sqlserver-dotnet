# <img src="https://uploads-ssl.webflow.com/5ea5d3315186cf5ec60c3ee4/5edf1c94ce4c859f2b188094_logo.svg" alt="Pip.Services Logo" width="200"> <br/> SQLServer components for .Net Changelog

## <a name="3.3.1"></a> 3.3.4 (2021-11-19)

### Features
* Improved logic via SSH tunnel

## <a name="3.3.0"></a> 3.3.1 (2021-11-16)

### Features
* Added SQLServer connection via SSH tunnel

## <a name="3.3.0"></a> 3.3.0 (2021-09-01)

### Breaking Changes
* Migrate to .NET Core 5.0

## <a name="3.2.0"></a> 3.2.0 (2021-06-09) 

### Features
* Updated references as PipServices3.Components have got minor changes

## <a name="3.1.0"></a> 3.1.0 (2021-02-19) 

### Features
* Renamed autoCreateObject to ensureSchema
* Added defineSchema method that shall be overriden in child classes
* Added clearSchema method

### Breaking changes
* Method autoCreateObject is deprecated and shall be renamed to ensureSchema

## <a name="3.0.1"></a> 3.0.1 (2020-10-27)

### Features
* added convert to json

### Bug Fixes
* fixed error message
* fixed project version

## <a name="3.0.0"></a> 3.0.0 (2020-10-22)

Initial public release

### Features
* Added DefaultSqlServerFactory
* Added SqlServerConnectionResolver
* Added IdentifiableJsonSqlServerPersistence
* Added IdentifiableSqlServerPersistence
* Added IndexOptions
* Added SqlServerConnection
* Added SqlServerPersistence

### Bug Fixes
No fixes in this version

