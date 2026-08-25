using System;
using System.IO;
using System.Linq;
using System.Text;
using BCnEncoder.Decoder;
using BCnEncoder.Encoder;
using BCnEncoder.Shared;
using ThwipKit.Core.Textures;

namespace ThwipKit.Core.Editors;

/// <summary>
/// Converts between Insomniac <c>.texture</c> blobs and standard <c>.dds</c> files.
/// Both formats store the same BCn-compressed block data; this class repackages the
/// blocks between the two container headers (lossless) and additionally exposes a real
/// BCn codec (via <see cref="BcDecoder"/>/<see cref="BcEncoder"/>) to decode blocks to
/// raw pixels and encode raw pixels back to a <c>.texture</c>.
/// </summary>
public static class TextureFormatConverter
{
    private const uint DdsMagic = 0x20534444; // "DDS "
    private const int DdsHeaderSize = 124;

    public static byte[] ConvertTextureToDds(byte[] textureBytes)
    {
        ArgumentNullException.ThrowIfNull(textureBytes);

        TextureHeader.ParseResult parsed = TextureHeader.Parse(textureBytes);
        byte[] blockData = parsed.BlockData.ToArray();
        string fourCc = FourCcFor(parsed.Format);

        using var ms = new MemoryStream();
        using (var writer = new BinaryWriter(ms))
        {
            writer.Write(DdsMagic);
            writer.Write(DdsHeaderSize);

            uint flags = 0x1 | 0x2 | 0x4 | 0x1000 | 0x20000;
            if (parsed.MipCount > 1)
            {
                flags |= 0x80000;
            }

            writer.Write(flags);
            writer.Write((uint)parsed.Height);
            writer.Write((uint)parsed.Width);
            writer.Write((uint)blockData.Length);
            writer.Write(1u); // depth
            writer.Write((uint)parsed.MipCount);
            for (int i = 0; i < 11; i++)
            {
                writer.Write(0u);
            }

            // DDS_PIXELFORMAT (32 bytes)
            writer.Write(32u);
            writer.Write(0x4u); // DDPF_FOURCC
            byte[] fourCcBytes = Encoding.ASCII.GetBytes(fourCc.PadRight(4, ' '));
            writer.Write(fourCcBytes, 0, 4);
            writer.Write(0u); // RGBBitCount
            writer.Write(0u); // R mask
            writer.Write(0u); // G mask
            writer.Write(0u); // B mask
            writer.Write(0u); // A mask

            uint caps = 0x8;
            if (parsed.MipCount > 1)
            {
                caps |= 0x400000;
            }

            writer.Write(caps);
            writer.Write(0u);
            writer.Write(0u);
            writer.Write(0u);
            writer.Write(0u);

            writer.Write(blockData);
        }

        return ms.ToArray();
    }

    public static byte[] ConvertDdsToTexture(byte[] ddsBytes)
    {
        ArgumentNullException.ThrowIfNull(ddsBytes);

        if (ddsBytes.Length < 4 + DdsHeaderSize)
        {
            throw new InvalidDataException("DDS file is too small to contain a header");
        }

        if (BinaryPrimitivesReadUInt32(ddsBytes, 0) != DdsMagic)
        {
            throw new InvalidDataException("Not a DDS file (missing magic)");
        }

        // DDS_PIXELFORMAT.dwFourCC is at offset 4 (magic) + 72 (pixelformat start) + 8 (fourCC).
        string fourCc = Encoding.ASCII.GetString(ddsBytes, 4 + 72 + 8, 4).TrimEnd();
        uint width = BinaryPrimitivesReadUInt32(ddsBytes, 4 + 12);
        uint height = BinaryPrimitivesReadUInt32(ddsBytes, 4 + 8);
        uint mipCount = BinaryPrimitivesReadUInt32(ddsBytes, 4 + 24);
        if (mipCount == 0)
        {
            mipCount = 1;
        }

        TextureHeader.DxgiFormat format = DxgiFor(fourCc);

        int blockStart = 4 + DdsHeaderSize;
        if (ddsBytes.Length <= blockStart)
        {
            throw new InvalidDataException("DDS file has no block data");
        }

        byte[] blockData = new byte[ddsBytes.Length - blockStart];
        Array.Copy(ddsBytes, blockStart, blockData, 0, blockData.Length);

        return BuildTextureHeader(width, height, mipCount, format, blockData);
    }

    /// <summary>Decodes the top mip of a <c>.texture</c> to raw RGBA pixels.</summary>
    public static ColorRgba32[] DecodeTextureToPixels(byte[] textureBytes)
    {
        ArgumentNullException.ThrowIfNull(textureBytes);

        TextureHeader.ParseResult parsed = TextureHeader.Parse(textureBytes);
        CompressionFormat bcn = TextureHeader.DxgiToBcn(parsed.Format);

        var decoder = new BcDecoder();
        return decoder.DecodeRaw(parsed.BlockData.ToArray(), parsed.Width, parsed.Height, bcn);
    }

    /// <summary>Encodes raw RGBA8 pixels into a <c>.texture</c> blob of the given format.</summary>
    public static byte[] EncodePixelsToTexture(byte[] rgbaBytes, int width, int height, TextureHeader.DxgiFormat format)
    {
        ArgumentNullException.ThrowIfNull(rgbaBytes);
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be positive");
        }

        CompressionFormat bcn = TextureHeader.DxgiToBcn(format);

        var encoder = new BcEncoder(bcn);
        byte[][] mips = encoder.EncodeToRawBytes(rgbaBytes, width, height, PixelFormat.Rgba32);
        byte[] blocks = mips.SelectMany(m => m).ToArray();

        return BuildTextureHeader((uint)width, (uint)height, 1, format, blocks);
    }

    private static byte[] BuildTextureHeader(uint width, uint height, uint mipCount, TextureHeader.DxgiFormat format, byte[] blockData)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(0x00000020u); // version/flags
        writer.Write(width);
        writer.Write(height);
        writer.Write(1u); // depth
        writer.Write(mipCount);
        writer.Write((uint)format);
        writer.Write(0u); // flags
        writer.Write(3u); // resource dimension (2D)
        for (int i = 0; i < 16; i++)
        {
            writer.Write((byte)0); // reserved
        }

        writer.Write(blockData);
        return ms.ToArray();
    }

    private static string FourCcFor(TextureHeader.DxgiFormat format) => format switch
    {
        TextureHeader.DxgiFormat.BC1_UNORM or TextureHeader.DxgiFormat.BC1_UNORM_SRGB => "DXT1",
        TextureHeader.DxgiFormat.BC2_UNORM => "DXT3",
        TextureHeader.DxgiFormat.BC3_UNORM => "DXT5",
        TextureHeader.DxgiFormat.BC4_UNORM => "BC4U",
        TextureHeader.DxgiFormat.BC5_UNORM => "BC5U",
        TextureHeader.DxgiFormat.BC6H_UF16 => "BC6H",
        TextureHeader.DxgiFormat.BC7_UNORM or TextureHeader.DxgiFormat.BC7_UNORM_SRGB => "BC7",
        _ => throw new NotSupportedException($"DXGI format {format} cannot be expressed as a DDS FOURCC")
    };

    private static TextureHeader.DxgiFormat DxgiFor(string fourCc) => fourCc switch
    {
        "DXT1" => TextureHeader.DxgiFormat.BC1_UNORM,
        "DXT3" => TextureHeader.DxgiFormat.BC2_UNORM,
        "DXT5" => TextureHeader.DxgiFormat.BC3_UNORM,
        "BC4U" => TextureHeader.DxgiFormat.BC4_UNORM,
        "BC5U" => TextureHeader.DxgiFormat.BC5_UNORM,
        "BC6H" => TextureHeader.DxgiFormat.BC6H_UF16,
        "BC7" => TextureHeader.DxgiFormat.BC7_UNORM,
        _ => throw new NotSupportedException($"Unsupported DDS FOURCC '{fourCc}'")
    };

    private static uint BinaryPrimitivesReadUInt32(byte[] bytes, int offset)
        => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
}
