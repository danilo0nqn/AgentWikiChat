using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentWikiChat.Models;

namespace AgentWikiChat.Services.Handlers;

/// <summary>
/// Handler unificado para todas las operaciones de Wikipedia.
/// Expone múltiples herramientas: búsqueda de títulos y obtención de artículos.
/// Arquitectura modular con código compartido.
/// </summary>
public class WikipediaHandler : IToolHandler
{
    private readonly HttpClient _httpClient;
    private const int MAX_RESULTS = 5;
    private readonly bool _debugMode = true;

    public WikipediaHandler()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
     _httpClient.DefaultRequestHeaders.Add("User-Agent", "AgentWikiChat/3.3 (.NET 9)");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    // Esta propiedad no se usa cuando hay múltiples tools, pero la mantenemos por compatibilidad
    public string ToolName => "wikipedia";

 public ToolDefinition GetToolDefinition()
    {
        // Por defecto devuelve la tool de búsqueda
     return GetSearchToolDefinition();
    }

    // Método adicional para obtener todas las tools
    public List<ToolDefinition> GetAllToolDefinitions()
    {
        return new List<ToolDefinition>
        {
    GetSearchToolDefinition(),
      GetArticleToolDefinition()
        };
    }

    private ToolDefinition GetSearchToolDefinition()
    {
        return new ToolDefinition
     {
            Type = "function",
 Function = new FunctionDefinition
          {
Name = "search_wikipedia_titles",
    Description = "Busca títulos de artículos en Wikipedia. Usa esto PRIMERO.",
        Parameters = new FunctionParameters
              {
       Type = "object",
           Properties = new Dictionary<string, PropertyDefinition>
            {
["query"] = new PropertyDefinition
   {
      Type = "string",
   Description = "Término a buscar"
   },
        ["language"] = new PropertyDefinition
                 {
          Type = "string",
                 Description = "Idioma (es, en, etc.)"
        }
                    },
        Required = new List<string> { "query" }
      }
            }
        };
    }

    private ToolDefinition GetArticleToolDefinition()
    {
        return new ToolDefinition
      {
    Type = "function",
            Function = new FunctionDefinition
       {
    Name = "get_wikipedia_article",
 Description = "Obtiene contenido de un artículo. Usa después de search_wikipedia_titles.",
         Parameters = new FunctionParameters
     {
          Type = "object",
   Properties = new Dictionary<string, PropertyDefinition>
{
       ["title"] = new PropertyDefinition
             {
        Type = "string",
              Description = "Título exacto del artículo"
          },
                ["language"] = new PropertyDefinition
    {
    Type = "string",
      Description = "Idioma (es, en, etc.)"
   }
         },
      Required = new List<string> { "title" }
        }
            }
        };
    }

  public async Task<string> HandleAsync(ToolParameters parameters, MemoryService memory)
    {
        // Determinar qué tool se está invocando basándose en los parámetros
        if (parameters.Has("query"))
        {
 return await HandleSearchAsync(parameters, memory);
        }
      else if (parameters.Has("title"))
        {
        return await HandleArticleAsync(parameters, memory);
        }
        else
   {
        return "? Parámetros inválidos. Usa 'query' para búsqueda o 'title' para artículo.";
        }
 }

    #region Search Wikipedia Titles

    private async Task<string> HandleSearchAsync(ToolParameters parameters, MemoryService memory)
    {
  var query = parameters.GetString("query");
        var language = parameters.GetString("language", "es");

        LogDebug($"[Search] Query: '{query}', Lang: '{language}'");

try
        {
       var results = await WikidataSearchAsync(query, language);

     if (results.Count == 0)
    {
         return $"? No se encontraron resultados para '{query}'.\n" +
       $"?? Intenta con otros términos o en otro idioma.";
            }

    LogDebug($"[Search] Encontrados {results.Count} resultados");
         return FormatSearchResults(query, results);
        }
   catch (Exception ex)
   {
        LogError($"[Search] Error: {ex.Message}");
  return $"? Error: {ex.Message}";
        }
    }

    private string FormatSearchResults(string query, List<WikipediaTitle> results)
    {
 var response = $"?? Encontré {results.Count} artículo(s) sobre '{query}':\n\n";

        for (int i = 0; i < results.Count; i++)
     {
            var result = results[i];
         response += $"{i + 1}. **{result.Title}**\n";
            if (!string.IsNullOrEmpty(result.Description))
  response += $"   {result.Description}\n";
            response += "\n";
        }

        response += "?? Usa get_wikipedia_article con el título exacto.";
        return response;
    }

    #endregion

    #region Get Wikipedia Article

    private async Task<string> HandleArticleAsync(ToolParameters parameters, MemoryService memory)
    {
      var title = parameters.GetString("title");
  var language = parameters.GetString("language", "es");

        LogDebug($"[Article] Title: '{title}', Lang: '{language}'");

        try
      {
  return await GetWikipediaSummaryAsync(title, language);
        }
        catch (Exception ex)
        {
     LogError($"[Article] Error: {ex.Message}");
   return $"? Error: {ex.Message}";
        }
    }

    private async Task<string> GetWikipediaSummaryAsync(string title, string language)
    {
        var safeTitle = title.Replace(" ", "_");
        var encodedTitle = Uri.EscapeDataString(safeTitle);
    var url = $"https://{language}.wikipedia.org/api/rest_v1/page/summary/{encodedTitle}";

     LogDebug($"[Summary] URL: {url}");

      var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
     {
       LogError($"[Summary] HTTP {response.StatusCode}");
   return $"?? No se pudo obtener '{title}' (HTTP {response.StatusCode}).\n" +
           $"?? Verifica el título o intenta en otro idioma.";
        }

  var wikiData = await response.Content.ReadFromJsonAsync<WikipediaSummaryResponse>();

        if (wikiData == null)
  {
       LogError("[Summary] Error al deserializar");
      return "?? Error al procesar respuesta.";
        }

        LogDebug("[Summary] Contenido obtenido ?");

        var result = $"?? **{wikiData.Title}**\n\n";
        if (!string.IsNullOrEmpty(wikiData.Description))
            result += $"**Descripción:** {wikiData.Description}\n\n";
        if (!string.IsNullOrEmpty(wikiData.Extract))
   result += $"{wikiData.Extract}\n\n";
        if (!string.IsNullOrEmpty(wikiData.ContentUrls?.Desktop?.Page))
            result += $"?? {wikiData.ContentUrls.Desktop.Page}";

      return result;
    }

    #endregion

    #region Wikidata Search (Shared)

    private async Task<List<WikipediaTitle>> WikidataSearchAsync(string query, string language)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var searchUrl = $"https://www.wikidata.org/w/api.php?" +
 $"action=wbsearchentities&" +
     $"search={encodedQuery}&" +
            $"language={language}&" +
       $"limit={MAX_RESULTS}&" +
     $"format=json";

        LogDebug($"[Wikidata] URL: {searchUrl}");

        var response = await _httpClient.GetAsync(searchUrl);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var searchResponse = JsonSerializer.Deserialize<WikidataSearchResponse>(content);
        var searchResults = searchResponse?.Search ?? new List<WikidataSearchResult>();

        var results = new List<WikipediaTitle>();

        foreach (var searchResult in searchResults)
        {
            var wikiTitle = await GetWikipediaTitleFromWikidataAsync(searchResult.Id, language);

            if (!string.IsNullOrEmpty(wikiTitle))
         {
     results.Add(new WikipediaTitle
           {
   Title = wikiTitle,
     Description = searchResult.Description ?? "",
        WikidataId = searchResult.Id
         });

       LogDebug($"  - '{wikiTitle}' ({searchResult.Description})");
         }
        }

        return results;
    }

    private async Task<string?> GetWikipediaTitleFromWikidataAsync(string wikidataId, string language)
    {
        try
        {
 var url = $"https://www.wikidata.org/w/api.php?" +
            $"action=wbgetentities&" +
            $"ids={wikidataId}&" +
            $"props=sitelinks&" +
             $"sitefilter={language}wiki&" +
 $"format=json";

   var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

    var content = await response.Content.ReadAsStringAsync();
    var entityResponse = JsonSerializer.Deserialize<WikidataEntityResponse>(content);

            var sitekey = $"{language}wiki";
            var entity = entityResponse?.Entities?.Values.FirstOrDefault();
  var sitelink = entity?.Sitelinks?.GetValueOrDefault(sitekey);

         return sitelink?.Title;
        }
        catch
        {
    return null;
        }
    }

    #endregion

    #region Logging

    private void LogDebug(string message)
    {
    if (_debugMode)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"[DEBUG] {message}");
          Console.ResetColor();
        }
    }

    private void LogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        Console.ResetColor();
    }

    #endregion

    #region DTOs

    private class WikipediaTitle
    {
 public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string WikidataId { get; set; } = string.Empty;
    }

    private class WikidataSearchResponse
    {
     [JsonPropertyName("search")]
        public List<WikidataSearchResult>? Search { get; set; }
    }

    private class WikidataSearchResult
    {
  [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

      [JsonPropertyName("label")]
      public string Label { get; set; } = string.Empty;

        [JsonPropertyName("description")]
    public string? Description { get; set; }
    }

    private class WikidataEntityResponse
 {
    [JsonPropertyName("entities")]
        public Dictionary<string, WikidataEntity>? Entities { get; set; }
    }

    private class WikidataEntity
    {
        [JsonPropertyName("sitelinks")]
        public Dictionary<string, WikidataSitelink>? Sitelinks { get; set; }
    }

    private class WikidataSitelink
    {
  [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;
    }

    private class WikipediaSummaryResponse
    {
        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("extract")]
        public string? Extract { get; set; }

  [JsonPropertyName("content_urls")]
        public ContentUrls? ContentUrls { get; set; }
    }

    private class ContentUrls
    {
        [JsonPropertyName("desktop")]
        public DesktopUrl? Desktop { get; set; }
    }

    private class DesktopUrl
    {
        [JsonPropertyName("page")]
        public string? Page { get; set; }
    }

    #endregion
}
