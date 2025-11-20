using System.Data;

namespace AgentWikiChat.Services.Database;

/// <summary>
/// Interfaz para handlers de bases de datos.
/// Permite soportar múltiples proveedores (SQL Server, PostgreSQL, MySQL, etc.)
/// </summary>
public interface IDatabaseHandler
{
    /// <summary>
    /// Nombre del proveedor (ej: "SQL Server", "PostgreSQL")
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Valida que una consulta SQL sea segura (solo SELECT).
    /// </summary>
    /// <param name="query">Consulta SQL a validar</param>
    /// <returns>Resultado de validación con mensaje de error si aplica</returns>
    ValidationResult ValidateQuery(string query);

    /// <summary>
    /// Ejecuta una consulta SELECT de manera segura.
    /// </summary>
    /// <param name="query">Consulta SQL SELECT</param>
    /// <param name="maxRows">Máximo de filas a retornar</param>
    /// <returns>Resultado de la consulta con datos y metadata</returns>
    Task<QueryResult> ExecuteQueryAsync(string query, int maxRows);

    /// <summary>
    /// Obtiene la lista de tablas disponibles en la base de datos.
    /// </summary>
    Task<List<string>> GetTablesAsync();

    /// <summary>
    /// Obtiene el esquema de una tabla específica.
    /// </summary>
    Task<List<ColumnInfo>> GetTableSchemaAsync(string tableName);
}

/// <summary>
/// Resultado de validación de consulta.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public static ValidationResult Success() => new ValidationResult { IsValid = true };
    public static ValidationResult Fail(string message) => new ValidationResult { IsValid = false, ErrorMessage = message };
}

/// <summary>
/// Resultado de ejecución de consulta.
/// </summary>
public class QueryResult
{
    public List<string> ColumnNames { get; set; } = new();
    public List<List<object?>> Rows { get; set; } = new();
    public int RowCount { get; set; }
    public double ExecutionTimeMs { get; set; }
    public string FormattedOutput { get; set; } = string.Empty;
}

/// <summary>
/// Información de columna de una tabla.
/// </summary>
public class ColumnInfo
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
    public bool IsPrimaryKey { get; set; }
}
