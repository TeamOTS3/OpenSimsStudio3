using OpenSimsStudio3.Formats.Projects;

namespace OpenSimsStudio3.Formats.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("oss3-tests-").FullName;
    public string Sub(string name) => Directory.CreateDirectory(System.IO.Path.Combine(Path, name)).FullName;
    public void Dispose() => Directory.Delete(Path, true);
}

public class ProjectTests
{
    [Fact]
    public void WriteThenRead_RoundTripsEveryField()
    {
        using var dir = new TempDir();
        var created = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var project = new Project
        {
            Id = "abc", Name = "My recolour", Target = "Both", Kind = "CasClone", SourceKey = "key", SourceName = "src",
            SourcePackage = "game.package", Casp = "casp", Package = "mine.package", SourceProduct = 3, Created = created, Modified = created.AddDays(1),
        };
        project.Write(dir.Path);
        Assert.Equal(project, Project.Read(dir.Path));
    }

    [Fact]
    public void Read_NewerFormat_Throws()
    {
        using var dir = new TempDir();
        new Project { Format = Project.CurrentFormat + 1 }.Write(dir.Path);
        Assert.Throws<InvalidDataException>(() => Project.Read(dir.Path));
    }

    [Fact]
    public void Read_MissingFile_Throws()
    {
        using var dir = new TempDir();
        Assert.ThrowsAny<IOException>(() => Project.Read(dir.Path));
    }

    [Fact]
    public void Read_NullJson_Throws()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, Project.FileName), "null");
        Assert.Throws<InvalidDataException>(() => Project.Read(dir.Path));
    }
}
