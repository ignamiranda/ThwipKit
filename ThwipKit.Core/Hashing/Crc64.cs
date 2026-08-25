namespace ThwipKit.Core.Hashing;

/// <summary>
/// CRC-64 (CRC-64/XZ) over arbitary byte sequences.
/// Polynomial 0xC96C5795D7870F42 (reflected form of 0x42F0E1EBA9EA3693), init 0xFFFFFFFFFFFFFFFF, final XOR 0xFFFFFFFFFFFFFFFF.
/// </summary>
public static class Crc64
{
    private static readonly ulong[] Table = CreateTable();

    public static ulong Compute(ReadOnlySpan<byte> data)
    {
        ulong crc = 0xFFFFFFFFFFFFFFFFuL;
        foreach (byte b in data)
        {
            crc = Table[(crc ^ b) & 0xFFuL] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFFFFFFFFFuL;
    }

    public static string ComputeHex(ReadOnlySpan<byte> data) => $"0x{Compute(data):X16}";

    private static ulong[] CreateTable()
    {
        const ulong polynomial = 0xC96C5795D7870F42uL;
        var table = new ulong[256];
        for (ulong i = 0; i < 256; i++)
        {
            ulong remainder = i;
            for (int bit = 0; bit < 8; bit++)
            {
                remainder = (remainder & 1) != 0
                    ? polynomial ^ (remainder >> 1)
                    : remainder >> 1;
            }

            table[i] = remainder;
        }

        return table;
    }
}
