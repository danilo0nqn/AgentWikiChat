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

        // Convertir mensajes al formato de Anthropic
        var conversationMessages = new List<AnthropicMessage>();

        foreach (var msg in context.Where(m => m.Role != "system"))
        {
            var anthropicMsg = ConvertToAnthropicMessage(msg);
            if (anthropicMsg != null)
            {
                conversationMessages.Add(anthropicMsg);
            }
        }

        // Agregar el mensaje actual del usuario
        conversationMessages.Add(new AnthropicMessage
        {
            Role = "user",
            Content = new object[]
            {
                new { type = "text", text = message }
            }
        });

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

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error de Anthropic API:");
            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine($"Response: {errorContent}");
            Console.ResetColor();
            throw new HttpRequestException($"Anthropic API error: {response.StatusCode} - {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            var result = JsonSerializer.Deserialize<AnthropicChatResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
                throw new InvalidOperationException("Respuesta vacía de Anthropic");

            // Extraer texto y tool_use de los content blocks
            var textContent = string.Empty;
            var toolCalls = new List<ToolCall>();

            foreach (var content in result.Content)
            {
                var contentElement = (JsonElement)content;
                var type = contentElement.GetProperty("type").GetString();

                if (type == "text")
                {
                    textContent = contentElement.GetProperty("text").GetString() ?? string.Empty;
                }
                else if (type == "tool_use")
                {
                    var toolCall = new ToolCall
                    {
                        Id = contentElement.GetProperty("id").GetString() ?? Guid.NewGuid().ToString(),
                        Type = "function",
                        Function = new ToolCallFunction
                        {
                            Name = contentElement.GetProperty("name").GetString() ?? string.Empty,
                            Arguments = contentElement.GetProperty("input")
                        }
                    };
                    toolCalls.Add(toolCall);
                }
            }

            // 📊 AGREGAR MÉTRICAS DE TOKENS
            TokenUsage? tokenUsage = null;
            if (result.Usage != null)
            {
                tokenUsage = new TokenUsage
                {
                    PromptTokens = result.Usage.InputTokens,
                    CompletionTokens = result.Usage.OutputTokens,
                    ModelName = _model,
                    Provider = "Anthropic",
                    Timestamp = DateTime.Now,
                    EstimatedCost = TokenEstimator.EstimateCost(
                        "Anthropic",
                        _model,
                        result.Usage.InputTokens,
                        result.Usage.OutputTokens
                    )
                };
            }

            return new ToolCallingResponse
            {
                Content = textContent,
                ToolCalls = toolCalls.Any() ? toolCalls : null,
                Role = "assistant",
                Done = result.StopReason == "end_turn" || result.StopReason == "tool_use",
                TokenUsage = tokenUsage,  // ← NUEVO
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
            Console.WriteLine($"❌ Error deserializando respuesta de Anthropic:");
            Console.WriteLine($"Respuesta cruda: {responseContent}");
            Console.ResetColor();
            throw new InvalidOperationException($"Error deserializando respuesta de Anthropic: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Convierte un mensaje interno al formato de Anthropic.
    /// </summary>
    private AnthropicMessage? ConvertToAnthropicMessage(Message msg)
    {
        // Mensajes de herramienta (tool results)
        if (msg.Role == "tool")
        {
            return new AnthropicMessage
            {
                Role = "user",
                Content = new object[]
                {
                    new
                    {
                        type = "tool_result",
                        tool_use_id = msg.ToolCallId,
                        content = msg.Content
                    }
                }
            };
        }

        // Mensajes del assistant con tool calls
        if (msg.Role == "assistant" && msg.ToolCalls != null && msg.ToolCalls.Any())
        {
            var contentBlocks = new List<object>();

            // Agregar texto si existe
            if (!string.IsNullOrEmpty(msg.Content))
            {
                contentBlocks.Add(new
                {
                    type = "text",
                    text = msg.Content
                });
            }

            // Agregar tool_use blocks
            foreach (var toolCall in msg.ToolCalls)
            {
                contentBlocks.Add(new
                {
                    type = "tool_use",
                    id = toolCall.Id,
                    name = toolCall.Function.Name,
                    input = JsonSerializer.Deserialize<Dictionary<string, object>>(
                        toolCall.Function.Arguments.GetRawText())
                });
            }

            return new AnthropicMessage
            {
                Role = "assistant",
                Content = contentBlocks.ToArray()
            };
        }

        // Mensajes regulares de usuario o assistant
        if (msg.Role == "user" || msg.Role == "assistant")
        {
            return new AnthropicMessage
            {
                Role = msg.Role,
                Content = new object[]
                {
                    new
                    {
                        type = "text",
                        text = msg.Content
                    }
                }
            };
        }

        return null;
    }

    /// <summary>
    /// Convierte ToolDefinition (formato unificado) a formato específico de Anthropic.
    /// </summary>
    private AnthropicTool ConvertToAnthropicTool(ToolDefinition tool)
    {
        // Convertir propiedades al formato de Anthropic
        var properties = new Dictionary<string, object>();

        foreach (var prop in tool.Function.Parameters.Properties)
        {
            var anthropicProp = new Dictionary<string, object>
            {
                ["type"] = prop.Value.Type,
                ["description"] = prop.Value.Description
            };

            // Solo agregar enum si tiene valores
            if (prop.Value.Enum != null && prop.Value.Enum.Any())
            {
                anthropicProp["enum"] = prop.Value.Enum;
            }

            properties[prop.Key] = anthropicProp;
        }

        return new AnthropicTool
        {
            Name = tool.Function.Name,
            Description = tool.Function.Description,
            InputSchema = new AnthropicInputSchema
            {
                Type = "object",
                Properties = properties,
                Required = tool.Function.Parameters.Required ?? new List<string>()
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
    public object[] Content { get; set; } = Array.Empty<object>();
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
    public Dictionary<string, object> Properties { get; set; } = new();

    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = new();
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
    public List<object> Content { get; set; } = new();

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }

    [JsonPropertyName("usage")]
    public AnthropicUsage? Usage { get; set; }
}

internal class AnthropicUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

#endregion