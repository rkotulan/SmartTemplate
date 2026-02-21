namespace SmartTemplate.Plugin.SqlFile.Dialects;

/// <summary>Maps SQL type names to C# type strings.</summary>
internal interface ISqlDialect
{
    /// <param name="sqlType">Raw SQL type keyword, e.g. "NVARCHAR", "INT".</param>
    /// <param name="isNullable">Whether the column allows NULL.</param>
    /// <returns>C# type string, e.g. "string", "int?", "DateTime".</returns>
    string MapType(string sqlType, bool isNullable);
}
