using System.Text.RegularExpressions;

namespace SmartTemplate.Plugin.SqlFile;

/// <summary>Extracts column definitions from a CREATE TABLE statement.</summary>
internal static class SqlParser
{
    // Matches the beginning of a column definition:
    //   [BracketedName]  or  "QuotedName"  or  `BacktickName`  or  PlainName
    // followed by the SQL type keyword (first word after the name).
    private static readonly Regex ColumnStartRegex = new(
        @"^\s*(?<raw>\[[^\]]+\]|""[^""]*""|`[^`]*`|\w+)\s+(?<type>\w+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NotNullRegex = new(
        @"\bNOT\s+NULL\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NullRegex = new(
        @"\bNULL\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Lines that open a table-level constraint rather than a column definition.
    private static readonly Regex SkipLineRegex = new(
        @"^\s*(CONSTRAINT|PRIMARY\s+KEY|FOREIGN\s+KEY|UNIQUE\b|CHECK\b|INDEX\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses a CREATE TABLE SQL statement and returns the column list.
    /// Column order matches the DDL order.
    /// </summary>
    public static List<SqlColumn> Parse(string sql)
    {
        var columns = new List<SqlColumn>();

        var openParen  = sql.IndexOf('(');
        var closeParen = sql.LastIndexOf(')');
        if (openParen < 0 || closeParen <= openParen)
            return columns;

        var body = sql[(openParen + 1)..closeParen];

        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            if (SkipLineRegex.IsMatch(trimmed)) continue;

            var match = ColumnStartRegex.Match(trimmed);
            if (!match.Success) continue;

            var name    = match.Groups["raw"].Value.Trim('[', ']', '"', '`');
            var sqlType = match.Groups["type"].Value;

            bool isNullable;
            if (NotNullRegex.IsMatch(trimmed))
                isNullable = false;
            else if (NullRegex.IsMatch(trimmed))
                isNullable = true;
            else
                isNullable = true; // no explicit NULL constraint → nullable (PostgreSQL default)

            columns.Add(new SqlColumn(name, sqlType, isNullable));
        }

        return columns;
    }
}
