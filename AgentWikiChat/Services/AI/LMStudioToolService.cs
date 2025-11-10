using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentWikiChat.Models;
using Microsoft.Extensions.Configuration;

namespace AgentWikiChat.Services.AI;

/// <summary>
/// Servicio para interactuar con LM Studio usando Function Calling.
/// LM Studio usa una API compatible con OpenAI, por lo que reutiliza esa estructura.
/// Implementa la interfaz unificada IToolCallingService.
/// </summary>
public class LMStudioToolService : IToolCallingService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly double _temperature;
    private readonly int _maxTokens;
    private readonly string _providerName;
    private readonly List<ToolDefinition> _tools = new();

    public LMStudioToolService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        // Buscar el proveedor activo
        var activeProviderName = configuration["AI:ActiveProvider"] ?? "LMStudio-Local";
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
    /// LM Studio usa API compatible con OpenAI.
    /// </summary>
    public async Task<ToolCallingResponse> SendMessageWithToolsAsync(
        string message,
      IEnumerable<Message> context,
        CancellationToken cancellationToken = default)
    {
        var messages = context.Select(m => new LMStudioMessage
        {
            Role = m.Role,
            Content = m.Content
        }).ToList();

        messages.Add(new LMStudioMessage { Role = "user", Content = message });

        var request = new LMStudioChatRequest
        {
            Model = _model,
            Messages = messages,
            Tools = _tools.Select(ConvertToLMStudioTool).ToList(),
            Temperature = _temperature,
            MaxTokens = _maxTokens
        };

        // LM Studio usa el mismo endpoint que OpenAI
        var response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            var result = JsonSerializer.Deserialize<LMStudioChatResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null || result.Choices == null || !result.Choices.Any())
                throw new InvalidOperationException("Respuesta vacía de LM Studio");

            var choice = result.Choices.First();
            var responseMessage = choice.Message;

            // Convertir tool_calls a formato unificado
            List<ToolCall>? toolCalls = null;
            if (responseMessage.ToolCalls != null && responseMessage.ToolCalls.Any())
            {
                toolCalls = responseMessage.ToolCalls.Select(tc => new ToolCall
                {
                    Id = tc.Id,
                    Type = tc.Type,
                    Function = new ToolCallFunction
                    {
                        Name = tc.Function.Name,
                        Arguments = JsonSerializer.SerializeToElement(tc.Function.Arguments)
                    }
                }).ToList();
            }

            return new ToolCallingResponse
            {
                Content = responseMessage.Content,
                ToolCalls = toolCalls,
                Role = responseMessage.Role,
                Done = true,
                Metadata = new Dictionary<string, object>
                {
                    ["finish_reason"] = choice.FinishReason ?? "stop",
                    ["provider"] = "LM Studio"
                }
            };
        }
        catch (JsonException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"? Error deserializando respuesta de LM Studio:");
            Console.WriteLine($"Respuesta cruda: {responseContent}");
            Console.ResetColor();
            throw new InvalidOperationException($"Error deserializando respuesta de LM Studio: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Convierte ToolDefinition (formato unificado) a formato compatible con LM Studio (OpenAI-like).
    /// </summary>
    private LMStudioTool ConvertToLMStudioTool(ToolDefinition tool)
    {
        return new LMStudioTool
        {
            Type = "function",
            Function = new LMStudioFunction
            {
                Name = tool.Function.Name,
                Description = tool.Function.Description,
                Parameters = new LMStudioFunctionParameters
                {
                    Type = "object",
                    Properties = tool.Function.Parameters.Properties.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new LMStudioProperty
                        {
                            Type = kvp.Value.Type,
                            Description = kvp.Value.Description,
                            Enum = kvp.Value.Enum
                        }
                    ),
                    Required = tool.Function.Parameters.Required
                }
            }
        };
    }

    public string GetProviderName() => $"{_providerName} ({_model}) [Tools: {_tools.Count}]";
}

#region DTOs for LM Studio API (OpenAI-compatible)

internal class LMStudioChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<LMStudioMessage> Messages { get; set; } = new();

    [JsonPropertyName("tools")]
    public List<LMStudioTool>? Tools { get; set; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }
}

internal class LMStudioMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<LMStudioToolCall>? ToolCalls { get; set; }
}

internal class LMStudioTool
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public LMStudioFunction Function { get; set; } = new();
}

internal class LMStudioFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public LMStudioFunctionParameters Parameters { get; set; } = new();
}

internal class LMStudioFunctionParameters
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, LMStudioProperty> Properties { get; set; } = new();

    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = new();
}

internal class LMStudioProperty
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("enum")]
    public List<string>? Enum { get; set; }
}

internal class LMStudioToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public LMStudioToolCallFunction Function { get; set; } = new();
}

internal class LMStudioToolCallFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = string.Empty;
}

internal class LMStudioChatResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<LMStudioChoice> Choices { get; set; } = new();
}

internal class LMStudioChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public LMStudioMessage Message { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

#endregion
