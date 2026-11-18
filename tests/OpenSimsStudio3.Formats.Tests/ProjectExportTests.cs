using OpenSimsStudio3.Formats.Install;
using OpenSimsStudio3.Formats.Projects;
using s3pi.Interfaces;

namespace OpenSimsStudio3.Formats.Tests;

public class ProjectExportTests
{
    // Each project is a folder with project.json and a package of made-up resources (type 0x1234, arbitrary bytes).
    static string MakeProject(TempDir root, string name, params ulong[] instances)
    {
        var folder = root.Sub(name);
        new Project { Id = name, Name = name, Package = "p.package" }.Write(folder);
        var package = s3pi.Package.Package.NewPackage(0);
        try
        {
            foreach (var i in instances)
                package.AddResource(new TGIBlock(0, null, 0x1234, 0, i), new MemoryStream([(byte)i, 1, 2, 3]), true);
            package.SaveAs(Path.Combine(folder, "p.package"));
        }
        finally { s3pi.Package.Package.ClosePackage(0, package); }
        return folder;
    }

    static List<ResourceKey> KeysOf(string path)
    {
        var p = s3pi.Package.Package.OpenPackage(0, path, false);
        try { return p.GetResourceList.Select(ResourceKey.Of).ToList(); }
        finally { s3pi.Package.Package.ClosePackage(0, p); }
    }

    [Fact]
    public void Export_MergesDistinctProjects()
    {
        using var dir = new TempDir();
        var a = MakeProject(dir, "a", 1, 2);
        var b = MakeProject(dir, "b", 3);
        var outPath = Path.Combine(dir.Path, "out", "merged.package");

        var result = ProjectExport.Export([a, b], outPath);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.Resources);
        Assert.Equal(["a", "b"], result.Projects);
        Assert.Equal(new ulong[] { 1, 2, 3 }, KeysOf(outPath).Select(k => k.Instance).Order().ToArray());
    }

    [Fact]
    public void Export_SameKeyInTwoProjects_ReportsCollision_AndWritesNothing()
    {
        using var dir = new TempDir();
        var a = MakeProject(dir, "a", 1, 2);
        var b = MakeProject(dir, "b", 2);
        var outPath = Path.Combine(dir.Path, "merged.package");

        var result = ProjectExport.Export([a, b], outPath);

        Assert.False(result.Succeeded);
        Assert.Equal(0, result.Resources);
        var problem = Assert.Single(result.Problems);
        Assert.Contains("\"a\"", problem);
        Assert.Contains("\"b\"", problem);
        Assert.False(File.Exists(outPath));
    }

    [Fact]
    public void Export_OutputIsAProjectsOwnPackage_IsRefused()
    {
        using var dir = new TempDir();
        var a = MakeProject(dir, "a", 1);

        var result = ProjectExport.Export([a], Path.Combine(a, "p.package"));

        Assert.False(result.Succeeded);
        Assert.Contains("overwrite", Assert.Single(result.Problems));
    }

    [Fact]
    public void Export_UnreadableProject_IsReportedNotThrown()
    {
        using var dir = new TempDir();
        var missing = dir.Sub("empty");

        var result = ProjectExport.Export([missing], Path.Combine(dir.Path, "o.package"));

        Assert.False(result.Succeeded);
        Assert.False(File.Exists(Path.Combine(dir.Path, "o.package")));
    }
}
