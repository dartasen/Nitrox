using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Models.Resources.Core;

namespace Nitrox.Server.Subnautica.Models.Resources.Parsers;

internal sealed class RandomStartResource(SubnauticaAssetsManager assetsManager, IOptions<ServerStartOptions> options) : IGameResource
{
    private readonly SubnauticaAssetsManager assetsManager = assetsManager;
    private readonly IOptions<ServerStartOptions> options = options;
    private readonly TaskCompletionSource<RandomStartGenerator> randomStartGeneratorTcs = new();

    public Task LoadAsync(CancellationToken cancellationToken)
    {
        randomStartGeneratorTcs.TrySetResult(LoadAndGetRandomStartGenerator(cancellationToken));
        return Task.CompletedTask;
    }

    public Task CleanupAsync()
    {
        assetsManager.Dispose();
        return Task.CompletedTask;
    }

    public Task<RandomStartGenerator> GetRandomStartGeneratorAsync() => randomStartGeneratorTcs.Task;

    private RandomStartGenerator? LoadAndGetRandomStartGenerator(CancellationToken cancellationToken = default)
    {
        string bundlePath = Path.Combine(options.Value.GetSubnauticaStandaloneResourcePath(), "essentials.unity_0ee8dd89ed55f05bc38a09cc77137d4e.bundle");
        BundleFileInstance bundleFileInst = assetsManager.LoadBundleFile(bundlePath);
        AssetsFileInstance assetFileInst = assetsManager.LoadAssetsFileFromBundle(bundleFileInst, 0, true);
        cancellationToken.ThrowIfCancellationRequested();

        AssetTypeValueField textureValueField = assetsManager.GetBaseField(assetFileInst, assetFileInst.file.GetAssetInfo(assetsManager, "RandomStart", AssetClassID.Texture2D));
        TextureFile textureFile = TextureFile.ReadTextureFile(textureValueField);
        byte[] texDat = textureFile.GetTextureData(assetFileInst);
        cancellationToken.ThrowIfCancellationRequested();

        if (texDat is not { Length: > 0 })
        {
            return null;
        }

        PixelProvider pixelProvider = new(texDat, textureFile.m_Width, textureFile.m_Height);
        cancellationToken.ThrowIfCancellationRequested();
        return new RandomStartGenerator(pixelProvider);
    }

    internal sealed class PixelProvider : RandomStartGenerator.IPixelProvider
    {
        private const int BYTES_PER_PIXEL = 4;

        private readonly byte[] textureData;
        private readonly int width;
        private readonly int height;

        public PixelProvider(byte[] textureData, int width, int height)
        {
            ArgumentNullException.ThrowIfNull(textureData);
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }
            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            int expectedLength = checked(width * height * BYTES_PER_PIXEL);
            if (textureData.Length < expectedLength)
            {
                throw new ArgumentException("Texture data is shorter than required for its dimensions.", nameof(textureData));
            }

            this.textureData = textureData;
            this.width = width;
            this.height = height;
        }

        private int GetPixelIndex(int x, int y)
        {
            int clampedX = Math.Clamp(x, 0, width - 1);
            int clampedY = Math.Clamp(y, 0, height - 1);
            int flippedY = height - clampedY - 1;
            return (flippedY * width + clampedX) * BYTES_PER_PIXEL;
        }

        public byte GetRed(int x, int y) => textureData[GetPixelIndex(x, y) + 2];

        public byte GetGreen(int x, int y) => textureData[GetPixelIndex(x, y) + 1];

        public byte GetBlue(int x, int y) => textureData[GetPixelIndex(x, y)];
    }
}
