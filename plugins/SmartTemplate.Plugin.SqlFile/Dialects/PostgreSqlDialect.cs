namespace SmartTemplate.Plugin.SqlFile.Dialects;

/// <summary>
/// PostgreSQL → C# type mapping.
/// Multi-word PG types (e.g. DOUBLE PRECISION, CHARACTER VARYING) are not
/// supported — use the single-word aliases (FLOAT8, VARCHAR) in your DDL.
/// </summary>
internal sealed class PostgreSqlDialect : ISqlDialect
{
    public string MapType(string sqlType, bool isNullable) =>
        sqlType.ToUpperInvariant() switch
        {
            "INT" or "INT4" or "INTEGER" or "SERIAL"    => isNullable ? "int?"            : "int",
            "BIGINT" or "INT8" or "BIGSERIAL"           => isNullable ? "long?"           : "long",
            "SMALLINT" or "INT2" or "SMALLSERIAL"       => isNullable ? "short?"          : "short",
            "BOOLEAN" or "BOOL"                         => isNullable ? "bool?"           : "bool",
            "TEXT" or "VARCHAR" or "CHAR" or "CITEXT"
                or "NAME"                               => "string",
            "TIMESTAMP"                                 => isNullable ? "DateTime?"       : "DateTime",
            "TIMESTAMPTZ"                               => isNullable ? "DateTimeOffset?" : "DateTimeOffset",
            "DATE"                                      => isNullable ? "DateOnly?"       : "DateOnly",
            "TIME"                                      => isNullable ? "TimeOnly?"       : "TimeOnly",
            "NUMERIC" or "DECIMAL"                      => isNullable ? "decimal?"        : "decimal",
            "REAL" or "FLOAT4"                          => isNullable ? "float?"          : "float",
            "FLOAT" or "FLOAT8"                         => isNullable ? "double?"         : "double",
            "UUID"                                      => isNullable ? "Guid?"           : "Guid",
            "BYTEA"                                     => "byte[]",
            "JSON" or "JSONB"                           => "string",
            _                                           => "object"
        };
}
