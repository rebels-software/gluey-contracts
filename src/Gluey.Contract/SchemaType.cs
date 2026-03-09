namespace Gluey.Contract;

/// <summary>
/// Represents the JSON Schema "type" keyword values as a flags enum.
/// Multiple types can be combined (e.g., <c>SchemaType.String | SchemaType.Null</c>
/// for <c>"type": ["string", "null"]</c>).
/// </summary>
[Flags]
internal enum SchemaType : byte
{
    None    = 0,
    Null    = 1 << 0,   // 1
    Boolean = 1 << 1,   // 2
    Integer = 1 << 2,   // 4
    Number  = 1 << 3,   // 8
    String  = 1 << 4,   // 16
    Array   = 1 << 5,   // 32
    Object  = 1 << 6,   // 64
}
