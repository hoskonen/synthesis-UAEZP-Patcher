using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;

namespace UAEZPSynthesisPatcher.Tests;

[TestClass]
public sealed class OutputModConfigurationTests
{
    [TestMethod]
    [DataRow("Synthesis.esp")]
    [DataRow("UAEZP-FWMF-Patch.esp")]
    public void ConfiguresExistingSynthesisOutputAsLightPlugin(
        string fileName)
    {
        var output = new SkyrimMod(
            ModKey.FromFileName(fileName),
            SkyrimRelease.SkyrimSE);
        ModKey originalModKey = output.ModKey;

        Program.ConfigureOutputMod(output);

        Assert.AreEqual(originalModKey, output.ModKey);
        Assert.IsTrue(output.ModHeader.Flags.HasFlag(
            SkyrimModHeader.HeaderFlag.Small));
    }
}
