using Microsoft.Data.SqlClient;
using SmartTemplate.Core.Plugins;

namespace SmartTemplate.Plugin.MoneyErp;

/// <summary>
/// Reads metadata from the Money ERP S5 system database and exposes it
/// as template variables.
///
/// Required data-dictionary keys (set via data.yaml or --var):
///   object_name       — ObjectName in MetaData_Objects (e.g. "Smlouva")
///   connection_string — ADO.NET SQL Server connection string
///
/// Keys added to the template context:
///   object_caption   — ObjectCaption from MetaData_Objects
///
///   properties       — top-level properties (PropertyType 0 and 1, no parent section)
///     each property:
///       name             — PropertyName
///       caption          — PropertyCaption
///       data_type        — PropertyDataType enum name or null
///       control_prefix   — derived control prefix or null (null = skip in template)
///       control_name     — prefix + name  (e.g. "cbTypSmlouvy")
///       inner_object     — InnerObjectName (for PropertyType=1) or null
///       inner_object_type — InnerObjectType int or null
///
///   property_groups  — PropertyType=3 sections with their children
///     each group:
///       name           — parent PropertyName  (e.g. "Ifrs16")
///       caption        — parent PropertyCaption
///       inner_object   — InnerObjectName of the section (e.g. "Smlouva_Ifrs16")
///       interface_type — "I" + inner_object  (e.g. "ISmlouva_Ifrs16")
///       properties     — child properties (PropertyType 0, 1 and 4)
///         same fields as top-level properties, plus:
///           control_name — prefix + groupName + name  (e.g. "chbIfrs16Enabled")
///
/// Control prefix rules:
///   PropertyType=1, InnerObjectType=0  → pbe
///   PropertyType=1, InnerObjectType=2  → gpbe
///   PropertyType=1, InnerObjectType=1/4 → null (collections, skip)
///   PropertyType=0 or 4, DataType:
///     Int(0)→nud  Float(1)→ce  String(2)→tb  Boolean(3)→chb
///     DateTime(6)→ndtp  Enumerator(7)→cb
///     Guid(4), Unspecified(8), Binary(10), null → null (skip)
///   PropertyType=4 with DataType=NULL: resolved via KeyProperty_ID→DataType
/// </summary>
public sealed class MoneyErpPlugin : IPlugin
{
    public string Name => "MoneyErp";

    // -----------------------------------------------------------------------
    // SQL: top-level properties (PropertyType 0 and 1, no Parent_ID)
    // -----------------------------------------------------------------------
    private const string SqlTopLevel = """
        SELECT
            p.PropertyName,
            p.PropertyCaption,
            p.PropertyType,
            p.DataType,
            p.InnerObjectName,
            p.InnerObjectType
        FROM MetaData_Objects o
        JOIN MetaData_Properties p
            ON  p.Object_ID    = o.ID
            AND p.Parent_ID   IS NULL
            AND p.PropertyType IN (0, 1)
        WHERE o.ObjectName = @ObjectName
        ORDER BY p.PropertyName
        """;

    // -----------------------------------------------------------------------
    // SQL: section groups (PropertyType=3) with their children
    //      Children may be PropertyType 0, 1 or 4.
    //      For PropertyType=4, KeyProperty_ID→DataType provides the actual type.
    // -----------------------------------------------------------------------
    private const string SqlGroups = """
        SELECT
            parent_p.PropertyName    AS GroupName,
            parent_p.PropertyCaption AS GroupCaption,
            parent_p.InnerObjectName AS GroupInnerObject,
            child_p.PropertyName     AS PropName,
            child_p.PropertyCaption  AS PropCaption,
            child_p.PropertyType     AS PropType,
            child_p.DataType         AS PropDataType,
            key_p.DataType           AS KeyDataType,
            child_p.InnerObjectName  AS PropInnerObject,
            child_p.InnerObjectType  AS PropInnerObjectType
        FROM MetaData_Objects o
        JOIN MetaData_Properties parent_p
            ON  parent_p.Object_ID  = o.ID
            AND parent_p.PropertyType = 3
            AND parent_p.Parent_ID  IS NULL
        JOIN MetaData_Properties child_p
            ON  child_p.Parent_ID   = parent_p.ID
            AND child_p.PropertyType IN (0, 1, 4)
        LEFT JOIN MetaData_Properties key_p
            ON  key_p.ID = child_p.KeyProperty_ID
        WHERE o.ObjectName = @ObjectName
        ORDER BY parent_p.PropertyName, child_p.PropertyName
        """;

    private const string SqlCaption = """
        SELECT ObjectCaption
        FROM MetaData_Objects
        WHERE ObjectName = @ObjectName
        """;

    // -----------------------------------------------------------------------

    public async Task<Dictionary<string, object?>> EnrichAsync(
        Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
    {
        var objectName       = data.GetString("object_name")
            ?? throw new InvalidOperationException("MoneyErp plugin: 'object_name' is required.");
        var connectionString = data.GetString("connection_string")
            ?? throw new InvalidOperationException("MoneyErp plugin: 'connection_string' is required.");

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        data["object_caption"]   = await GetCaptionAsync(conn, objectName, cancellationToken);
        data["properties"]       = await GetTopLevelPropertiesAsync(conn, objectName, cancellationToken);
        data["property_groups"]  = await GetPropertyGroupsAsync(conn, objectName, cancellationToken);

        return data;
    }

    // -----------------------------------------------------------------------

    private static async Task<string?> GetCaptionAsync(
        SqlConnection conn, string objectName, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(SqlCaption, conn);
        cmd.Parameters.AddWithValue("@ObjectName", objectName);
        return (await cmd.ExecuteScalarAsync(ct))?.ToString();
    }

    private static async Task<List<Dictionary<string, object?>>> GetTopLevelPropertiesAsync(
        SqlConnection conn, string objectName, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(SqlTopLevel, conn);
        cmd.Parameters.AddWithValue("@ObjectName", objectName);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var result = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync(ct))
        {
            var name          = reader.GetString(reader.GetOrdinal("PropertyName"));
            var caption       = reader.IsDBNull(reader.GetOrdinal("PropertyCaption"))
                ? name : reader.GetString(reader.GetOrdinal("PropertyCaption"));
            var propType      = (int)reader.GetInt16(reader.GetOrdinal("PropertyType"));
            var dataType      = reader.IsDBNull(reader.GetOrdinal("DataType"))
                ? (byte?)null : (byte)reader.GetInt16(reader.GetOrdinal("DataType"));
            var innerObj      = reader.IsDBNull(reader.GetOrdinal("InnerObjectName"))
                ? null : reader.GetString(reader.GetOrdinal("InnerObjectName"));
            var innerObjType  = reader.IsDBNull(reader.GetOrdinal("InnerObjectType"))
                ? (int?)null : (int)reader.GetInt16(reader.GetOrdinal("InnerObjectType"));

            var prefix = ResolvePrefix(propType, dataType, resolvedDataType: null, innerObjType);

            result.Add(new Dictionary<string, object?>
            {
                ["name"]              = name,
                ["caption"]           = caption,
                ["data_type"]         = DataTypeName(dataType),
                ["control_prefix"]    = prefix,
                ["control_name"]      = prefix is not null ? prefix + name : null,
                ["inner_object"]      = innerObj,
                ["inner_object_type"] = innerObjType,
            });
        }

        return result;
    }

    private static async Task<List<Dictionary<string, object?>>> GetPropertyGroupsAsync(
        SqlConnection conn, string objectName, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(SqlGroups, conn);
        cmd.Parameters.AddWithValue("@ObjectName", objectName);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var groups = new List<Dictionary<string, object?>>();
        Dictionary<string, object?>? currentGroup = null;
        List<Dictionary<string, object?>>? currentProps = null;
        string? currentGroupName = null;

        while (await reader.ReadAsync(ct))
        {
            var groupName       = reader.GetString(reader.GetOrdinal("GroupName"));
            var groupCaption    = reader.IsDBNull(reader.GetOrdinal("GroupCaption"))
                ? groupName : reader.GetString(reader.GetOrdinal("GroupCaption"));
            var groupInnerObj   = reader.IsDBNull(reader.GetOrdinal("GroupInnerObject"))
                ? null : reader.GetString(reader.GetOrdinal("GroupInnerObject"));

            var propName        = reader.GetString(reader.GetOrdinal("PropName"));
            var propCaption     = reader.IsDBNull(reader.GetOrdinal("PropCaption"))
                ? propName : reader.GetString(reader.GetOrdinal("PropCaption"));
            var propType        = (int)reader.GetInt16(reader.GetOrdinal("PropType"));
            var propDataType    = reader.IsDBNull(reader.GetOrdinal("PropDataType"))
                ? (byte?)null : reader.GetByte(reader.GetOrdinal("PropDataType"));
            var keyDataType     = reader.IsDBNull(reader.GetOrdinal("KeyDataType"))
                ? (byte?)null : (byte)reader.GetInt16(reader.GetOrdinal("KeyDataType"));
            var innerObj        = reader.IsDBNull(reader.GetOrdinal("PropInnerObject"))
                ? null : reader.GetString(reader.GetOrdinal("PropInnerObject"));
            var innerObjType    = reader.IsDBNull(reader.GetOrdinal("PropInnerObjectType"))
                ? (int?)null : (int)reader.GetInt16(reader.GetOrdinal("PropInnerObjectType"));

            var effectiveDataType = propDataType ?? keyDataType;
            var prefix = ResolvePrefix(propType, propDataType, keyDataType, innerObjType);
            var interfaceType = groupInnerObj is not null ? "I" + groupInnerObj : null;

            if (groupName != currentGroupName)
            {
                currentGroupName = groupName;
                currentProps     = [];
                currentGroup     = new Dictionary<string, object?>
                {
                    ["name"]           = groupName,
                    ["caption"]        = groupCaption,
                    ["inner_object"]   = groupInnerObj,
                    ["interface_type"] = interfaceType,
                    ["properties"]     = currentProps
                };
                groups.Add(currentGroup);
            }

            currentProps!.Add(new Dictionary<string, object?>
            {
                ["name"]              = propName,
                ["caption"]           = propCaption,
                ["data_type"]         = DataTypeName(effectiveDataType),
                ["control_prefix"]    = prefix,
                ["control_name"]      = prefix is not null ? prefix + groupName + propName : null,
                ["inner_object"]      = innerObj,
                ["inner_object_type"] = innerObjType,
            });
        }

        return groups;
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Resolves the control prefix from property metadata.
    /// Returns null when the property should be skipped (no FormRelation generated).
    /// </summary>
    private static string? ResolvePrefix(
        int propertyType, byte? dataType, byte? resolvedDataType, int? innerObjectType)
    {
        if (propertyType == 1) // inner object reference
        {
            return innerObjectType switch
            {
                0 => "pbe",
                2 => "gpbe",
                _ => null   // 1 = collection, 4 = collection → skip
            };
        }

        // PropertyType 0 (scalar) or 4 (type from KeyProperty_ID)
        var effective = dataType ?? resolvedDataType;
        return effective switch
        {
            0 => "nud",    // Int
            1 => "ce",     // Float
            2 => "tb",     // String
            3 => "chb",    // Boolean
            4 => null,     // Guid — skip
            6 => "ndtp",   // DateTime
            7 => "cb",     // Enumerator
            _ => null      // Unspecified(8), Binary(10), null → skip
        };
    }

    private static string? DataTypeName(byte? dataType)
    {
        if (dataType is null) return null;
        return Enum.IsDefined(typeof(PropertyDataType), (int)dataType.Value)
            ? ((PropertyDataType)dataType.Value).ToString()
            : null;
    }
}

// ---------------------------------------------------------------------------
file static class DataDictExtensions
{
    public static string? GetString(this Dictionary<string, object?> data, string key)
        => data.TryGetValue(key, out var v) ? v?.ToString() : null;
}
