namespace AgentWikiChat.Models;

/// <summary>
/// Configuración para un proveedor de control de versiones.
/// Similar a AIProviderConfig, permite tener múltiples repositorios configurados.
/// </summary>
public class RepositoryProviderConfig
{
    /// <summary>
    /// Nombre identificador del proveedor (ej: "GitHub-AgentWikiChat", "SVN-Production").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de proveedor: SVN, Git, GitHub, GitLab, Bitbucket, etc.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// URL del repositorio.
    /// </summary>
    public string RepositoryUrl { get; set; } = string.Empty;

    /// <summary>
    /// Nombre de usuario para autenticación.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Contraseña o token de acceso para autenticación.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Ruta local del working copy (obligatorio para Git, opcional para SVN).
    /// </summary>
    public string WorkingCopyPath { get; set; } = string.Empty;

    /// <summary>
    /// Timeout de comando en segundos.
    /// </summary>
    public int CommandTimeout { get; set; } = 60;

    /// <summary>
    /// Habilitar logging de operaciones ejecutadas.
    /// </summary>
    public bool EnableLogging { get; set; } = true;
}
