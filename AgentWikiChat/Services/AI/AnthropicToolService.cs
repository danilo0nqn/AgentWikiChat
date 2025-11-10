using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentWikiChat.Models;
using Microsoft.Extensions.Configuration;

namespace AgentWikiChat.Services.AI;

/// <summary>
/// Servicio para interactuar con Anthropic Claude usando Function Calling (Tools).
/// Implementa la interfaz unificada IToolCallingService.
/// </summary>
public class AnthropicToolService : IToolCallingService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly string _apiKey;
    private readonly double _temperature;
    private readonly int _maxTokens;
    private readonly string _providerName;
    private readonly List<ToolDefinition> _tools = new();

    public AnthropicToolService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        // Buscar el proveedor activo
        var activeProviderName = configuration["AI:ActiveProvider"] ?? "Anthropic-Claude";
        var providers = configuration.GetSection("AI:Providers").Get<List<AIProviderConfig>>();

        if (providers == null || !providers.Any())
            throw new InvalidOperationException("No hay proveedores configurados en AI:Providers");

        var provider = providers.FirstOrDefault(p => p.Name == activeProviderName);

        if (provider == null)
            throw new InvalidOperationException($"Proveedor '{activeProviderName}' no encontrado en configuración");

        if (string.IsNullOrWhiteSpace(provider.ApiKey))
            throw new InvalidOperationException("API Key de Anthropic no configurada");

        _providerName = provider.Name;
        _baseUrl = provider.BaseUrl;
        _model = provider.Model;
        _apiKey = provider.ApiKey;
        _temperature = provider.Temperature;
        _maxTokens = provider.MaxTokens;

        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
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
    /// Anthropic Claude decidirá si necesita invocar alguna herramienta.
    /// </summary>
    public async Task<ToolCallingResponse> SendMessageWithToolsAsync(
        string message,
        IEnumerable<Message> context,
        CancellationToken cancellationToken = default)
    {
        // Anthropic requiere separar el system message
        var systemMessage = context.FirstOrDefault(m => m.Role == "system")?.Content ?? string.Empty;
        var conversationMessages = context.Where(m => m.Role != "system").Select(m => new AnthropicMessage
        {
            Role = m.Role == "assistant" ? "assistant" : "user",
            Content = m.Content
        }).ToList();

        conversationMessages.Add(new AnthropicMessage { Role = "user", Content = message });

        var request = new AnthropicChatRequest
        {
            Model = _model,
            MaxTokens = _maxTokens,
            Temperature = _temperature,
            System = systemMessage,
            Messages = conversationMessages,
            Tools = _tools.Select(ConvertToAnthropicTool).ToList()
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/messages", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            var result = JsonSerializer.Deserialize<AnthropicChatResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
                throw new InvalidOperationException("Respuesta vacía de Anthropic");

            // Anthropic puede retornar múltiples content blocks
            var textContent = result.Content
                     .Where(c => c.Type == "text")
         .Select(c => c.Text)
            .FirstOrDefault();

            var toolUseBlocks = result.Content
  .Where(c => c.Type == "tool_use")
                .ToList();

            // Convertir tool_use blocks a formato unificado
            List<ToolCall>? toolCalls = null;
            if (toolUseBlocks.Any())
            {
                toolCalls = toolUseBlocks.Select(tb => new ToolCall
                {
                    Id = tb.Id ?? Guid.NewGuid().ToString(),
                    Type = "function",
                    Function = new ToolCallFunction
                    {
                        Name = tb.Name ?? string.Empty,
                        Arguments = JsonSerializer.SerializeToElement(tb.Input ?? new object())
                    }
                }).ToList();
            }

            return new ToolCallingResponse
            {
                Content = textContent,
                ToolCalls = toolCalls,
                Role = "assistant",
                Done = result.StopReason == "end_turn" || result.StopReason == "tool_use",
                Metadata = new Dictionary<string, object>
                {
                    ["stop_reason"] = result.StopReason ?? "end_turn",
                    ["usage"] = result.Usage ?? new AnthropicUsage()
                }
            };
        }
        catch (JsonException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"? Error deserializando respuesta de Anthropic:");
            Console.WriteLine($"Respuesta cruda: {responseContent}");
            Console.ResetColor();
            throw new InvalidOperationException($"Error deserializando respuesta de Anthropic: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Convierte ToolDefinition (formato unificado) a formato específico de Anthropic.
    /// </summary>
    private AnthropicTool ConvertToAnthropicTool(ToolDefinition tool)
    {
        return new AnthropicTool
        {
            Name = tool.Function.Name,
            Description = tool.Function.Description,
            InputSchema = new AnthropicInputSchema
            {
                Type = "object",
                Properties = tool.Function.Parameters.Properties.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new AnthropicProperty
                    {
                        Type = kvp.Value.Type,
                        Description = kvp.Value.Description,
                        Enum = kvp.Value.Enum
                    }
                ),
                Required = tool.Function.Parameters.Required
            }
        };
    }

    public string GetProviderName() => $"{_providerName} ({_model}) [Tools: {_tools.Count}]";
}

#region DTOs for Anthropic API

internal class AnthropicChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("system")]
    public string? System { get; set; }

    [JsonPropertyName("messages")]
    public List<AnthropicMessage> Messages { get; set; } = new();

    [JsonPropertyName("tools")]
    public List<AnthropicTool>? Tools { get; set; }
}

internal class AnthropicMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

internal class AnthropicTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("input_schema")]
    public AnthropicInputSchema InputSchema { get; set; } = new();
}

internal class AnthropicInputSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, AnthropicProperty> Properties { get; set; } = new();

    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = new();
}

internal class AnthropicProperty
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("enum")]
    public List<string>? Enum { get; set; }
}

internal class AnthropicChatResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public List<AnthropicContentBlock> Content { get; set; } = new();

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }

    [JsonPropertyName("usage")]
    public AnthropicUsage? Usage { get; set; }
}

internal class AnthropicContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("input")]
    public object? Input { get; set; }
}

internal class AnthropicUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

#endregion
