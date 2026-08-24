using ThwipKit.Core.Staging;

namespace ThwipKit.Core.Assets;

public static class AssetClassifier
{
    private static readonly IReadOnlyDictionary<string, AssetType> ExtensionMap = new Dictionary<string, AssetType>(
        StringComparer.OrdinalIgnoreCase)
    {
        [".texture"] = AssetType.Texture,
        [".dds"] = AssetType.Texture,
        [".png"] = AssetType.Texture,
        [".model"] = AssetType.Model,
        [".mesh"] = AssetType.Model,
        [".skeleton"] = AssetType.Model,
        [".material"] = AssetType.Material,
        [".mat"] = AssetType.Material,
        [".config"] = AssetType.Config,
        [".cfg"] = AssetType.Config,
        [".wem"] = AssetType.Audio,
        [".bnk"] = AssetType.Audio,
    };

    public static AssetType Classify(AssetInfo asset)
    {
        if (asset is null)
        {
            return AssetType.Unknown;
        }

        if (asset.Type != AssetType.Unknown)
        {
            return asset.Type;
        }

        string? name = asset.ResolvedName ?? Path.GetFileName(asset.AssetIdHex);
        string extension = Path.GetExtension(name);

        if (string.IsNullOrEmpty(extension))
        {
            return AssetType.Unknown;
        }

        return ExtensionMap.TryGetValue(extension, out AssetType type) ? type : AssetType.Unknown;
    }
}
