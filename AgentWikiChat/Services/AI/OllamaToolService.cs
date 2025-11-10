using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentWikiChat.Models;
using Microsoft.Extensions.Configuration;

namespace AgentWikiChat.Services.AI;

/// <summary>
/// Servicio para interactuar con Ollama usando Function Calling (Tools).
/// Implementa la interfaz unificada IToolCallingService.
/// </summary>
public class OllamaToolService : IToolCallingService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly double _temperature;
    private readonly int _maxTokens;
    private readonly string _providerName;
    private readonly List<ToolDefinition> _tools = new();

    public OllamaToolService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        // Buscar el proveedor activo
        var activeProviderName = configuration["AI:ActiveProvider"] ?? "Ollama-Local";
        var providers = configuration.GetSection("AI:Providers").Get<List<AIProviderConfig>>();

        if (providers == null || !providers.Any())
            throw new InvalidOperationException("No hay proveedores configurados en AI:Providers");

        var provider = providers.FirstOrDefault(p => p.Name == activeProviderName);

        if (provider == null)
            throw new InvalidOperationException($"Proveedor '{activeProviderName}' no encontrado en configuración");

        _providerName = provider.Name;
        _baseUrl = provider.BaseUrl;
        _model = provider.Model;
        _temperature = provider.Temperature;
        _maxTokens = provider.MaxTokens;

        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(provider.TimeoutSeconds);
    }

    public void RegisterTool(ToolDefinition tool)
    {
        if (!_tools.Any(t => t.Function.Name == tool.Function.Name))
        {
            _tools.Add(tool);
        }
    }

    public void RegisterTools(IEnumerable<ToolDefinition> tools)
    {
        foreach (var tool in tools)
        {
            RegisterTool(tool);
        }
    }

    public IReadOnlyList<ToolDefinition> GetRegisteredTools() => _tools.AsReadOnly();

    /// <summary>
    /// Envía un mensaje con contexto y herramientas disponibles.
    /// Ollama decidirá si necesita invocar alguna herramienta.
    /// </summary>
    public async Task<ToolCallingResponse> SendMessageWithToolsAsync(
          string message,
          IEnumerable<Message> context,
          CancellationToken cancellationToken = default)
    {
        var messages = context.Select(m => new OllamaMessage
        {
            Role = m.Role,
            Content = m.Content
        }).ToList();

        messages.Add(new OllamaMessage { Role = "user", Content = message });

        var request = new OllamaChatRequest
        {
            Model = _model,
            Messages = messages,
            Tools = _tools,
            Stream = false,
            Options = new OllamaOptions
            {
                Temperature = _temperature,
                NumPredict = _maxTokens
            }
        };

        var response = await _httpClient.PostAsJsonAsync("/api/chat", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        // Leer como string primero para debugging
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            var result = JsonSerializer.Deserialize<OllamaChatResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
                throw new InvalidOperationException("Respuesta vacía de Ollama");

            // Convertir a formato unificado
            return new ToolCallingResponse
            {
                Content = result.Message.Content,
                ToolCalls = result.Message.ToolCalls,
                Role = result.Message.Role,
                Done = result.Done
            };
        }
        catch (JsonException ex)
        {
            // Si falla la deserialización, mostrar la respuesta para debug
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"? Error deserializando respuesta de Ollama:");
            Console.WriteLine($"Respuesta cruda: {responseContent}");
            Console.ResetColor();
            throw new InvalidOperationException($"Error deserializando respuesta de Ollama: {ex.Message}", ex);
        }
    }

    public string GetProviderName() => $"{_providerName} ({_model}) [Tools: {_tools.Count}]";
}

#region DTOs for Ollama API

internal class OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OllamaMessage> Messages { get; set; } = new();

    [JsonPropertyName("tools")]
    public List<ToolDefinition>? Tools { get; set; }

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("options")]
    public OllamaOptions? Options { get; set; }
}

internal class OllamaMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("tool_calls")]
    public List<ToolCall>? ToolCalls { get; set; }
}

internal class OllamaOptions
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("num_predict")]
    public int NumPredict { get; set; }
}

internal class OllamaChatResponse
{
    [JsonPropertyName("message")]
    public OllamaMessage Message { get; set; } = new();

    [JsonPropertyName("done")]
    public bool Done { get; set; }
}

#endregion
