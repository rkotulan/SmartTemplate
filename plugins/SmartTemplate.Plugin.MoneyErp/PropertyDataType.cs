namespace SmartTemplate.Plugin.MoneyErp;

/// <summary>
/// Maps the integer value of MetaData_Properties.DataType to a human-readable name.
/// Mirrors the Money ERP S5 internal PropertyDataType enum.
/// </summary>
public enum PropertyDataType
{
    Int         = 0,
    Float       = 1,
    String      = 2,
    Boolean     = 3,
    Guid        = 4,
    Object      = 5,
    DateTime    = 6,
    Enumerator  = 7,
    Unspecified = 8,
    Group       = 9,
    Binary      = 10,
}
