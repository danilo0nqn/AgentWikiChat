using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;

namespace AgentWikiChat.Services.Database;

/// <summary>
/// Handler para bases de datos SQL Server.
/// </summary>
public class SqlServerDatabaseHandler : BaseDatabaseHandler
{
    private readonly string _connectionString;
    private readonly int _commandTimeout;

    public override string ProviderName => "SQL Server";

    public SqlServerDatabaseHandler(string connectionString, int commandTimeout = 30)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _commandTimeout = commandTimeout;
    }

    public override async Task<QueryResult> ExecuteQueryAsync(string query, int maxRows)
    {
        var result = new QueryResult();
        var startTime = DateTime.Now;

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand(query, connection)
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

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var query = @"
            SELECT TABLE_SCHEMA + '.' + TABLE_NAME as FullTableName
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_SCHEMA, TABLE_NAME";

        using var command = new SqlCommand(query, connection);
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

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Separar schema y nombre de tabla
        var parts = tableName.Split('.');
        var schema = parts.Length > 1 ? parts[0] : "dbo";
        var table = parts.Length > 1 ? parts[1] : parts[0];

        var query = @"
            SELECT 
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH,
                CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as IS_PRIMARY_KEY
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                    ON tc.CONSTRAINT_TYPE = 'PRIMARY KEY' 
                    AND tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
            ) pk ON c.TABLE_SCHEMA = pk.TABLE_SCHEMA 
                AND c.TABLE_NAME = pk.TABLE_NAME 
                AND c.COLUMN_NAME = pk.COLUMN_NAME
            WHERE c.TABLE_SCHEMA = @Schema AND c.TABLE_NAME = @Table
            ORDER BY c.ORDINAL_POSITION";

        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@Schema", schema);
        command.Parameters.AddWithValue("@Table", table);

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                ColumnName = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES",
                MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                IsPrimaryKey = reader.GetInt32(4) == 1
            });
        }

        return columns;
    }
}
