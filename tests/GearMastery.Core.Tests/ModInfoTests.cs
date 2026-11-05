using GearMastery.Core;

namespace GearMastery.Core.Tests;

public class ModInfoTests
{
    // tools\deploy.ps1 re-badges exactly this Id and Name in module\SubModule.xml; keep them in step.
    [Fact]
    public void ModuleIdMatchesManifest()
    {
        var manifest = File.ReadAllText(Path.Combine(RepoRoot(), "module", "SubModule.xml"));
        Assert.Contains("<Id value=\"" + ModInfo.ModuleId + "\" />", manifest);
        Assert.Contains("<Name value=\"" + ModInfo.DisplayName + "\" />", manifest);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "GearMastery.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("GearMastery.sln not found above " + AppContext.BaseDirectory);
    }
}
