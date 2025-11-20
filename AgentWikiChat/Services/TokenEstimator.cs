namespace AgentWikiChat.Services;

/// <summary>
/// Estimador de tokens para cuando el proveedor no devuelve métricas exactas.
/// Usa heurísticas simples (no es perfecto pero da una aproximación útil).
/// </summary>
public static class TokenEstimator
{
    /// <summary>
    /// Estima tokens basándose en conteo de palabras.
    /// Regla aproximada: 1 token ≈ 4 caracteres o ≈ 0.75 palabras (para inglés/español).
    /// </summary>
    public static int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        // Método 1: Basado en caracteres (más simple)
        var charCount = text.Length;
        var tokensByChars = (int)Math.Ceiling(charCount / 4.0);

        // Método 2: Basado en palabras (más preciso para texto natural)
        var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var tokensByWords = (int)Math.Ceiling(words.Length / 0.75);

        // Usar el promedio de ambos métodos
        return (tokensByChars + tokensByWords) / 2;
    }

    /// <summary>
    /// Estima tokens de una lista de mensajes.
    /// </summary>
    public static int EstimateTokensFromMessages(IEnumerable<AgentWikiChat.Models.Message> messages)
    {
        var totalTokens = 0;

        foreach (var message in messages)
        {
            // Contenido del mensaje
            if (!string.IsNullOrEmpty(message.Content))
            {
                totalTokens += EstimateTokens(message.Content);
            }

            // Si tiene tool calls, agregar overhead
            if (message.ToolCalls != null && message.ToolCalls.Any())
            {
                foreach (var toolCall in message.ToolCalls)
                {
                    // Nombre de función + argumentos
                    totalTokens += EstimateTokens(toolCall.Function.Name);
                    totalTokens += EstimateTokens(toolCall.Function.GetArgumentsAsString());
                    totalTokens += 10; // Overhead de estructura JSON
                }
            }

            // Overhead por mensaje (role, timestamp, etc.)
            totalTokens += 4;
        }

        return totalTokens;
    }

    /// <summary>
    /// Estima el costo aproximado para un proveedor dado.
    /// Precios aproximados a 2025 (pueden variar).
    /// </summary>
    public static decimal? EstimateCost(string provider, string model, int promptTokens, int completionTokens)
    {
        // Pricing aproximado (USD por 1M tokens)
        var pricing = GetPricing(provider, model);
        if (pricing == null)
            return null;

        var (inputPrice, outputPrice) = pricing.Value;

        var promptCost = (promptTokens / 1_000_000.0m) * inputPrice;
        var completionCost = (completionTokens / 1_000_000.0m) * outputPrice;

        return promptCost + completionCost;
    }

    /// <summary>
    /// Obtiene el pricing para un modelo específico (USD por 1M tokens).
    /// Returns: (inputPrice, outputPrice)
    /// </summary>
    private static (decimal inputPrice, decimal outputPrice)? GetPricing(string provider, string model)
    {
        var providerLower = provider?.ToLowerInvariant() ?? "";
        var modelLower = model?.ToLowerInvariant() ?? "";

        // OpenAI pricing (aproximado)
        if (providerLower.Contains("openai"))
        {
            if (modelLower.Contains("gpt-4-turbo") || modelLower.Contains("gpt-4-1106"))
                return (10.00m, 30.00m); // GPT-4 Turbo

            if (modelLower.Contains("gpt-4"))
                return (30.00m, 60.00m); // GPT-4

            if (modelLower.Contains("gpt-3.5-turbo"))
                return (0.50m, 1.50m); // GPT-3.5 Turbo

            if (modelLower.Contains("gpt-4o"))
                return (2.50m, 10.00m); // GPT-4o (optimized)
        }

        // Anthropic Claude pricing (aproximado)
        if (providerLower.Contains("anthropic") || providerLower.Contains("claude"))
        {
            if (modelLower.Contains("opus"))
                return (15.00m, 75.00m); // Claude 3 Opus

            if (modelLower.Contains("sonnet") || modelLower.Contains("4-5"))
                return (3.00m, 15.00m); // Claude 3/3.5/4 Sonnet

            if (modelLower.Contains("haiku"))
                return (0.25m, 1.25m); // Claude 3 Haiku
        }

        // Google Gemini pricing (aproximado)
        if (providerLower.Contains("gemini") || providerLower.Contains("google"))
        {
            if (modelLower.Contains("pro") || modelLower.Contains("2.5-pro") || modelLower.Contains("2-5-pro"))
                return (0.50m, 1.50m); // Gemini Pro

            if (modelLower.Contains("flash") || modelLower.Contains("2.5-flash") || modelLower.Contains("2-5-flash"))
                return (0.10m, 0.30m); // Gemini Flash

            if (modelLower.Contains("ultra"))
                return (5.00m, 15.00m); // Gemini Ultra (estimado)
        }

        // Ollama y LM Studio son locales (sin costo)
        if (providerLower.Contains("ollama") || providerLower.Contains("lmstudio") || providerLower.Contains("lm studio"))
        {
            return (0.00m, 0.00m); // Local, sin costo
        }

        // Proveedor desconocido
        return null;
    }

    /// <summary>
    /// Formatea un costo en USD.
    /// </summary>
    public static string FormatCost(decimal cost)
    {
        if (cost < 0.000001m)
            return "$0.00";

        if (cost < 0.01m)
            return $"${cost:F6}";

        return $"${cost:F4}";
    }
}
