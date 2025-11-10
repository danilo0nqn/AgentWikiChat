using Microsoft.Extensions.Configuration;

namespace AgentWikiChat.Services.Database;

/// <summary>
/// Factory para crear handlers de base de datos según configuración.
/// </summary>
public static class DatabaseHandlerFactory
{
    /// <summary>
    /// Crea el handler apropiado según el tipo de base de datos configurado.
    /// </summary>
    public static IDatabaseHandler CreateHandler(IConfiguration configuration)
    {
        var dbConfig = configuration.GetSection("Database");
        
        var providerType = dbConfig.GetValue<string>("Provider")?.ToLowerInvariant() 
            ?? throw new InvalidOperationException("Database:Provider no configurado en appsettings.json");

        var connectionString = dbConfig.GetValue<string>("ConnectionString")
            ?? throw new InvalidOperationException("Database:ConnectionString no configurada en appsettings.json");

        var commandTimeout = dbConfig.GetValue("CommandTimeout", 30);

        return providerType switch
        {
            "sqlserver" => new SqlServerDatabaseHandler(connectionString, commandTimeout),
            "postgresql" or "postgres" => new PostgreSqlDatabaseHandler(connectionString, commandTimeout),
            _ => throw new NotSupportedException(
                $"Proveedor de base de datos '{providerType}' no soportado. " +
                $"Opciones disponibles: {string.Join(", ", GetSupportedProviders())}")
        };
    }

    /// <summary>
    /// Obtiene la lista de proveedores soportados.
    /// </summary>
    public static string[] GetSupportedProviders()
    {
        return new[] { "sqlserver", "postgresql", "postgres" };
    }

    /// <summary>
    /// Verifica si un proveedor está soportado.
    /// </summary>
    public static bool IsProviderSupported(string provider)
    {
        return GetSupportedProviders().Contains(provider.ToLowerInvariant());
    }
}
