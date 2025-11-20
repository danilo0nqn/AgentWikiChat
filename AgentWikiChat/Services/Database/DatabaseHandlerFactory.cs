using AgentWikiChat.Models;
using Microsoft.Extensions.Configuration;

namespace AgentWikiChat.Services.Database;

/// <summary>
/// Factory para crear handlers de base de datos según configuración.
/// Ahora soporta múltiples proveedores configurados (patrón multi-provider).
/// </summary>
public static class DatabaseHandlerFactory
{
    /// <summary>
    /// Crea el handler apropiado según el proveedor activo configurado.
    /// </summary>
    public static IDatabaseHandler CreateHandler(IConfiguration configuration)
    {
        var dbSection = configuration.GetSection("Tools").GetSection("Database");
        
        var activeProvider = dbSection.GetValue<string>("ActiveProvider")
            ?? throw new InvalidOperationException("Database:ActiveProvider no configurado en appsettings.json");

        var providers = dbSection.GetSection("Providers").Get<List<DatabaseProviderConfig>>()
            ?? throw new InvalidOperationException("Database:Providers no configurado en appsettings.json");

        var providerConfig = providers.FirstOrDefault(p => p.Name == activeProvider)
            ?? throw new InvalidOperationException($"Proveedor de base de datos '{activeProvider}' no encontrado en Database:Providers");

        return CreateHandlerFromConfig(providerConfig);
    }

    /// <summary>
    /// Crea un handler a partir de la configuración de un proveedor específico.
    /// </summary>
    private static IDatabaseHandler CreateHandlerFromConfig(DatabaseProviderConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ConnectionString))
        {
            throw new InvalidOperationException($"ConnectionString no configurada para el proveedor '{config.Name}'");
        }

        return config.Type.ToLowerInvariant() switch
        {
            "sqlserver" => new SqlServerDatabaseHandler(config.ConnectionString, config.CommandTimeout),
            "postgresql" or "postgres" => new PostgreSqlDatabaseHandler(config.ConnectionString, config.CommandTimeout),
            _ => throw new NotSupportedException(
                $"Tipo de base de datos '{config.Type}' no soportado para el proveedor '{config.Name}'. " +
                $"Tipos disponibles: {string.Join(", ", GetSupportedTypes())}")
        };
    }

    /// <summary>
    /// Obtiene la lista de tipos de base de datos soportados.
    /// </summary>
    public static string[] GetSupportedTypes()
    {
        return new[] { "sqlserver", "postgresql", "postgres" };
    }

    /// <summary>
    /// Verifica si un tipo de base de datos está soportado.
    /// </summary>
    public static bool IsTypeSupported(string type)
    {
        return GetSupportedTypes().Contains(type.ToLowerInvariant());
    }

    /// <summary>
    /// Obtiene la configuración del proveedor activo.
    /// </summary>
    public static DatabaseProviderConfig GetActiveProviderConfig(IConfiguration configuration)
    {
        var dbSection = configuration.GetSection("Tools").GetSection("Database");
        
        var activeProvider = dbSection.GetValue<string>("ActiveProvider")
            ?? throw new InvalidOperationException("Database:ActiveProvider no configurado");

        var providers = dbSection.GetSection("Providers").Get<List<DatabaseProviderConfig>>()
            ?? throw new InvalidOperationException("Database:Providers no configurado");

        return providers.FirstOrDefault(p => p.Name == activeProvider)
            ?? throw new InvalidOperationException($"Proveedor '{activeProvider}' no encontrado");
    }
}
