using System.Data;
using Npgsql;

namespace AgentWikiChat.Services.Database;

/// <summary>
/// Handler para bases de datos PostgreSQL.
/// </summary>
public class PostgreSqlDatabaseHandler : BaseDatabaseHandler
{
    private readonly string _connectionString;
    private readonly int _commandTimeout;

    public override string ProviderName => "PostgreSQL";

    public PostgreSqlDatabaseHandler(string connectionString, int commandTimeout = 30)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _commandTimeout = commandTimeout;
    }

    public override async Task<QueryResult> ExecuteQueryAsync(string query, int maxRows)
    {
        var result = new QueryResult();
        var startTime = DateTime.Now;

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new NpgsqlCommand(query, connection)
        {
            CommandTimeout = _commandTimeout,
            CommandType = CommandType.Text
        };

        using var reader = await command.ExecuteReaderAsync();

        // Obtener metadata de columnas
        var columnNames = new List<string>();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columnNames.Add(reader.GetName(i));
        }

        result.ColumnNames = columnNames;

        // Leer datos
        var rowCount = 0;
        while (await reader.ReadAsync() && rowCount < maxRows)
        {
            var row = new List<object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row.Add(reader.IsDBNull(i) ? null : reader.GetValue(i));
            }
            result.Rows.Add(row);
            rowCount++;
        }

        result.RowCount = rowCount;
        result.ExecutionTimeMs = (DateTime.Now - startTime).TotalMilliseconds;

        return result;
    }

    public override async Task<List<string>> GetTablesAsync()
    {
        var tables = new List<string>();

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var query = @"
            SELECT schemaname || '.' || tablename as full_table_name
            FROM pg_catalog.pg_tables
            WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
            ORDER BY schemaname, tablename";

        using var command = new NpgsqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    public override async Task<List<ColumnInfo>> GetTableSchemaAsync(string tableName)
    {
        var columns = new List<ColumnInfo>();

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Separar schema y nombre de tabla
        var parts = tableName.Split('.');
        var schema = parts.Length > 1 ? parts[0] : "public";
        var table = parts.Length > 1 ? parts[1] : parts[0];

        var query = @"
            SELECT 
                c.column_name,
                c.data_type,
                c.is_nullable,
                c.character_maximum_length,
                CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END as is_primary_key
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT ku.table_schema, ku.table_name, ku.column_name
                FROM information_schema.table_constraints tc
                INNER JOIN information_schema.key_column_usage ku
                    ON tc.constraint_type = 'PRIMARY KEY' 
                    AND tc.constraint_name = ku.constraint_name
                    AND tc.table_schema = ku.table_schema
                    AND tc.table_name = ku.table_name
            ) pk ON c.table_schema = pk.table_schema 
                AND c.table_name = pk.table_name 
                AND c.column_name = pk.column_name
            WHERE c.table_schema = @schema AND c.table_name = @table
            ORDER BY c.ordinal_position";

        using var command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("@schema", schema);
        command.Parameters.AddWithValue("@table", table);

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                ColumnName = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES",
                MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                IsPrimaryKey = reader.GetBoolean(4)
            });
        }

        return columns;
    }
}
