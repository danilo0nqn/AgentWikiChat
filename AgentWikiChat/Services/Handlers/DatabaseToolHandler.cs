using AgentWikiChat.Models;
using AgentWikiChat.Services.Database;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace AgentWikiChat.Services.Handlers;

/// <summary>
/// Handler para consultas a bases de datos (SQL Server, PostgreSQL, MySQL, etc.)
/// SEGURIDAD: Solo permite consultas SELECT. Bloqueado: UPDATE, DELETE, INSERT, EXEC, etc.
/// Soporta múltiples proveedores de bases de datos mediante IDatabaseHandler con patrón multi-provider.
/// </summary>
public class DatabaseToolHandler : IToolHandler
{
    private readonly IDatabaseHandler _dbHandler;
    private readonly DatabaseProviderConfig _providerConfig;
    private readonly bool _debugMode = true;

    public DatabaseToolHandler(IConfiguration configuration)
    {
        // Crear handler apropiado usando factory (multi-provider)
        _dbHandler = DatabaseHandlerFactory.CreateHandler(configuration);

        // Obtener configuración del proveedor activo
        _providerConfig = DatabaseHandlerFactory.GetActiveProviderConfig(configuration);

        LogDebug($"[Database] Inicializado - Provider: {_providerConfig.Name} ({_dbHandler.ProviderName}), MaxRows: {_providerConfig.MaxRowsToReturn}");
    }

    public string ToolName => "query_database";

    public ToolDefinition GetToolDefinition()
    {
        return new ToolDefinition
        {
            Type = "function",
            Function = new FunctionDefinition
            {
                Name = ToolName,
                Description = $"Ejecuta consultas SELECT en base de datos {_providerConfig.Name}. SOLO LECTURA (no INSERT/UPDATE/DELETE). Si no conocés nombres de tablas/columnas, usa explore_database_schema PRIMERO.",
                Parameters = new FunctionParameters
                {
                    Type = "object",
                    Properties = new Dictionary<string, PropertyDefinition>
                    {
                        ["query"] = new PropertyDefinition
                        {
                            Type = "string",
                            Description = "Consulta SQL SELECT a ejecutar. Solo SELECT permitido."
                        },
                        ["max_rows"] = new PropertyDefinition
                        {
                            Type = "string",
                            Description = $"Máximo de filas a retornar (por defecto: {_providerConfig.MaxRowsToReturn})"
                        }
                    },
                    Required = new List<string> { "query" }
                }
            }
        };
    }

    public async Task<string> HandleAsync(ToolParameters parameters, MemoryService memory)
    {
        var query = parameters.GetString("query");
        var maxRowsStr = parameters.GetString("max_rows", _providerConfig.MaxRowsToReturn.ToString());

        if (string.IsNullOrWhiteSpace(query))
        {
            return "⚠️ Error: La consulta SQL no puede estar vacía.";
        }

        // Validar que el máximo de filas sea un número válido
        if (!int.TryParse(maxRowsStr, out var maxRows))
        {
            maxRows = _providerConfig.MaxRowsToReturn;
        }

        // Limitar el máximo de filas al configurado
        if (maxRows > _providerConfig.MaxRowsToReturn)
        {
            maxRows = _providerConfig.MaxRowsToReturn;
        }

        LogDebug($"[Database] Query recibida: {TruncateForDisplay(query, 200)}");
        LogDebug($"[Database] Max rows: {maxRows}");

        // 🔒 VALIDACIÓN DE SEGURIDAD
        var validationResult = _dbHandler.ValidateQuery(query);
        if (!validationResult.IsValid)
        {
            LogError($"[Database] Consulta rechazada: {validationResult.ErrorMessage}");
            return $"🔒 **Consulta Rechazada por Seguridad**\n\n{validationResult.ErrorMessage}\n\n" +
                   $"💡 **Recuerda**: Solo se permiten consultas SELECT de solo lectura.";
        }

        try
        {
            // Ejecutar consulta
            var result = await _dbHandler.ExecuteQueryAsync(query, maxRows);

            // Guardar en memoria modular
            memory.AddToModule("database", "system", $"Query ejecutada en {_providerConfig.Name}: {TruncateForDisplay(query, 100)} - Rows: {result.RowCount}");

            // Formatear salida
            return FormatQueryResult(result, query);
        }
        catch (Exception ex)
        {
            LogError($"[Database] Error: {ex.Message}");
            return $"❌ **Error en {_providerConfig.Name} ({_dbHandler.ProviderName})**\n\n" +
                   $"**Mensaje**: {ex.Message}\n\n" +
                   $"💡 Verifica la sintaxis de tu consulta SQL y que la tabla/columna exista.";
        }
    }

    #region Formatting

    /// <summary>
    /// Formatea el resultado de la consulta para presentación al usuario.
    /// </summary>
    private string FormatQueryResult(QueryResult result, string query)
    {
        var output = new StringBuilder();

        output.AppendLine($"📊 **Resultado de la Consulta**\n");
        output.AppendLine($"**Proveedor**: {_providerConfig.Name} ({_dbHandler.ProviderName})");

        // Mostrar query ejecutada (truncada)
        output.AppendLine($"**Query**: `{TruncateForDisplay(query, 200)}`");
        output.AppendLine($"**Filas retornadas**: {result.RowCount}");
        output.AppendLine($"**Tiempo de ejecución**: {result.ExecutionTimeMs:F2}ms\n");

        if (result.RowCount == 0)
        {
            output.AppendLine("ℹ️ No se encontraron resultados.");
            return output.ToString();
        }

        // Formatear tabla
        output.AppendLine("**Resultados:**\n");

        // Encabezados
        output.Append("| ");
        output.Append(string.Join(" | ", result.ColumnNames));
        output.AppendLine(" |");

        // Separador
        output.Append("| ");
        output.Append(string.Join(" | ", result.ColumnNames.Select(_ => "---")));
        output.AppendLine(" |");

        // Datos (limitar a primeras 50 filas para no saturar)
        var rowsToShow = Math.Min(result.RowCount, 50);
        for (int i = 0; i < rowsToShow; i++)
        {
            output.Append("| ");
            var formattedValues = result.Rows[i].Select(v =>
                v == null ? "*NULL*" : TruncateForDisplay(v.ToString() ?? "", 50)
            );
            output.Append(string.Join(" | ", formattedValues));
            output.AppendLine(" |");
        }

        if (result.RowCount > rowsToShow)
        {
            output.AppendLine($"\n⚠️ *Mostrando {rowsToShow} de {result.RowCount} filas. Usa `max_rows` para ajustar.*");
        }

        return output.ToString();
    }

    #endregion

    #region Logging Helpers

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
