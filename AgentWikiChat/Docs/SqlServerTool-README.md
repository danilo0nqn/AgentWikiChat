# ?? Database Tool - Documentación

## Descripción

La herramienta `query_database` permite al agente ejecutar consultas SQL SELECT contra bases de datos de manera segura y controlada.

**? Soporte Multi-Base de Datos**: SQL Server, PostgreSQL, y más (extensible).

## ??? Proveedores Soportados

| Proveedor | Estado | Paquete NuGet |
|-----------|--------|---------------|
| **SQL Server** | ? Activo | Microsoft.Data.SqlClient 6.1.2 |
| **PostgreSQL** | ? Activo | Npgsql 9.0.4 |
| **MySQL** | ?? Futuro | MySqlConnector |
| **SQLite** | ?? Futuro | Microsoft.Data.Sqlite |

---

## ?? Seguridad

### Restricciones Implementadas

La herramienta está diseñada con **máxima seguridad** y solo permite operaciones de **solo lectura**:

? **PERMITIDO:**
- Consultas `SELECT`
- Cláusulas: `WHERE`, `JOIN`, `GROUP BY`, `HAVING`, `ORDER BY`
- Funciones agregadas: `COUNT`, `SUM`, `AVG`, `MAX`, `MIN`
- CTEs (`WITH`)

? **PROHIBIDO:**
- `INSERT`, `UPDATE`, `DELETE`
- `DROP`, `CREATE`, `ALTER`, `TRUNCATE`
- `EXEC`, `EXECUTE` (stored procedures)
- Procedimientos del sistema: `SP_*`, `XP_*`
- `BACKUP`, `RESTORE`
- `GRANT`, `REVOKE`, `DENY`
- Múltiples instrucciones (separadas por `;`)
- Comentarios inline (`--`) por seguridad

### Validaciones Automáticas

1. **Validación de sintaxis**: Verifica que la consulta comience con `SELECT`
2. **Lista negra de keywords**: Detecta palabras clave prohibidas
3. **Prevención de SQL Injection**: Bloqueo de múltiples instrucciones
4. **Límite de filas**: Máximo configurable (por defecto: 1000 filas)
5. **Timeout**: Tiempo máximo de ejecución configurable (por defecto: 30s)

---

## ?? Configuración

### appsettings.json

```json
{
  "Database": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=localhost;Database=MyDB;User Id=sa;Password=MyPass;TrustServerCertificate=True;",
    "CommandTimeout": 90,
    "MaxRowsToReturn": 100,
    "EnableQueryLogging": true
  }
}
```

### Parámetros de Configuración

| Parámetro | Descripción | Por defecto |
|-----------|-------------|-------------|
| `Provider` | Proveedor de BD: `SqlServer`, `PostgreSQL` | *(requerido)* |
| `ConnectionString` | Cadena de conexión | *(requerido)* |
| `CommandTimeout` | Timeout en segundos | 30 |
| `MaxRowsToReturn` | Máximo de filas a retornar | 1000 |
| `EnableQueryLogging` | Habilita logging de consultas | true |

---

## ?? Cambiar de Proveedor

### SQL Server

```json
{
  "Database": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=localhost;Database=MyDB;Integrated Security=True;TrustServerCertificate=True;"
  }
}
```

### PostgreSQL

```json
{
  "Database": {
    "Provider": "PostgreSQL",
    "ConnectionString": "Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=MyPass;"
  }
}
```

---

## ?? Uso

### Ejemplos de Consultas al Agente

**Ejemplo 1: Consulta simple**
```
Usuario: "¿Cuántos usuarios hay en la base de datos?"

Agente: query_database({
  "query": "SELECT COUNT(*) AS TotalUsuarios FROM Users"
})
```

**Ejemplo 2: Consulta con filtros (SQL Server)**
```
Usuario: "Muéstrame los últimos 10 pedidos con sus clientes"

Agente: query_database({
  "query": "SELECT TOP 10 o.OrderId, o.OrderDate, c.CustomerName, o.Total FROM Orders o JOIN Customers c ON o.CustomerId = c.CustomerId ORDER BY o.OrderDate DESC"
})
```

**Ejemplo 3: Consulta con LIMIT (PostgreSQL)**
```
Usuario: "Dame los 5 productos más caros"

Agente: query_database({
  "query": "SELECT product_name, price FROM products ORDER BY price DESC LIMIT 5"
})
```

**Ejemplo 4: Consulta con agregación**
```
Usuario: "Dame las ventas totales por mes del último año"

Agente: query_database({
  "query": "SELECT YEAR(OrderDate) AS Año, MONTH(OrderDate) AS Mes, SUM(Total) AS VentaTotal FROM Orders WHERE OrderDate >= DATEADD(YEAR, -1, GETDATE()) GROUP BY YEAR(OrderDate), MONTH(OrderDate) ORDER BY Año DESC, Mes DESC"
})
```

---

## ?? Formato de Salida

El resultado se presenta en formato Markdown con:

- **Proveedor de BD** (SQL Server, PostgreSQL, etc.)
- **Query ejecutada** (truncada a 200 caracteres)
- **Filas retornadas**
- **Tiempo de ejecución** en milisegundos
- **Tabla de resultados** en formato Markdown

Ejemplo:
```
?? Resultado de la Consulta (SQL Server)

Query: `SELECT TOP 5 ProductName, Price FROM Products ORDER BY Price DESC`
Filas retornadas: 5
Tiempo de ejecución: 45.32ms

Resultados:

| ProductName | Price |
| --- | --- |
| Laptop Pro | 1299.99 |
| Smartphone X | 899.00 |
| Tablet Max | 699.50 |
| Monitor 4K | 449.99 |
| Keyboard RGB | 89.99 |
```

---

## ?? Mensajes de Error

### Error: Consulta rechazada
```
?? Consulta Rechazada por Seguridad

Palabra clave prohibida detectada: 'UPDATE'. Solo se permiten consultas de solo lectura (SELECT).

?? Recuerda: Solo se permiten consultas SELECT de solo lectura.
```

### Error: SQL Exception (SQL Server)
```
? Error en SQL Server

Mensaje: Invalid column name 'NonExistentColumn'.

?? Verifica la sintaxis de tu consulta SQL y que la tabla/columna exista.
```

### Error: PostgreSQL Exception
```
? Error en PostgreSQL

Mensaje: relation "table_name" does not exist

?? Verifica la sintaxis de tu consulta SQL y que la tabla/columna exista.
```

---

## ?? Testing

### Consultas de Prueba Seguras (SQL Server)

```sql
-- Listar tablas
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'

-- Esquema de una tabla
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'YourTable'

-- Contar registros
SELECT COUNT(*) FROM YourTable

-- Top N registros
SELECT TOP 10 * FROM YourTable ORDER BY Id DESC
```

### Consultas de Prueba Seguras (PostgreSQL)

```sql
-- Listar tablas
SELECT schemaname || '.' || tablename FROM pg_catalog.pg_tables WHERE schemaname NOT IN ('pg_catalog', 'information_schema')

-- Esquema de una tabla
SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_name = 'your_table'

-- Contar registros
SELECT COUNT(*) FROM your_table

-- Limit N registros
SELECT * FROM your_table ORDER BY id DESC LIMIT 10
```

### Consultas que Serán Rechazadas

```sql
-- ? UPDATE
UPDATE Users SET Status = 'Active'

-- ? DELETE
DELETE FROM Orders WHERE OrderDate < '2020-01-01'

-- ? DROP
DROP TABLE OldData

-- ? EXEC
EXEC sp_updatestats

-- ? Múltiples instrucciones
SELECT * FROM Users; DROP TABLE Users;
```

---

## ?? Troubleshooting

### Error: "Proveedor no soportado"
**Solución**: Verifica que `Database:Provider` sea `SqlServer`, `PostgreSQL` o `Postgres`.

### Error: "ConnectionString no configurada"
**Solución**: Verifica que `appsettings.json` tenga la sección `Database` con `ConnectionString`.

### Error: "Login failed for user"
**Solución**: Verifica las credenciales en la cadena de conexión y que el usuario tenga permisos de lectura.

### Error: "Timeout expired"
**Solución**: Aumenta `CommandTimeout` en `appsettings.json` o optimiza la consulta SQL.

### Error: Sintaxis diferente entre proveedores
**Solución**: 
- SQL Server usa `TOP N`, PostgreSQL usa `LIMIT N`
- SQL Server: `GETDATE()`, PostgreSQL: `NOW()`
- Consulta la documentación de diferencias de sintaxis

---

## ??? Arquitectura

### Componentes

```
SqlServerToolHandler (IToolHandler)
         ?
    IDatabaseHandler (interfaz)
         ?
         ??? SqlServerDatabaseHandler
         ??? PostgreSqlDatabaseHandler
         ??? BaseDatabaseHandler (validación común)
```

### Factory Pattern

`DatabaseHandlerFactory` crea el handler apropiado según configuración:
- Lee `Database:Provider` de appsettings.json
- Instancia el handler correcto
- Pasa la connection string y parámetros

---

## ?? Extensión: Agregar Nuevo Proveedor

Ver documentación completa en `Docs/MultiDatabase-Support.md`.

**Resumen:**
1. Instalar paquete NuGet del proveedor
2. Crear clase heredando de `BaseDatabaseHandler`
3. Implementar métodos abstractos
4. Agregar al `DatabaseHandlerFactory`
5. Configurar en `appsettings.json`

---

## ?? Notas Adicionales

- El handler guarda un registro de cada consulta ejecutada en la memoria modular (`database`)
- Los logs detallados están disponibles cuando `Debug` y `EnableQueryLogging` están habilitados
- La validación se ejecuta **antes** de enviar la query al servidor
- Compatible con SQL Server 2012+, PostgreSQL 10+
- Soporte multi-tenant: un proveedor por aplicación

---

## ?? Referencias

- **Multi-Database Support**: `Docs/MultiDatabase-Support.md`
- **SQL Server**: [Microsoft.Data.SqlClient](https://github.com/dotnet/SqlClient)
- **PostgreSQL**: [Npgsql](https://github.com/npgsql/npgsql)

---

**Versión**: 2.0.0 (Multi-Database Support)  
**Última actualización**: 2025-01-06  
**Autor**: AgentWikiChat Team
