using SmartTemplate.Core.Plugins;
using SmartTemplate.Plugin.SqlFile.Dialects;

namespace SmartTemplate.Plugin.SqlFile;

/// <summary>
/// SmartTemplate plugin that reads a SQL CREATE TABLE script from a file,
/// parses column definitions, and exposes them as template variables.
///
/// Required keys in data.yaml / --var:
///   table     - table name; used to locate &lt;sql_dir&gt;/&lt;table&gt;.sql
///   sql_dir   - folder containing *.sql files (resolved relative to the
///               working directory where `st` is invoked)
///
/// Optional keys:
///   entity          - C# class name (defaults to table name when absent)
///   sql_dialect     - "mssql" (default) | "postgres" | "postgresql" | "pg"
///   exclude_columns - list of column names to omit (e.g. base-class columns)
///
/// Exposed template variables after enrichment:
///   properties  - list of objects with:
///                   name        string   column name as written in DDL
///                   type        string   C# type (e.g. "string", "int?")
///                   sql_type    string   raw SQL keyword (e.g. "NVARCHAR")
///                   is_nullable bool
/// </summary>
public sealed class SqlFilePlugin : IPlugin
{
    public string Name => "SqlFile";

    public async Task<Dictionary<string, object?>> EnrichAsync(
        Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
    {
        // 1. Required: table name
        if (!data.TryGetValue("table", out var tableObj) || tableObj is not string { Length: > 0 } tableName)
            throw new InvalidOperationException(
                "SqlFilePlugin: 'table' key (non-empty string) is required in data.");

        // 2. Required: sql_dir
        if (!data.TryGetValue("sql_dir", out var sqlDirObj) || sqlDirObj is not string { Length: > 0 } sqlDirRaw)
            throw new InvalidOperationException(
                "SqlFilePlugin: 'sql_dir' key (non-empty string) is required in data.");

        // Resolve relative to the working directory where `st` was invoked.
        var sqlDir = Path.GetFullPath(sqlDirRaw);

        // 3. Find the SQL file
        var sqlFile = FindSqlFile(sqlDir, tableName)
            ?? throw new FileNotFoundException(
                $"SqlFilePlugin: no .sql file for table '{tableName}' found in '{sqlDir}'.");

        // 4. Parse columns
        var sql     = await File.ReadAllTextAsync(sqlFile, cancellationToken);
        var columns = SqlParser.Parse(sql);

        // 5. Filter excluded columns
        var excluded = GetExcludeColumns(data);
        columns = [.. columns.Where(c => !excluded.Contains(c.Name))];

        // 6. Map SQL types to C# types
        var dialect    = GetDialect(data);
        var properties = columns
            .Select(c => (object?)new Dictionary<string, object?>
            {
                ["name"]        = c.Name,
                ["type"]        = dialect.MapType(c.SqlType, c.IsNullable),
                ["sql_type"]    = c.SqlType,
                ["is_nullable"] = c.IsNullable,
            })
            .ToList();

        data["properties"] = properties;

        // Default entity name to table name when not supplied via prompt / --var
        if (!data.TryGetValue("entity", out var entityObj) || entityObj is not string { Length: > 0 })
            data["entity"] = tableName;

        return data;
    }

    // -------------------------------------------------------------------------

    private static string? FindSqlFile(string dir, string tableName)
    {
        if (!Directory.Exists(dir)) return null;

        // Exact case match first
        var exact = Path.Combine(dir, $"{tableName}.sql");
        if (File.Exists(exact)) return exact;

        // Case-insensitive fallback (useful on Linux)
        return Directory
            .EnumerateFiles(dir, "*.sql", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => string.Equals(
                Path.GetFileNameWithoutExtension(f),
                tableName,
                StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string> GetExcludeColumns(Dictionary<string, object?> data)
    {
        if (!data.TryGetValue("exclude_columns", out var raw))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return raw switch
        {
            List<object?> list => list
                .OfType<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };
    }

    private static ISqlDialect GetDialect(Dictionary<string, object?> data)
    {
        if (data.TryGetValue("sql_dialect", out var v) && v is string s)
            return s.ToLowerInvariant() switch
            {
                "postgres" or "postgresql" or "pg" => new PostgreSqlDialect(),
                _ => new MsSqlDialect()
            };

        return new MsSqlDialect();
    }
}
