using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nitrox.Model;
using Nitrox.Model.Configuration;
using Nitrox.Model.DataStructures.Unity;
using Nitrox.Model.Helper;
using Nitrox.Model.Subnautica.DataStructures.GameLogic;
using Nitrox.Server.Subnautica.Extensions;
using Nitrox.Server.Subnautica.Models.Resources.Core;

namespace Nitrox.Server.Subnautica.Models.Resources.Parsers;

[TestClass]
public sealed class RandomStartResourceIntegrationTest
{
    private const string GamePathEnvironmentVariable = "SUBNAUTICA_GAME_PATH";
    private const string TextureBundleName = "essentials.unity_0ee8dd89ed55f05bc38a09cc77137d4e.bundle";

    [TestMethod]
    [TestCategory("Integration")]
    public async Task LoadAsync_ActualRandomStartTexture_ProducesValidStartPositions()
    {
        string? gamePath = Environment.GetEnvironmentVariable(GamePathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            gamePath = NitroxUser.GamePath;
        }
        if (string.IsNullOrWhiteSpace(gamePath) || !Directory.Exists(gamePath))
        {
            Assert.Inconclusive($"Set {GamePathEnvironmentVariable} to a Subnautica installation to run this integration test.");
            return;
        }

        ServerStartOptions optionsValue = new()
        {
            GamePath = gamePath,
            NitroxAssetsPath = AppContext.BaseDirectory
        };
        IOptions<ServerStartOptions> options = Options.Create(optionsValue);
        string bundlePath = Path.Combine(optionsValue.GetSubnauticaStandaloneResourcePath(), TextureBundleName);
        if (!File.Exists(bundlePath))
        {
            Assert.Inconclusive($"The RandomStart texture bundle was not found at '{bundlePath}'.");
            return;
        }

        string classPackagePath = Path.Combine(AppContext.BaseDirectory, "Resources", "classdata.tpk");
        Assert.IsTrue(File.Exists(classPackagePath), $"Required class package was not found at '{classPackagePath}'.");

        SubnauticaAssetsManager assetsManager = new(
            options,
            new ThreadSafeMonoCecilTempGenerator(options),
            NullLogger<SubnauticaAssetsManager>.Instance);
        RandomStartResource resource = new(assetsManager, options);

        try
        {
            await resource.LoadAsync(CancellationToken.None);

            RandomStartGenerator generator = await resource.GetRandomStartGeneratorAsync();
            List<NitroxVector3> startPositions = generator.GenerateAllStartPositions(new PixelGridRandom()).Take(3).ToList();

            startPositions.Should().HaveCount(3);
            startPositions.Should().OnlyContain(startPosition =>
                startPosition.Y == 0f
                && startPosition.X >= -2048f
                && startPosition.X < 2048f
                && startPosition.Z >= -2048f
                && startPosition.Z < 2048f);
            startPositions.Select(startPosition => (startPosition.X, startPosition.Z)).Distinct().Should().HaveCount(3);
        }
        finally
        {
            await resource.CleanupAsync();
        }
    }

    private sealed class PixelGridRandom : Random
    {
        private const int TextureSize = 512;
        private int sampleCount;

        // Sweep the generator's sample grid once so missing valid pixels fail without an unbounded loop.
        public override double NextDouble()
        {
            int pixelIndex = sampleCount / 2;
            if (pixelIndex >= TextureSize * TextureSize)
            {
                throw new InvalidOperationException("No valid start pixel was found in the RandomStart texture.");
            }

            int coordinate = sampleCount++ % 2 == 0
                ? pixelIndex % TextureSize
                : pixelIndex / TextureSize;
            return (coordinate + 0.5) / TextureSize;
        }
    }
}
