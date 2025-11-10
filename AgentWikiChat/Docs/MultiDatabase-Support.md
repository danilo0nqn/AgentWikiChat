# ??? Soporte Multi-Base de Datos - AgentWikiChat PRO

## Descripción

AgentWikiChat PRO ahora soporta **múltiples proveedores de bases de datos** con la misma herramienta `query_database`. La arquitectura permite cambiar fácilmente entre SQL Server, PostgreSQL y otros proveedores simplemente modificando la configuración.

---

## ?? Proveedores Soportados

| Proveedor | Nombre Config | Paquete NuGet | Estado |
|-----------|---------------|---------------|--------|
| **SQL Server** | `SqlServer` | `Microsoft.Data.SqlClient` | ? Implementado |
| **PostgreSQL** | `PostgreSQL` o `Postgres` | `Npgsql` | ? Implementado |
| **MySQL** | `MySQL` | `MySqlConnector` | ?? Futuro |
| **SQLite** | `SQLite` | `Microsoft.Data.Sqlite` | ?? Futuro |

---

## ?? Configuración

### 1. SQL Server

```json
{
  "Database": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=localhost;Database=MyDatabase;User Id=sa;Password=MyPassword;TrustServerCertificate=True;",
    "CommandTimeout": 90,
    "MaxRowsToReturn": 100,
    "EnableQueryLogging": true
  }
}
```

**Formatos de Connection String:**

```
# Autenticación SQL
Server=localhost;Database=MyDB;User Id=sa;Password=MyPass;TrustServerCertificate=True;

# Autenticación Windows
Server=localhost;Database=MyDB;Integrated Security=True;TrustServerCertificate=True;

# SQL Server con instancia nombrada
Server=localhost\SQLEXPRESS;Database=MyDB;User Id=sa;Password=MyPass;

# SQL Server remoto
Server=192.168.1.100,1433;Database=MyDB;User Id=sa;Password=MyPass;

# Azure SQL Database
Server=myserver.database.windows.net;Database=MyDB;User Id=myuser@myserver;Password=MyPass;Encrypt=True;
```

---

### 2. PostgreSQL

```json
{
  "Database": {
    "Provider": "PostgreSQL",
    "ConnectionString": "Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=MyPassword;",
    "CommandTimeout": 90,
    "MaxRowsToReturn": 100,
    "EnableQueryLogging": true
  }
}
```

**Formatos de Connection String:**

```
# Básico
Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=MyPass;

# Con SSL
Host=localhost;Database=mydb;Username=postgres;Password=MyPass;SSL Mode=Require;

# PostgreSQL en Docker
Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=MyPass;

# PostgreSQL remoto
Host=192.168.1.100;Port=5432;Database=mydb;Username=pguser;Password=MyPass;

# Amazon RDS PostgreSQL
Host=mydb.abc123.us-east-1.rds.amazonaws.com;Database=mydb;Username=postgres;Password=MyPass;

# Azure Database for PostgreSQL
Host=myserver.postgres.database.azure.com;Database=mydb;Username=myuser@myserver;Password=MyPass;SSL Mode=Require;
```

---

## ??? Arquitectura

### Diagrama de Componentes

```
SqlServerToolHandler (IToolHandler)
         ?
    Usa IDatabaseHandler
         ?
         ??? SqlServerDatabaseHandler
         ??? PostgreSqlDatabaseHandler
         ??? MySqlDatabaseHandler (futuro)
         ??? SQLiteDatabaseHandler (futuro)
```

### Clases Principales

#### 1. **IDatabaseHandler** (Interfaz)
Define el contrato para todos los proveedores:
- `ValidateQuery(query)` - Validación de seguridad
- `ExecuteQueryAsync(query, maxRows)` - Ejecución de consultas
- `GetTablesAsync()` - Lista de tablas
- `GetTableSchemaAsync(tableName)` - Esquema de tabla

#### 2. **BaseDatabaseHandler** (Clase Base)
Lógica común de validación:
- Validación de keywords prohibidas
- Remoción de comentarios SQL
- Verificación de múltiples instrucciones
- Utilities compartidas

#### 3. **SqlServerDatabaseHandler**
Implementación para SQL Server:
- Usa `Microsoft.Data.SqlClient`
- Queries específicas de SQL Server (INFORMATION_SCHEMA)
- Manejo de schemas (dbo, etc.)

#### 4. **PostgreSqlDatabaseHandler**
Implementación para PostgreSQL:
- Usa `Npgsql`
- Queries específicas de PostgreSQL (pg_catalog)
- Manejo de schemas (public, etc.)

#### 5. **DatabaseHandlerFactory**
Factory pattern para crear el handler apropiado según configuración.

---

## ?? Seguridad

La validación de seguridad es **común para todos los proveedores**:

### ? Permitido
- Consultas `SELECT`
- `WHERE`, `JOIN`, `GROUP BY`, `HAVING`, `ORDER BY`
- Funciones agregadas: `COUNT`, `SUM`, `AVG`, `MAX`, `MIN`
- CTEs (Common Table Expressions)
- Subconsultas SELECT

### ? Prohibido
- `INSERT`, `UPDATE`, `DELETE`
- `DROP`, `CREATE`, `ALTER`, `TRUNCATE`
- `EXEC`, `EXECUTE` (stored procedures)
- Procedimientos del sistema
- `BACKUP`, `RESTORE`
- `GRANT`, `REVOKE`, `DENY`
- Múltiples instrucciones (`;`)
- Comentarios inline (`--`)

---

## ?? Uso

### Cambiar de SQL Server a PostgreSQL

**Paso 1**: Cambiar el proveedor en `appsettings.json`
```json
{
  "Database": {
    "Provider": "PostgreSQL",  // ? Cambiar aquí
    "ConnectionString": "Host=localhost;Database=mydb;Username=postgres;Password=MyPass;"
  }
}
```

**Paso 2**: Reiniciar la aplicación
```bash
dotnet run
```

**Paso 3**: Verificar
```
?? Proveedor IA: LM Studio (Local)
?? Base de Datos: PostgreSQL  ? Verás esto en el log
```

---

## ?? Diferencias Entre Proveedores

### SQL Server vs PostgreSQL

| Característica | SQL Server | PostgreSQL |
|----------------|------------|------------|
| **TOP N** | `SELECT TOP 10 * FROM table` | `SELECT * FROM table LIMIT 10` |
| **LIMIT OFFSET** | `OFFSET N ROWS FETCH NEXT M ROWS ONLY` | `LIMIT M OFFSET N` |
| **String Concat** | `+` o `CONCAT()` | `||` o `CONCAT()` |
| **Schema por defecto** | `dbo` | `public` |
| **Case Sensitivity** | Case-insensitive (por defecto) | Case-sensitive |
| **Boolean** | `BIT` (0/1) | `BOOLEAN` (true/false) |
| **Date Functions** | `GETDATE()`, `DATEADD()` | `NOW()`, `DATE_ADD()` |

### Ejemplos de Queries Equivalentes

**Obtener TOP 10 registros:**
```sql
-- SQL Server
SELECT TOP 10 * FROM Users ORDER BY Id DESC

-- PostgreSQL
SELECT * FROM Users ORDER BY Id DESC LIMIT 10
```

**Paginación:**
```sql
-- SQL Server
SELECT * FROM Users 
ORDER BY Id 
OFFSET 20 ROWS FETCH NEXT 10 ROWS ONLY

-- PostgreSQL
SELECT * FROM Users 
ORDER BY Id 
LIMIT 10 OFFSET 20
```

**Concatenación de strings:**
```sql
-- SQL Server
SELECT FirstName + ' ' + LastName AS FullName FROM Users

-- PostgreSQL
SELECT FirstName || ' ' || LastName AS FullName FROM Users
```

---

## ?? Testing

### Verificar SQL Server

```json
{
  "Database": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=localhost;Database=MyDB;Integrated Security=True;"
  }
}
```

**Pruebas:**
```
?? Tú> ¿Cuántas tablas hay?
?? Tú> Dame las últimas 10 filas de la tabla Users
?? Tú> Muestra el esquema de la tabla Products
```

### Verificar PostgreSQL

```json
{
  "Database": {
    "Provider": "PostgreSQL",
    "ConnectionString": "Host=localhost;Database=mydb;Username=postgres;Password=postgres;"
  }
}
```

**Pruebas:**
```
?? Tú> ¿Cuántas tablas hay en el schema public?
?? Tú> Dame los primeros 5 usuarios
?? Tú> Cuenta los pedidos del último mes
```

---

## ?? Extensión: Agregar Nuevo Proveedor

### Ejemplo: MySQL

**Paso 1**: Instalar paquete NuGet
```bash
dotnet add package MySqlConnector
```

**Paso 2**: Crear handler
```csharp
// Services/Database/MySqlDatabaseHandler.cs
public class MySqlDatabaseHandler : BaseDatabaseHandler
{
    public override string ProviderName => "MySQL";
    
    // Implementar métodos abstractos...
}
```

**Paso 3**: Actualizar factory
```csharp
// Services/Database/DatabaseHandlerFactory.cs
return providerType switch
{
    "sqlserver" => new SqlServerDatabaseHandler(...),
    "postgresql" => new PostgreSqlDatabaseHandler(...),
    "mysql" => new MySqlDatabaseHandler(...),  // ? Agregar
    _ => throw new NotSupportedException(...)
};
```

**Paso 4**: Configurar
```json
{
  "Database": {
    "Provider": "MySQL",
    "ConnectionString": "Server=localhost;Database=mydb;User=root;Password=MyPass;"
  }
}
```

---

## ?? Ejemplos de Uso

### SQL Server

```
?? Tú> ¿Cuántos clientes hay en la base de datos?

?? Bot: [Invoca query_database]
Query: SELECT COUNT(*) AS TotalClientes FROM Clientes

?? Resultado de la Consulta (SQL Server)
Query: SELECT COUNT(*) AS TotalClientes FROM Clientes
Filas retornadas: 1
Tiempo de ejecución: 12.34ms

Resultados:
| TotalClientes |
| --- |
| 1542 |

?? Bot: En la base de datos hay 1,542 clientes registrados.
```

### PostgreSQL

```
?? Tú> Dame los 5 productos más caros

?? Bot: [Invoca query_database]
Query: SELECT nombre, precio FROM productos ORDER BY precio DESC LIMIT 5

?? Resultado de la Consulta (PostgreSQL)
Query: SELECT nombre, precio FROM productos ORDER BY precio DESC LIMIT 5
Filas retornadas: 5
Tiempo de ejecución: 8.76ms

Resultados:
| nombre | precio |
| --- | --- |
| Laptop Pro | 1299.99 |
| Smartphone X | 899.00 |
| Tablet Max | 699.50 |
| Monitor 4K | 449.99 |
| Teclado RGB | 189.99 |
```

---

## ?? Limitaciones Conocidas

### SQL Server
- Requiere `TrustServerCertificate=True` para certificados autofirmados
- Autenticación Windows solo funciona en Windows

### PostgreSQL
- Por defecto usa el schema `public`
- Case-sensitive en nombres de tablas/columnas
- Requiere especificar puerto (usualmente 5432)

---

## ?? Troubleshooting

### Error: "Proveedor no soportado"
**Solución**: Verifica que `Database:Provider` sea uno de: `SqlServer`, `PostgreSQL`, `Postgres`

### Error: "Connection timeout"
**Solución**: Aumenta `CommandTimeout` en configuración o verifica conectividad

### Error: "Invalid object name 'table'"
**SQL Server**: Verifica que uses el schema correcto (ej: `dbo.table`)
**PostgreSQL**: Verifica que uses el schema correcto (ej: `public.table`)

### Error: Sintaxis no válida al cambiar de proveedor
**Solución**: Revisa las diferencias de sintaxis (TOP vs LIMIT, etc.)

---

## ?? Referencias

### SQL Server
- [Microsoft.Data.SqlClient - GitHub](https://github.com/dotnet/SqlClient)
- [Connection Strings](https://www.connectionstrings.com/sql-server/)
- [T-SQL Reference](https://docs.microsoft.com/en-us/sql/t-sql/)

### PostgreSQL
- [Npgsql - GitHub](https://github.com/npgsql/npgsql)
- [Connection Strings](https://www.connectionstrings.com/postgresql/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)

---

## ? Ventajas de la Arquitectura

| Ventaja | Descripción |
|---------|-------------|
| ?? **Intercambiable** | Cambiar de proveedor sin modificar código |
| ?? **Seguro** | Validación común para todos |
| ?? **Extensible** | Agregar nuevos proveedores fácilmente |
| ?? **Single Tool** | Una sola herramienta para todas las BDs |
| ?? **Encapsulado** | Lógica específica aislada por proveedor |

---

**Versión**: 1.0.0  
**Última actualización**: 2025-01-06  
**Compatibilidad**: AgentWikiChat PRO v3.3.0+
