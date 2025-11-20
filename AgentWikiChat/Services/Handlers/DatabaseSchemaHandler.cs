using AgentWikiChat.Models;
using AgentWikiChat.Services.Database;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace AgentWikiChat.Services.Handlers;

/// <summary>
/// Handler para explorar el esquema de bases de datos (tablas, columnas, tipos).
/// Permite al agente descubrir la estructura de la BD antes de hacer queries.
/// SEGURIDAD: Solo operaciones de lectura de metadatos (INFORMATION_SCHEMA).
/// </summary>
public class DatabaseSchemaHandler : IToolHandler
{
    private readonly IDatabaseHandler _dbHandler;
    private readonly DatabaseProviderConfig _providerConfig;
    private readonly bool _debugMode = true;

    public DatabaseSchemaHandler(IConfiguration configuration)
    {
        // Reutilizar el handler existente
        _dbHandler = DatabaseHandlerFactory.CreateHandler(configuration);
        _providerConfig = DatabaseHandlerFactory.GetActiveProviderConfig(configuration);

        LogDebug($"[SchemaExplorer] Inicializado - Provider: {_providerConfig.Name}");
    }

    public string ToolName => "explore_database_schema";

    public ToolDefinition GetToolDefinition()
    {
        return new ToolDefinition
        {
            Type = "function",
            Function = new FunctionDefinition
            {
                Name = ToolName,
                Description = $"Explora la estructura del esquema de base de datos ({_providerConfig.Name}). Operaciones: 'list_tables' (lista tablas con filtro opcional), 'search_tables' (buscar por nombre parcial - USA ESTO para bases grandes), 'describe_table' (mostrar columnas/tipos). SOLO LECTURA.",
                Parameters = new FunctionParameters
                {
                    Type = "object",
                    Properties = new Dictionary<string, PropertyDefinition>
                    {
                        ["operation"] = new PropertyDefinition
                        {
                            Type = "string",
                            Description = "Operación: 'list_tables', 'describe_table', o 'search_tables'",
                            Enum = new List<string> { "list_tables", "describe_table", "search_tables" }
                        },
                        ["table_name"] = new PropertyDefinition
                        {
                            Type = "string",
                            Description = "Nombre o término de búsqueda de tabla. REQUERIDO para 'describe_table' y 'search_tables'. Para search_tables, usa un término que describa el TIPO de datos que buscás (no un valor específico). Para describe_table, usa el nombre exacto de la tabla."
                        },
                        ["schema_filter"] = new PropertyDefinition
                        {
                            Type = "string",
                            Description = "Filtrar por schema específico. Opcional. Útil solo con list_tables."
                        },
                        ["limit"] = new PropertyDefinition
                        {
                            Type = "string",
                            Description = "Límite de tablas a retornar (por defecto: 50, máximo: 200). Solo para list_tables."
                        },
                        ["include_row_count"] = new PropertyDefinition
                        {
                            Type = "string",
                            Description = "¿Incluir conteo de filas por tabla? (true/false, por defecto: false)"
                        }
                    },
                    Required = new List<string> { "operation" }
                }
            }
        };
    }

    public async Task<string> HandleAsync(ToolParameters parameters, MemoryService memory)
    {
        var operation = parameters.GetString("operation");
        var tableName = parameters.GetString("table_name", "");
        var schemaFilter = parameters.GetString("schema_filter", "");
        var limitStr = parameters.GetString("limit", "50");
        var includeRowCount = parameters.GetString("include_row_count", "false").ToLower() == "true";

        if (!int.TryParse(limitStr, out var limit))
        {
            limit = 50;
        }

        // Cap al máximo configurado
        limit = Math.Min(limit, 200);

        if (string.IsNullOrWhiteSpace(operation))
        {
            return "⚠️ Error: La operación no puede estar vacía.";
        }

        LogDebug($"[SchemaExplorer] Operación: {operation}, Tabla: {tableName}, Schema: {schemaFilter}, Limit: {limit}");

        try
        {
            string result = operation switch
            {
                "list_tables" => await ListTablesAsync(includeRowCount, schemaFilter, limit),
                "describe_table" => await DescribeTableAsync(tableName),
                "search_tables" => await SearchTablesAsync(tableName, includeRowCount),
                _ => $"❌ Operación desconocida: {operation}. Usa: list_tables, describe_table, search_tables"
            };

            // Guardar en memoria modular
            memory.AddToModule("database_schema", "system", $"{operation}: {tableName} - Ejecutado");

            return result;
        }
        catch (Exception ex)
        {
            LogError($"[SchemaExplorer] Error: {ex.Message}");
            return $"❌ **Error explorando esquema**\n\n" +
                   $"**Mensaje**: {ex.Message}\n\n" +
                   $"💡 Verifica que tengas permisos para leer INFORMATION_SCHEMA.";
        }
    }

    #region Schema Operations

    /// <summary>
    /// Lista tablas de la base de datos con paginación inteligente.
    /// </summary>
    private async Task<string> ListTablesAsync(bool includeRowCount, string schemaFilter, int limit)
    {
        // Query base
        var whereClause = "WHERE TABLE_TYPE = 'BASE TABLE'";

        // Agregar filtro de schema si existe
        if (!string.IsNullOrWhiteSpace(schemaFilter))
        {
            whereClause += _dbHandler.ProviderName switch
            {
                "SQL Server" => $" AND TABLE_SCHEMA = '{SanitizeTableName(schemaFilter)}'",
                "PostgreSQL" => $" AND table_schema = '{SanitizeTableName(schemaFilter).ToLower()}'",
                _ => ""
            };
        }
        else
        {
            // Filtros por defecto para PostgreSQL
            if (_dbHandler.ProviderName == "PostgreSQL")
            {
                whereClause += " AND table_schema NOT IN ('pg_catalog', 'information_schema')";
            }
        }

        var query = _dbHandler.ProviderName switch
        {
            "SQL Server" => $@"
                SELECT TOP {limit}
                    TABLE_SCHEMA,
                    TABLE_NAME,
                    TABLE_TYPE
                FROM INFORMATION_SCHEMA.TABLES
                {whereClause}
                ORDER BY TABLE_SCHEMA, TABLE_NAME",

            "PostgreSQL" => $@"
                SELECT 
                    table_schema,
                    table_name,
                    table_type
                FROM information_schema.tables
                {whereClause}
                ORDER BY table_schema, table_name
                LIMIT {limit}",

            _ => throw new NotSupportedException($"Proveedor {_dbHandler.ProviderName} no soportado")
        };

        var result = await _dbHandler.ExecuteQueryAsync(query, limit);

        if (result.RowCount == 0)
        {
            return string.IsNullOrWhiteSpace(schemaFilter)
                ? "ℹ️ No se encontraron tablas en la base de datos."
                : $"ℹ️ No se encontraron tablas en el schema '{schemaFilter}'.";
        }

        var output = new StringBuilder();
        output.AppendLine($"📊 **Tablas en la Base de Datos** ({_providerConfig.Name})\n");

        if (!string.IsNullOrWhiteSpace(schemaFilter))
        {
            output.AppendLine($"**Schema filtrado**: `{schemaFilter}`");
        }

        output.AppendLine($"**Mostrando**: {result.RowCount} de {limit} tablas (límite configurado)");

        // Advertencia si estamos en el límite
        if (result.RowCount == limit)
        {
            output.AppendLine($"\n⚠️ **ADVERTENCIA**: Se alcanzó el límite de {limit} tablas.");
            output.AppendLine($"💡 **Sugerencias**:");
            output.AppendLine($"   - Usa `search_tables` con un término específico para encontrar tablas");
            output.AppendLine($"   - Usa `schema_filter` para filtrar por schema específico");
            output.AppendLine($"   - Incrementa el `limit` si necesitás ver más (máximo 200)\n");
        }

        output.AppendLine("\n---\n");

        var schemaGroups = result.Rows
            .GroupBy(row => row[0]?.ToString() ?? "default")
            .OrderBy(g => g.Key);

        foreach (var schemaGroup in schemaGroups)
        {
            output.AppendLine($"### Schema: `{schemaGroup.Key}` ({schemaGroup.Count()} tablas)\n");

            foreach (var row in schemaGroup)
            {
                var tableName = row[1]?.ToString() ?? "Unknown";
                output.Append($"- **{tableName}**");

                // Opcionalmente incluir conteo de filas (NO recomendado para muchas tablas)
                if (includeRowCount)
                {
                    try
                    {
                        var countQuery = $"SELECT COUNT(*) FROM [{schemaGroup.Key}].[{tableName}]";
                        var countResult = await _dbHandler.ExecuteQueryAsync(countQuery, 1);
                        var count = countResult.Rows[0][0]?.ToString() ?? "0";
                        output.Append($" ({count} filas)");
                    }
                    catch
                    {
                        output.Append(" (conteo no disponible)");
                    }
                }

                output.AppendLine();
            }

            output.AppendLine();
        }

        output.AppendLine("---\n");
        output.AppendLine($"💡 **Siguientes pasos**:");
        output.AppendLine($"   - Usa `describe_table` para ver estructura de una tabla específica");
        output.AppendLine($"   - Usa `search_tables` con un término para buscar tablas específicas");

        return output.ToString();
    }

    /// <summary>
    /// Describe la estructura de una tabla (columnas, tipos, nullable, etc.).
    /// </summary>
    private async Task<string> DescribeTableAsync(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return "⚠️ Error: Debes especificar el nombre de la tabla.";
        }

        // Separar schema y tabla si vienen juntos (ej: "dbo.Personas" → schema="dbo", table="Personas")
        var schema = "dbo";
        var table = tableName;

        if (tableName.Contains('.'))
        {
            var parts = tableName.Split('.');
            if (parts.Length == 2)
            {
                schema = parts[0];
                table = parts[1];
            }
        }

        var query = _dbHandler.ProviderName switch
        {
            "SQL Server" => $@"
                SELECT 
                    COLUMN_NAME,
                    DATA_TYPE,
                    CHARACTER_MAXIMUM_LENGTH,
                    IS_NULLABLE,
                    COLUMN_DEFAULT
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = '{SanitizeTableName(schema)}'
                    AND TABLE_NAME = '{SanitizeTableName(table)}'
                ORDER BY ORDINAL_POSITION",

            "PostgreSQL" => $@"
                SELECT 
                    column_name,
                    data_type,
                    character_maximum_length,
                    is_nullable,
                    column_default
                FROM information_schema.columns
                WHERE table_schema = '{SanitizeTableName(schema).ToLower()}'
                    AND table_name = '{SanitizeTableName(table).ToLower()}'
                ORDER BY ordinal_position",

            _ => throw new NotSupportedException($"Proveedor {_dbHandler.ProviderName} no soportado")
        };

        var result = await _dbHandler.ExecuteQueryAsync(query, 200);

        if (result.RowCount == 0)
        {
            return $"❌ No se encontró la tabla `{tableName}` (buscando schema: '{schema}', tabla: '{table}').\n\n" +
                   $"💡 **Sugerencia**: Usa `search_tables` con término '{table}' para ver tablas similares disponibles.";
        }

        var output = new StringBuilder();
        output.AppendLine($"📋 **Estructura de la Tabla: `{schema}.{table}`**\n");
        output.AppendLine($"**Columnas**: {result.RowCount}\n");
        output.AppendLine("| Columna | Tipo | Longitud | Nullable | Default |");
        output.AppendLine("|---------|------|----------|----------|---------|");

        foreach (var row in result.Rows)
        {
            var columnName = row[0]?.ToString() ?? "?";
            var dataType = row[1]?.ToString() ?? "?";
            var maxLength = row[2]?.ToString() ?? "-";
            var nullable = row[3]?.ToString() == "YES" ? "✅" : "❌";
            var defaultValue = row[4]?.ToString() ?? "-";

            output.AppendLine($"| {columnName} | {dataType} | {maxLength} | {nullable} | {TruncateForDisplay(defaultValue, 30)} |");
        }

        output.AppendLine("\n---\n");
        output.AppendLine($"💡 **Ejemplo de query**: \n```sql\nSELECT TOP 5 * FROM {schema}.{table}\n```");

        return output.ToString();
    }

    /// <summary>
    /// Busca tablas que coincidan con un término parcial.
    /// </summary>
    private async Task<string> SearchTablesAsync(string searchTerm, bool includeRowCount)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return "⚠️ Error: Debes especificar un término de búsqueda.";
        }

        var query = _dbHandler.ProviderName switch
        {
            "SQL Server" => $@"
                SELECT 
                    TABLE_SCHEMA,
                    TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_TYPE = 'BASE TABLE'
                    AND TABLE_NAME LIKE '%{SanitizeTableName(searchTerm)}%'
                ORDER BY TABLE_NAME",

            "PostgreSQL" => $@"
                SELECT 
                    table_schema,
                    table_name
                FROM information_schema.tables
                WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
                    AND table_type = 'BASE TABLE'
                    AND table_name LIKE '%{SanitizeTableName(searchTerm).ToLower()}%'
                ORDER BY table_name",

            _ => throw new NotSupportedException($"Proveedor {_dbHandler.ProviderName} no soportado")
        };

        var result = await _dbHandler.ExecuteQueryAsync(query, 100);

        if (result.RowCount == 0)
        {
            return $"🔍 No se encontraron tablas que coincidan con `{searchTerm}`.\n\n" +
                   $"💡 **Sugerencia**: Usa `list_tables` para ver todas las tablas disponibles.";
        }

        var output = new StringBuilder();
        output.AppendLine($"🔍 **Resultados de búsqueda para: `{searchTerm}`**\n");
        output.AppendLine($"**Tablas encontradas**: {result.RowCount}\n");

        foreach (var row in result.Rows)
        {
            var schema = row[0]?.ToString() ?? "dbo";
            var table = row[1]?.ToString() ?? "Unknown";

            output.Append($"- **{schema}.{table}**");

            if (includeRowCount)
            {
                try
                {
                    var countQuery = $"SELECT COUNT(*) FROM [{schema}].[{table}]";
                    var countResult = await _dbHandler.ExecuteQueryAsync(countQuery, 1);
                    var count = countResult.Rows[0][0]?.ToString() ?? "0";
                    output.Append($" ({count} filas)");
                }
                catch
                {
                    // Ignorar errores de conteo
                }
            }

            output.AppendLine();
        }

        output.AppendLine("\n---\n");
        output.AppendLine($"💡 **Siguiente paso**: Usa `describe_table` para ver la estructura de la tabla que necesites.");

        return output.ToString();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Sanitiza el nombre de tabla para prevenir SQL injection básico.
    /// </summary>
    private string SanitizeTableName(string tableName)
    {
        // Remover caracteres peligrosos
        return tableName
            .Replace("'", "''")  // Escape single quotes
            .Replace(";", "")    // Remove semicolons
            .Replace("--", "")   // Remove comments
            .Replace("/*", "")   // Remove block comments
            .Replace("*/", "")
            .Trim();
    }

    private void LogDebug(string message)
    {
        if (_debugMode && _providerConfig.EnableQueryLogging)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[DEBUG] {message}");
            Console.ResetColor();
        }
    }

    private void LogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        Console.ResetColor();
    }

    private string TruncateForDisplay(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }

    #endregion
}
