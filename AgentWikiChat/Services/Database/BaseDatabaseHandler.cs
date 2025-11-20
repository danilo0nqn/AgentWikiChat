using System.Text.RegularExpressions;

namespace AgentWikiChat.Services.Database;

/// <summary>
/// Clase base abstracta con lógica común de validación para todos los proveedores.
/// </summary>
public abstract class BaseDatabaseHandler : IDatabaseHandler
{
    // Palabras clave prohibidas (case-insensitive)
    protected static readonly string[] ProhibitedKeywords = new[]
    {
        "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER",
        "TRUNCATE", "EXEC", "EXECUTE", "SP_", "XP_", "BACKUP",
        "RESTORE", "GRANT", "REVOKE", "DENY", "MERGE"
    };

    public abstract string ProviderName { get; }
    public abstract Task<QueryResult> ExecuteQueryAsync(string query, int maxRows);
    public abstract Task<List<string>> GetTablesAsync();
    public abstract Task<List<ColumnInfo>> GetTableSchemaAsync(string tableName);

    /// <summary>
    /// Validación común para todos los proveedores.
    /// </summary>
    public virtual ValidationResult ValidateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return ValidationResult.Fail("La consulta está vacía.");
        }

        // Normalizar: remover comentarios y limpiar espacios
        var normalizedQuery = RemoveSqlComments(query).Trim();

        // 1. Verificar que empiece con SELECT (ignorando espacios y saltos de línea)
        if (!Regex.IsMatch(normalizedQuery, @"^\s*SELECT\s+", RegexOptions.IgnoreCase))
        {
            return ValidationResult.Fail("Solo se permiten consultas SELECT. La consulta debe comenzar con SELECT.");
        }

        // 2. Buscar palabras clave prohibidas
        foreach (var keyword in ProhibitedKeywords)
        {
            // Buscar la palabra completa (con límites de palabra)
            var pattern = $@"\b{Regex.Escape(keyword)}\b";
            if (Regex.IsMatch(normalizedQuery, pattern, RegexOptions.IgnoreCase))
            {
                return ValidationResult.Fail($"Palabra clave prohibida detectada: '{keyword}'. Solo se permiten consultas de solo lectura (SELECT).");
            }
        }

        // 3. Verificar que no contenga punto y coma seguido de otra instrucción
        var statements = normalizedQuery.Split(';')
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (statements.Count > 1)
        {
            return ValidationResult.Fail("No se permiten múltiples instrucciones SQL (separadas por ;). Solo una consulta SELECT por vez.");
        }

        // 4. Verificar que no contenga -- (comentarios inline que podrían ocultar código)
        if (normalizedQuery.Contains("--"))
        {
            return ValidationResult.Fail("No se permiten comentarios inline (--) en la consulta por razones de seguridad.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Remueve comentarios SQL (/* */ y --)
    /// </summary>
    protected string RemoveSqlComments(string sql)
    {
        // Remover comentarios de bloque /* */
        sql = Regex.Replace(sql, @"/\*.*?\*/", "", RegexOptions.Singleline);

        // Remover comentarios de línea --
        sql = Regex.Replace(sql, @"--.*?$", "", RegexOptions.Multiline);

        return sql;
    }

    /// <summary>
    /// Trunca texto para display.
    /// </summary>
    protected string TruncateForDisplay(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }
}
