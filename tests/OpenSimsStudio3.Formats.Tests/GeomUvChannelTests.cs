using System.Numerics;
using OpenSimsStudio3.Formats.Meshes;
using OpenSimsStudio3.Formats.Projects;
using s3pi.GenericRCOLResource;
using GEOM = meshExpImp.ModelBlocks.GEOM;

namespace OpenSimsStudio3.Formats.Tests;

/// <summary>
/// UV channels beyond the first must survive reading, re-writing and loading a GEOM. The GEOM is written byte by byte
/// here (not through s3pi), so the test does not share code with the thing it checks.
/// </summary>
public class GeomUvChannelTests
{
    const int VertexCount = 4;

    // Distinct values per vertex and channel, so a copied or dropped channel cannot pass by accident.
    static Vector2 Uv(int channel, int vertex) => new(0.125f * (channel + 1) + 0.03125f * vertex, 0.5f - 0.0625f * (channel + 1) - 0.015625f * vertex);

    static Vector3 Position(int vertex) => new(vertex, vertex * 0.5f, -vertex);

    /// <summary>A GEOM (shader 0, so no MTNF) as a single-chunk RCOL, with <paramref name="uvChannels" /> UV usages in the vertex format.</summary>
    static byte[] BuildGeomRcol(int uvChannels)
    {
        var g = new MemoryStream();
        var w = new BinaryWriter(g);
        w.Write(0x4D4F4547u); // 'GEOM'
        w.Write(5u);
        var tgiOffsetAt = g.Position;
        w.Write(0u); // offset from the size field to the TGI list
        w.Write(0u); // TGI size
        w.Write(0u); // shader 0: no MTNF
        w.Write(0u); // merge group
        w.Write(0u); // sort order
        w.Write(VertexCount);
        var formats = new List<(uint Usage, uint Type, byte Size)> { (1, 1, 12), (2, 1, 12) };
        for (var c = 0; c < uvChannels; c++) formats.Add((3, 1, 8));
        w.Write(formats.Count);
        foreach (var (usage, type, size) in formats) { w.Write(usage); w.Write(type); w.Write(size); }
        for (var v = 0; v < VertexCount; v++)
        {
            var p = Position(v);
            w.Write(p.X); w.Write(p.Y); w.Write(p.Z);
            w.Write(0f); w.Write(1f); w.Write(0f);
            for (var c = 0; c < uvChannels; c++) { var uv = Uv(c, v); w.Write(uv.X); w.Write(uv.Y); }
        }
        w.Write(1); w.Write((byte)2); // one face-point size, 2 bytes
        w.Write(6); // face point count
        foreach (ushort i in new ushort[] { 0, 1, 2, 0, 2, 3 }) w.Write(i);
        w.Write(0); // skin index
        w.Write(0); // bone hashes
        var tgiAt = g.Position;
        w.Write(0); // empty TGI list
        g.Position = tgiOffsetAt;
        w.Write((uint)(tgiAt - (tgiOffsetAt + 4)));
        w.Write(4u); // size of the empty list (its count)
        var chunk = g.ToArray();

        var rcol = new MemoryStream();
        var r = new BinaryWriter(rcol);
        r.Write(1u); r.Write(0); r.Write(0u); r.Write(0); r.Write(1); // version, public chunks, unused, 0 external, 1 chunk
        r.Write(0x1234UL); r.Write(0u); r.Write(0u);                 // chunk key (instance, type 0 like EA GEOMs, group)
        r.Write(0x2Cu); r.Write(chunk.Length);                        // index entry
        r.Write(chunk);
        return rcol.ToArray();
    }

    static GEOM ReadGeom(byte[] bytes) =>
        new GenericRCOLResource(0, new MemoryStream(bytes)).ChunkEntries.Select(c => c.RCOLBlock).OfType<GEOM>().Single();

    static List<Vector2>[] UvChannels(GEOM geom, int channels)
    {
        var sets = Enumerable.Range(0, channels).Select(_ => new List<Vector2>()).ToArray();
        foreach (var vertex in geom.VertexData)
        {
            var uvs = vertex.Vertex.OfType<GEOM.UVElement>().ToList();
            Assert.Equal(channels, uvs.Count);
            for (var c = 0; c < channels; c++) sets[c].Add(new Vector2(uvs[c].U, uvs[c].V));
        }
        return sets;
    }

    static void AssertChannels(IReadOnlyList<List<Vector2>> sets)
    {
        for (var c = 0; c < sets.Count; c++)
            for (var v = 0; v < VertexCount; v++)
                Assert.Equal(Uv(c, v), sets[c][v]);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RoundTrip_ReadWriteRead_KeepsEveryUvChannel(int channels)
    {
        var original = BuildGeomRcol(channels);
        AssertChannels(UvChannels(ReadGeom(original), channels));

        var written = new GenericRCOLResource(0, new MemoryStream(original)).AsBytes;
        Assert.Equal(original, written); // unedited: byte-exact
        AssertChannels(UvChannels(ReadGeom(written), channels));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void RoundTrip_AfterEditingUv0_KeepsOtherChannels(int channels)
    {
        // Forces s3pi to rebuild the vertex element lists instead of replaying the stored bytes.
        var rcol = new GenericRCOLResource(0, new MemoryStream(BuildGeomRcol(channels)));
        var geom = rcol.ChunkEntries.Select(c => c.RCOLBlock).OfType<GEOM>().Single();
        foreach (var vertex in geom.VertexData)
            vertex.Vertex = new GEOM.ElementList(null, vertex.Vertex.ToList(), geom.VertexFormats);
        AssertChannels(UvChannels(ReadGeom(rcol.AsBytes), channels));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Load_ExposesEveryUvChannel(int channels)
    {
        var mesh = GeomLoader.Read(new MemoryStream(BuildGeomRcol(channels)), "synthetic");
        Assert.Equal(VertexCount, mesh.Positions.Length);
        Assert.Equal(channels, mesh.UvSetCount);
        for (var c = 0; c < channels; c++)
        {
            var set = mesh.UvSet(c);
            Assert.Equal(VertexCount, set.Length);
            for (var v = 0; v < VertexCount; v++) Assert.Equal(Uv(c, v), set[v]);
        }
        Assert.Equal(mesh.UvSet(0), mesh.Uvs);
    }

    [Fact]
    public void Load_NoUvs_HasNoSets()
    {
        var mesh = GeomLoader.Read(new MemoryStream(BuildGeomRcol(0)), "synthetic");
        Assert.Equal(0, mesh.UvSetCount);
        Assert.Empty(mesh.Uvs);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Export_TangentRulePath_KeepsEveryUvChannel(int channels)
    {
        // MeshExchange.ApplyTangentRules reads two GEOMs and returns the re-written one: the import/export write path.
        var bytes = BuildGeomRcol(channels);
        var (written, _, _) = MeshExchange.ApplyTangentRules(bytes, bytes);
        AssertChannels(UvChannels(ReadGeom(written), channels));
    }

    [Fact]
    public void Merge_KeepsUvSetsWhenEveryPartHasThem()
    {
        var a = GeomLoader.Read(new MemoryStream(BuildGeomRcol(3)), "a");
        var b = GeomLoader.Read(new MemoryStream(BuildGeomRcol(3)), "b");
        var merged = GeomMesh.Merge("m", [a, b]);
        Assert.Equal(3, merged.UvSetCount);
        for (var c = 0; c < 3; c++) Assert.Equal(a.UvSet(c).Concat(b.UvSet(c)), merged.UvSet(c));
    }
}
