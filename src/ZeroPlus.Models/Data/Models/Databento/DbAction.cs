namespace ZeroPlus.Models.Data.Models.Databento;

public enum DbAction : byte
{
    Add = 65, // 0x41
    Cancel = 67, // 0x43
    Fill = 70, // 0x46
    Modify = 77, // 0x4D
    None = 78, // 0x4E
    Clear = 82, // 0x52
    Trade = 84, // 0x54
}