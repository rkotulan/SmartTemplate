namespace SmartTemplate.Plugin.SqlFile;

/// <summary>Parsed column from a CREATE TABLE statement.</summary>
internal sealed record SqlColumn(string Name, string SqlType, bool IsNullable);
