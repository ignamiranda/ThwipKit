namespace ThwipKit.Core.Hashing;

/// <summary>
/// CRC-32 (reflected, IEEE 802.3 / CRC-32C-compatible table) with configurable
/// initial value and final XOR so callers can match engine-specific variants.
/// </summary>
public static class Crc32
{
    public const uint DefaultInitialValue = 0xFFFFFFFF;
    public const uint DefaultFinalXor = 0xFFFFFFFF;

    private static readonly uint[] Table = BuildTable(0xEDB88320U);

    private static uint[] BuildTable(uint polynomial)
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
            }
            table[i] = crc;
        }
        return table;
    }

    public static uint Compute(ReadOnlySpan<byte> data, uint initialValue = DefaultInitialValue, uint finalXor = DefaultFinalXor)
    {
        uint crc = initialValue;
        foreach (byte b in data)
        {
            crc = (crc >> 8) ^ Table[(int)((crc ^ b) & 0xFF)];
        }
        return crc ^ finalXor;
    }
}
