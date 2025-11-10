using AgentWikiChat.Models;
using AgentWikiChat.Services.Database;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace AgentWikiChat.Services.Handlers;

/// <summary>
/// Handler para consultas a bases de datos (SQL Server, PostgreSQL, MySQL, etc.)
/// SEGURIDAD: Solo permite consultas SELECT. Bloqueado: UPDATE, DELETE, INSERT, EXEC, etc.
/// Soporta múltiples proveedores de bases de datos mediante IDatabaseHandler.
/// </summary>
public class DatabaseToolHandler : IToolHandler
{
    private readonly IDatabaseHandler _dbHandler;
    private readonly int _maxRowsToReturn;
    private readonly bool _enableLogging;
    private readonly bool _debugMode = true;

    public DatabaseToolHandler(IConfiguration configuration)
    {
        // Crear handler apropiado usando factory
        _dbHandler = DatabaseHandlerFactory.CreateHandler(configuration);

        var dbConfig = configuration.GetSection("Database");
        _maxRowsToReturn = dbConfig.GetValue("MaxRowsToReturn", 1000);
        _enableLogging = dbConfig.GetValue("EnableQueryLogging", true);

        LogDebug($"[Database] Inicializado - Provider: {_dbHandler.ProviderName}, MaxRows: {_maxRowsToReturn}");
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
                Description = $"Ejecuta consultas SELECT en la base de datos ({_dbHandler.ProviderName}). SOLO LECTURA - No permite modificaciones (INSERT, UPDATE, DELETE). Usa esta herramienta para obtener información de tablas, ejecutar queries, o generar reportes basados en datos.",
                Parameters = new FunctionParameters
                {
                    Type = "object",
                    Properties = new Dictionary<string, PropertyDefinition>
                    {
                        ["query"] = new PropertyDefinition
                        {
                            Type = "string",
                            Description = "Consulta SQL SELECT a ejecutar. IMPORTANTE: Solo SELECT permitido, sin subconsultas con modificaciones."
                        },
                        ["max_rows"] = new PropertyDefinition
                        {
                            Type = "string",
                            Description = $"Número máximo de filas a retornar (por defecto: {_maxRowsToReturn}). Usa valores menores para queries grandes."
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
        var maxRowsStr = parameters.GetString("max_rows", _maxRowsToReturn.ToString());

        if (string.IsNullOrWhiteSpace(query))
        {
            return "⚠️ Error: La consulta SQL no puede estar vacía.";
        }

        // Validar que el máximo de filas sea un número válido
        if (!int.TryParse(maxRowsStr, out var maxRows))
        {
            maxRows = _maxRowsToReturn;
        }

        // Limitar el máximo de filas al configurado
        if (maxRows > _maxRowsToReturn)
        {
            maxRows = _maxRowsToReturn;
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
            memory.AddToModule("database", "system", $"Query ejecutada: {TruncateForDisplay(query, 100)} - Rows: {result.RowCount}");

            // Formatear salida
            return FormatQueryResult(result, query);
        }
        catch (Exception ex)
        {
            LogError($"[Database] Error: {ex.Message}");
            return $"❌ **Error en {_dbHandler.ProviderName}**\n\n" +
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

        output.AppendLine($"📊 **Resultado de la Consulta ({_dbHandler.ProviderName})**\n");
        
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
        if (_debugMode && _enableLogging)
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
