using AgentWikiChat.Models;
using Microsoft.Extensions.Configuration;

namespace AgentWikiChat.Services.AI;

/// <summary>
/// Factory para crear el servicio de Tool Calling apropiado según configuración.
/// Soporta: Ollama, OpenAI, LM Studio, Anthropic Claude.
/// </summary>
public class ToolCallingServiceFactory
{
    /// <summary>
    /// Crea el servicio de Tool Calling según el proveedor activo en configuración.
    /// </summary>
    public static IToolCallingService CreateService(HttpClient httpClient, IConfiguration configuration)
    {
        var activeProviderName = configuration["AI:ActiveProvider"];
        if (string.IsNullOrWhiteSpace(activeProviderName))
            throw new InvalidOperationException("No se especificó ActiveProvider en configuración");

        var providers = configuration.GetSection("AI:Providers").Get<List<AIProviderConfig>>();
        if (providers == null || !providers.Any())
            throw new InvalidOperationException("No hay proveedores configurados en AI:Providers");

        var provider = providers.FirstOrDefault(p => p.Name == activeProviderName);
        if (provider == null)
            throw new InvalidOperationException($"Proveedor '{activeProviderName}' no encontrado");

        // Determinar el tipo de proveedor
        var providerType = provider.Type?.ToLowerInvariant() ?? "ollama";

        return providerType switch
        {
            "ollama" => new OllamaToolService(httpClient, configuration),
            "openai" => new OpenAIToolService(httpClient, configuration),
            "lmstudio" => new LMStudioToolService(httpClient, configuration),
            "anthropic" => new AnthropicToolService(httpClient, configuration),
            _ => throw new NotSupportedException($"Proveedor '{providerType}' no soportado. " +
                                                $"Opciones: {string.Join(", ", GetSupportedProviders())}")
        };
    }

    /// <summary>
    /// Obtiene una lista de proveedores soportados.
    /// </summary>
    public static string[] GetSupportedProviders()
    {
        return new[] { "ollama", "openai", "lmstudio", "anthropic" };
    }
}
