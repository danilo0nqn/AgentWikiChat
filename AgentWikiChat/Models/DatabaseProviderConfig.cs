namespace AgentWikiChat.Models;

/// <summary>
/// Configuración para un proveedor de base de datos.
/// Similar a AIProviderConfig, permite tener múltiples conexiones configuradas.
/// </summary>
public class DatabaseProviderConfig
{
    /// <summary>
    /// Nombre identificador del proveedor (ej: "SqlServer-Production", "PostgreSQL-Local").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de proveedor: SqlServer, PostgreSQL, MySQL, etc.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Cadena de conexión a la base de datos.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Timeout de comando en segundos.
    /// </summary>
    public int CommandTimeout { get; set; } = 30;

    /// <summary>
    /// Máximo número de filas a retornar en una consulta.
    /// </summary>
    public int MaxRowsToReturn { get; set; } = 1000;

    /// <summary>
    /// Habilitar logging de queries ejecutadas.
    /// </summary>
    public bool EnableQueryLogging { get; set; } = true;
}
