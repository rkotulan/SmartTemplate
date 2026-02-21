namespace SmartTemplate.Plugin.SqlFile.Dialects;

/// <summary>T-SQL → C# type mapping (Microsoft SQL Server).</summary>
internal sealed class MsSqlDialect : ISqlDialect
{
    public string MapType(string sqlType, bool isNullable) =>
        sqlType.ToUpperInvariant() switch
        {
            "BIGINT"                                                                        => isNullable ? "long?"           : "long",
            "BINARY" or "IMAGE" or "TIMESTAMP" or "VARBINARY"                              => "byte[]",
            "BIT"                                                                           => isNullable ? "bool?"           : "bool",
            "CHAR" or "NCHAR" or "NTEXT" or "NVARCHAR" or "TEXT" or "VARCHAR" or "XML"    => "string",
            "DATE" or "DATETIME" or "DATETIME2" or "SMALLDATETIME" or "TIME"               => isNullable ? "DateTime?"       : "DateTime",
            "DATETIMEOFFSET"                                                                => isNullable ? "DateTimeOffset?" : "DateTimeOffset",
            "DECIMAL" or "MONEY" or "NUMERIC" or "SMALLMONEY"                              => isNullable ? "decimal?"        : "decimal",
            "FLOAT"                                                                         => isNullable ? "double?"         : "double",
            "INT"                                                                           => isNullable ? "int?"            : "int",
            "REAL"                                                                          => isNullable ? "float?"          : "float",
            "SMALLINT"                                                                      => isNullable ? "short?"          : "short",
            "TINYINT"                                                                       => isNullable ? "byte?"           : "byte",
            "UNIQUEIDENTIFIER"                                                              => isNullable ? "Guid?"           : "Guid",
            _                                                                               => "object"
        };
}
