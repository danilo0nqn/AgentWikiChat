namespace AgentWikiChat.Services.VersionControl;

/// <summary>
/// Interfaz genérica para sistemas de control de versiones.
/// Define operaciones estándar que deben soportar todos los proveedores (SVN, Git, etc.)
/// </summary>
public interface IVersionControlHandler
{
    /// <summary>
    /// Nombre del proveedor (ej: "SVN", "Git")
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Verifica si el cliente del sistema de control de versiones está instalado
    /// </summary>
    bool IsClientInstalled();

    /// <summary>
    /// Obtiene la versión del cliente instalado
    /// </summary>
    string GetClientVersion();

    /// <summary>
    /// Prueba la conexión con el repositorio
    /// </summary>
    /// <returns>True si la conexión es exitosa</returns>
    Task<bool> TestConnectionAsync();

    /// <summary>
    /// Ejecuta una operación de solo lectura en el repositorio
    /// </summary>
    /// <param name="operation">Tipo de operación (log, info, list, etc.)</param>
    /// <param name="parameters">Parámetros adicionales (path, revision, limit, etc.)</param>
    /// <returns>Resultado de la operación</returns>
    Task<string> ExecuteReadOnlyOperationAsync(string operation, Dictionary<string, string> parameters);

    /// <summary>
    /// Valida si una operación es de solo lectura y está permitida
    /// </summary>
    /// <param name="operation">Operación a validar</param>
    /// <returns>True si la operación es válida</returns>
    bool IsOperationAllowed(string operation);

    /// <summary>
    /// Obtiene la lista de operaciones permitidas
    /// </summary>
    IEnumerable<string> GetAllowedOperations();

    /// <summary>
    /// Obtiene mensaje de ayuda para instalar el cliente
    /// </summary>
    string GetInstallationInstructions();

    /// <summary>
    /// Obtiene sugerencias según el tipo de error
    /// </summary>
    string GetErrorSuggestions(string errorMessage);
}
