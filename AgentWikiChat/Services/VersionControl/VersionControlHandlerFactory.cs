using AgentWikiChat.Models;
using Microsoft.Extensions.Configuration;

namespace AgentWikiChat.Services.VersionControl;

/// <summary>
/// Factory para crear instancias de handlers de sistemas de control de versiones.
/// Similar a DatabaseHandlerFactory, permite tener múltiples repositorios configurados (patrón multi-provider).
/// </summary>
public static class VersionControlHandlerFactory
{
    /// <summary>
    /// Crea un handler de sistema de control de versiones según el proveedor activo configurado.
    /// </summary>
    /// <param name="configuration">Configuración de la aplicación</param>
    /// <returns>Instancia del handler apropiado</returns>
    public static IVersionControlHandler CreateHandler(IConfiguration configuration)
    {
        var repoSection = configuration.GetSection("Tools").GetSection("Repository");
        
        var activeProvider = repoSection.GetValue<string>("ActiveProvider")
            ?? throw new InvalidOperationException("Repository:ActiveProvider no configurado en appsettings.json");

        var providers = repoSection.GetSection("Providers").Get<List<RepositoryProviderConfig>>()
            ?? throw new InvalidOperationException("Repository:Providers no configurado en appsettings.json");

        var providerConfig = providers.FirstOrDefault(p => p.Name == activeProvider)
            ?? throw new InvalidOperationException($"Proveedor de repositorio '{activeProvider}' no encontrado en Repository:Providers");

        return CreateHandlerFromConfig(providerConfig, configuration);
    }

    /// <summary>
    /// Crea un handler a partir de la configuración de un proveedor específico.
    /// </summary>
    private static IVersionControlHandler CreateHandlerFromConfig(RepositoryProviderConfig config, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(config.RepositoryUrl) && config.Type.ToLowerInvariant() != "git")
        {
            throw new InvalidOperationException($"RepositoryUrl no configurada para el proveedor '{config.Name}'");
        }

        // Crear una configuración temporal con los valores del proveedor específico
        var tempConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Repository:Provider"] = config.Type,
                ["Repository:RepositoryUrl"] = config.RepositoryUrl,
                ["Repository:Username"] = config.Username,
                ["Repository:Password"] = config.Password,
                ["Repository:WorkingCopyPath"] = config.WorkingCopyPath,
                ["Repository:CommandTimeout"] = config.CommandTimeout.ToString(),
                ["Repository:EnableLogging"] = config.EnableLogging.ToString(),
                ["Ui:Debug"] = configuration["Ui:Debug"] ?? "false"
            })
            .Build();

        return config.Type.ToLowerInvariant() switch
        {
            "svn" or "subversion" => new SvnVersionControlHandler(tempConfig),
            "git" => new GitVersionControlHandler(tempConfig),
            "github" => new GitHubVersionControlHandler(tempConfig),
            // Futuro: "mercurial" or "hg" => new MercurialVersionControlHandler(tempConfig),
            // Futuro: "tfs" or "tfvc" => new TfsVersionControlHandler(tempConfig),
            // Futuro: "perforce" or "p4" => new PerforceVersionControlHandler(tempConfig),
            // Futuro: "gitlab" => new GitLabVersionControlHandler(tempConfig),
            // Futuro: "bitbucket" => new BitbucketVersionControlHandler(tempConfig),
            _ => throw new NotSupportedException(
                $"Tipo de control de versiones '{config.Type}' no soportado para el proveedor '{config.Name}'. " +
                $"Tipos válidos: {string.Join(", ", GetSupportedTypes())}")
        };
    }

    /// <summary>
    /// Obtiene la lista de tipos de control de versiones soportados.
    /// </summary>
    public static IEnumerable<string> GetSupportedTypes()
    {
        return new[] { "SVN", "Git", "GitHub" };
        // Futuro: return new[] { "SVN", "Git", "GitHub", "GitLab", "Bitbucket", "Mercurial", "TFS", "Perforce" };
    }

    /// <summary>
    /// Verifica si un tipo de control de versiones está soportado.
    /// </summary>
    public static bool IsTypeSupported(string type)
    {
        return GetSupportedTypes()
            .Select(t => t.ToLowerInvariant())
            .Contains(type.ToLowerInvariant());
    }

    /// <summary>
    /// Obtiene la configuración del proveedor activo.
    /// </summary>
    public static RepositoryProviderConfig GetActiveProviderConfig(IConfiguration configuration)
    {
        var repoSection = configuration.GetSection("Tools").GetSection("Repository");
        
        var activeProvider = repoSection.GetValue<string>("ActiveProvider")
            ?? throw new InvalidOperationException("Repository:ActiveProvider no configurado");

        var providers = repoSection.GetSection("Providers").Get<List<RepositoryProviderConfig>>()
            ?? throw new InvalidOperationException("Repository:Providers no configurado");

        return providers.FirstOrDefault(p => p.Name == activeProvider)
            ?? throw new InvalidOperationException($"Proveedor '{activeProvider}' no encontrado");
    }
}
