namespace ThwipKit.Core.Hashing;

/// <summary>
/// CRC-32 (PKZIP / IEEE 802.3) over arbitary byte sequences.
/// Polynomial 0xEDB88320, reflected, init 0xFFFFFFFF, final XOR 0xFFFFFFFF.
/// </summary>
public static class Crc32
{
    private static readonly uint[] Table = CreateTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in data)
        {
            crc = Table[(crc ^ b) & 0xFFu] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    public static string ComputeHex(ReadOnlySpan<byte> data) => $"0x{Compute(data):X8}";

    private static uint[] CreateTable()
    {
        const uint polynomial = 0xEDB88320u;
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint remainder = i;
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
