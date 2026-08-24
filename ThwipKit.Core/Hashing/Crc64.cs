namespace ThwipKit.Core.Hashing;

/// <summary>
/// CRC-64 (reflected, CRC-64/XZ polynomial) with configurable initial value and
/// final XOR so callers can match engine-specific variants.
/// </summary>
public static class Crc64
{
    public const ulong DefaultInitialValue = 0xFFFFFFFFFFFFFFFFUL;
    public const ulong DefaultFinalXor = 0xFFFFFFFFFFFFFFFFUL;

    private static readonly ulong[] Table = BuildTable(0xC96C5795D7870F42UL);

    private static ulong[] BuildTable(ulong polynomial)
    {
        var table = new ulong[256];
        for (uint i = 0; i < 256; i++)
        {
            ulong crc = i;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
            }
            table[i] = crc;
        }
        return table;
    }

    public static ulong Compute(ReadOnlySpan<byte> data, ulong initialValue = DefaultInitialValue, ulong finalXor = DefaultFinalXor)
    {
        ulong crc = initialValue;
        foreach (byte b in data)
        {
            crc = (crc >> 8) ^ Table[(int)((crc ^ b) & 0xFF)];
        }
        return crc ^ finalXor;
    }
}
