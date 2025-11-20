using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentWikiChat.Models;
using Microsoft.Extensions.Configuration;

namespace AgentWikiChat.Services.AI;

/// <summary>
/// Servicio para interactuar con Google Gemini usando Function Calling (Tools).
/// Implementa la interfaz unificada IToolCallingService.
/// Documentación: https://ai.google.dev/gemini-api/docs/function-calling
/// </summary>
public class GeminiToolService : IToolCallingService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly string _apiKey;
    private readonly double _temperature;
    private readonly int _maxTokens;
    private readonly string _providerName;
    private readonly List<ToolDefinition> _tools = new();

    public GeminiToolService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        // Buscar el proveedor activo
        var activeProviderName = configuration["AI:ActiveProvider"] ?? "Gemini-Pro";
        var providers = configuration.GetSection("AI:Providers").Get<List<AIProviderConfig>>();

        if (providers == null || !providers.Any())
            throw new InvalidOperationException("No hay proveedores configurados en AI:Providers");

        var provider = providers.FirstOrDefault(p => p.Name == activeProviderName);

        if (provider == null)
            throw new InvalidOperationException($"Proveedor '{activeProviderName}' no encontrado en configuración");

        _providerName = provider.Name;
        _baseUrl = provider.BaseUrl;
        _model = provider.Model;
        _apiKey = provider.ApiKey ?? throw new InvalidOperationException("ApiKey es requerida para Gemini");
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
    /// Gemini decidirá si necesita invocar alguna herramienta.
    /// </summary>
    public async Task<ToolCallingResponse> SendMessageWithToolsAsync(
        string message,
        IEnumerable<Message> context,
        CancellationToken cancellationToken = default)
    {
        // Convertir mensajes al formato de Gemini
        var contents = ConvertMessagesToGeminiContents(context, message);

        var request = new GeminiGenerateContentRequest
        {
            Contents = contents,
            Tools = _tools.Any() ? new List<GeminiTool>
            {
                new GeminiTool
                {
                    FunctionDeclarations = _tools.Select(ConvertToGeminiFunctionDeclaration).ToList()
                }
            } : null,
            GenerationConfig = new GeminiGenerationConfig
            {
                Temperature = _temperature,
                MaxOutputTokens = _maxTokens,
                TopP = 0.95,
                TopK = 40
            }
        };

        // Gemini API endpoint: POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}
        var endpoint = $"/v1beta/models/{_model}:generateContent?key={_apiKey}";

        var response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
        
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error en Gemini API:");
            Console.WriteLine($"Status: {response.StatusCode}");
            Console.WriteLine($"Respuesta: {responseContent}");
            Console.ResetColor();
            throw new InvalidOperationException($"Error en Gemini API: {response.StatusCode}");
        }

        try
        {
            var result = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null || result.Candidates == null || !result.Candidates.Any())
                throw new InvalidOperationException("Respuesta vacía de Gemini");

            var candidate = result.Candidates.First();
            var content = candidate.Content;

            // Verificar si hay function calls
            List<ToolCall>? toolCalls = null;
            string? textContent = null;

            if (content.Parts != null)
            {
                foreach (var part in content.Parts)
                {
                    // Si hay texto
                    if (!string.IsNullOrEmpty(part.Text))
                    {
                        textContent = part.Text;
                    }

                    // Si hay function call
                    if (part.FunctionCall != null)
                    {
                        toolCalls ??= new List<ToolCall>();
                        toolCalls.Add(new ToolCall
                        {
                            Id = Guid.NewGuid().ToString(), // Gemini no provee ID, generamos uno
                            Type = "function",
                            Function = new ToolCallFunction
                            {
                                Name = part.FunctionCall.Name,
                                Arguments = JsonSerializer.SerializeToElement(part.FunctionCall.Args ?? new Dictionary<string, object>())
                            }
                        });
                    }
                }
            }

            // 📊 AGREGAR MÉTRICAS DE TOKENS
            TokenUsage? tokenUsage = null;
            if (result.UsageMetadata != null)
            {
                tokenUsage = new TokenUsage
                {
                    PromptTokens = result.UsageMetadata.PromptTokenCount,
                    CompletionTokens = result.UsageMetadata.CandidatesTokenCount,
                    ModelName = _model,
                    Provider = "Gemini",
                    Timestamp = DateTime.Now,
                    EstimatedCost = TokenEstimator.EstimateCost(
                        "Gemini",
                        _model,
                        result.UsageMetadata.PromptTokenCount,
                        result.UsageMetadata.CandidatesTokenCount
                    )
                };
            }

            return new ToolCallingResponse
            {
                Content = textContent,
                ToolCalls = toolCalls,
                Role = "assistant",
                Done = true,
                TokenUsage = tokenUsage,  // ✅ NUEVO
                Metadata = new Dictionary<string, object>
                {
                    ["finish_reason"] = candidate.FinishReason ?? "STOP",
                    ["provider"] = "Gemini",
                    ["safety_ratings"] = candidate.SafetyRatings ?? new List<GeminiSafetyRating>()
                }
            };
        }
        catch (JsonException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error deserializando respuesta de Gemini:");
            Console.WriteLine($"Respuesta cruda: {responseContent}");
            Console.ResetColor();
            throw new InvalidOperationException($"Error deserializando respuesta de Gemini: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Convierte mensajes del formato unificado al formato de Gemini.
    /// Gemini agrupa mensajes por rol en "contents".
    /// </summary>
    private List<GeminiContent> ConvertMessagesToGeminiContents(IEnumerable<Message> context, string newMessage)
    {
        var contents = new List<GeminiContent>();

        // Procesar mensajes del contexto
        foreach (var msg in context)
        {
            // Gemini usa "user" y "model" (no "assistant")
            var role = msg.Role == "assistant" ? "model" : msg.Role;
            
            // Gemini no soporta rol "system" directamente, lo convertimos a "user"
            if (role == "system")
            {
                role = "user";
            }

            // Gemini no soporta rol "tool", lo convertimos a "model"
            if (role == "tool")
            {
                role = "model";
            }

            contents.Add(new GeminiContent
            {
                Role = role,
                Parts = new List<GeminiPart>
                {
                    new GeminiPart { Text = msg.Content }
                }
            });
        }

        // Agregar mensaje actual del usuario
        contents.Add(new GeminiContent
        {
            Role = "user",
            Parts = new List<GeminiPart>
            {
                new GeminiPart { Text = newMessage }
            }
        });

        return contents;
    }

    /// <summary>
    /// Convierte ToolDefinition (formato unificado) a formato de Gemini.
    /// </summary>
    private GeminiFunctionDeclaration ConvertToGeminiFunctionDeclaration(ToolDefinition tool)
    {
        return new GeminiFunctionDeclaration
        {
            Name = tool.Function.Name,
            Description = tool.Function.Description,
            Parameters = new GeminiSchema
            {
                Type = "object",
                Properties = tool.Function.Parameters.Properties.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new GeminiSchema
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

#region DTOs for Gemini API

internal class GeminiGenerateContentRequest
{
    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; set; } = new();

    [JsonPropertyName("tools")]
    public List<GeminiTool>? Tools { get; set; }

    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig? GenerationConfig { get; set; }
}

internal class GeminiContent
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = new();
}

internal class GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("functionCall")]
    public GeminiFunctionCall? FunctionCall { get; set; }

    [JsonPropertyName("functionResponse")]
    public GeminiFunctionResponse? FunctionResponse { get; set; }
}

internal class GeminiFunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("args")]
    public Dictionary<string, object>? Args { get; set; }
}

internal class GeminiFunctionResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("response")]
    public Dictionary<string, object>? Response { get; set; }
}

internal class GeminiTool
{
    [JsonPropertyName("functionDeclarations")]
    public List<GeminiFunctionDeclaration> FunctionDeclarations { get; set; } = new();
}

internal class GeminiFunctionDeclaration
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public GeminiSchema? Parameters { get; set; }
}

internal class GeminiSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("properties")]
    public Dictionary<string, GeminiSchema>? Properties { get; set; }

    [JsonPropertyName("required")]
    public List<string>? Required { get; set; }

    [JsonPropertyName("enum")]
    public List<string>? Enum { get; set; }
}

internal class GeminiGenerationConfig
{
    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }

    [JsonPropertyName("topK")]
    public int TopK { get; set; }

    [JsonPropertyName("topP")]
    public double TopP { get; set; }

    [JsonPropertyName("maxOutputTokens")]
    public int MaxOutputTokens { get; set; }
}

internal class GeminiGenerateContentResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate> Candidates { get; set; } = new();

    [JsonPropertyName("promptFeedback")]
    public GeminiPromptFeedback? PromptFeedback { get; set; }

    [JsonPropertyName("usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; set; }
}

internal class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent Content { get; set; } = new();

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }

    [JsonPropertyName("safetyRatings")]
    public List<GeminiSafetyRating>? SafetyRatings { get; set; }

    [JsonPropertyName("citationMetadata")]
    public GeminiCitationMetadata? CitationMetadata { get; set; }
}

internal class GeminiSafetyRating
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("probability")]
    public string Probability { get; set; } = string.Empty;
}

internal class GeminiCitationMetadata
{
    [JsonPropertyName("citations")]
    public List<GeminiCitation>? Citations { get; set; }
}

internal class GeminiCitation
{
    [JsonPropertyName("startIndex")]
    public int StartIndex { get; set; }

    [JsonPropertyName("endIndex")]
    public int EndIndex { get; set; }

    [JsonPropertyName("uri")]
    public string Uri { get; set; } = string.Empty;
}

internal class GeminiPromptFeedback
{
    [JsonPropertyName("blockReason")]
    public string? BlockReason { get; set; }

    [JsonPropertyName("safetyRatings")]
    public List<GeminiSafetyRating>? SafetyRatings { get; set; }
}

internal class GeminiUsageMetadata
{
    [JsonPropertyName("promptTokenCount")]
    public int PromptTokenCount { get; set; }

    [JsonPropertyName("candidatesTokenCount")]
    public int CandidatesTokenCount { get; set; }

    [JsonPropertyName("totalTokenCount")]
    public int TotalTokenCount { get; set; }
}

#endregion
