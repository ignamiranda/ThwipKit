using System;
using System.Buffers.Binary;
using BCnEncoder.Shared;

namespace ThwipKit.Core.Textures;

/// <summary>
/// Parses the Insomniac .texture stub header (pre-BCn payload).
/// Layout based on SpiderTex / RawTex reverse-engineering of Insomniac engine assets.
/// </summary>
public static class TextureHeader
{
    /// <summary>
    /// DXGI_FORMAT codes from Windows SDK (subset used by Insomniac assets).
    /// </summary>
    public enum DxgiFormat : uint
    {
        BC1_UNORM = 71,
        BC1_UNORM_SRGB = 72,
        BC2_UNORM = 74,
        BC3_UNORM = 77,
        BC4_UNORM = 80,
        BC5_UNORM = 83,
        BC6H_UF16 = 95,
        BC7_UNORM = 98,
        BC7_UNORM_SRGB = 99,
    }

    /// <summary>
    /// Result of parsing a .texture header.
    /// </summary>
    public readonly record struct ParseResult(
        int Width,
        int Height,
        int MipCount,
        DxgiFormat Format,
        int HeaderSize,
        ReadOnlyMemory<byte> BlockData
    );

    /// <summary>
    /// Parses a .texture blob and returns header info + raw BCn block data.
    /// </summary>
    /// <exception cref="InvalidDataException">Thrown if the blob is too small or format is unsupported.</exception>
    public static ParseResult Parse(ReadOnlyMemory<byte> textureBytes)
    {
        if (textureBytes.Length < 48)
        {
            throw new InvalidDataException($".texture blob too small ({textureBytes.Length} bytes); expected header + data");
        }

        // Insomniac .texture header (observed in SpiderTex/RawTex):
        // Offset 0x00: uint32 magic/version? (often 0x00000020 or similar)
        // Offset 0x04: uint32 width
        // Offset 0x08: uint32 height
        // Offset 0x0C: uint32 depth/arraySize (usually 1 for 2D)
        // Offset 0x10: uint32 mipCount
        // Offset 0x14: uint32 format (DXGI_FORMAT enum value)
        // Offset 0x18: uint32 unknown/flags
        // Offset 0x1C: uint32 resourceDimension (3 = 2D)
        // ... padding to 48 bytes typically

        uint width = BinaryPrimitives.ReadUInt32LittleEndian(textureBytes.Span[4..8]);
        uint height = BinaryPrimitives.ReadUInt32LittleEndian(textureBytes.Span[8..12]);
        uint mipCount = BinaryPrimitives.ReadUInt32LittleEndian(textureBytes.Span[16..20]);
        uint dxgiCode = BinaryPrimitives.ReadUInt32LittleEndian(textureBytes.Span[20..24]);

        if (width == 0 || height == 0)
        {
            throw new InvalidDataException($"Invalid texture dimensions: {width}x{height}");
        }

        if (mipCount == 0)
        {
            throw new InvalidDataException("Mip count is zero");
        }

        if (!Enum.IsDefined(typeof(DxgiFormat), dxgiCode))
        {
            throw new NotSupportedException($"Unsupported DXGI format 0x{dxgiCode:X} ({dxgiCode})");
        }

        var format = (DxgiFormat)dxgiCode;

        // Block data starts at offset 48 (header size)
        const int headerSize = 48;
        if (textureBytes.Length <= headerSize)
        {
            throw new InvalidDataException("No BCn block data after header");
        }

        var blockData = new ReadOnlyMemory<byte>(textureBytes[headerSize..].ToArray());

        return new ParseResult(
            Width: (int)width,
            Height: (int)height,
            MipCount: (int)mipCount,
            Format: format,
            HeaderSize: headerSize,
            BlockData: blockData
        );
    }

    /// <summary>
    /// Maps DXGI_FORMAT to BCnEncoder CompressionFormat.
    /// </summary>
    public static CompressionFormat DxgiToBcn(DxgiFormat format) => format switch
    {
        DxgiFormat.BC1_UNORM or DxgiFormat.BC1_UNORM_SRGB => CompressionFormat.Bc1,
        DxgiFormat.BC2_UNORM => CompressionFormat.Bc2,
        DxgiFormat.BC3_UNORM => CompressionFormat.Bc3,
        DxgiFormat.BC4_UNORM => CompressionFormat.Bc4,
        DxgiFormat.BC5_UNORM => CompressionFormat.Bc5,
        DxgiFormat.BC6H_UF16 => CompressionFormat.Bc6U,
        DxgiFormat.BC7_UNORM or DxgiFormat.BC7_UNORM_SRGB => CompressionFormat.Bc7,
        _ => throw new NotSupportedException($"DXGI format {format} has no BCnEncoder mapping")
    };
}