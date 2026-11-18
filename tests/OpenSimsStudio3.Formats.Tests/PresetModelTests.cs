using OpenSimsStudio3.Formats.Cas;
using OpenSimsStudio3.Formats.Install;

namespace OpenSimsStudio3.Formats.Tests;

public class PresetModelTests
{
    // Made up for these tests: same shape as a CasRgbMask preset, none of its content.
    const string Xml =
        """<preset><complate name="TestComplate">""" +
        """<value key="Multiplier" value="key:00B2D882:00000000:0000000000000001" />""" +
        """<value key="Mask" value="key:00B2D882:0000000A:00000000000000FF" />""" +
        """<value key="Pattern A" value="pattern_a" /><value key="Pattern A Enabled" value="True" /><value key="Pattern A Tiling" value="1,1" />""" +
        """<value key="Pattern B" value="pattern_b" /><value key="Pattern B Enabled" value="False" />""" +
        """<value key="Logo Enabled" value="false" /><value key="Logo" value="key:00B2D882:00000000:0000000000000002" />""" +
        """<value key="Tip Color" value="0.1,0.2,0.3,1" /><value key="Name" value="not a colour" />""" +
        """<pattern variable="Pattern A"><value key="Color" value="1,0.5,0,1" /></pattern>""" +
        """<pattern variable="Pattern B" />""" +
        """</complate></preset>""";

    [Fact]
    public void ParseThenToXml_RoundTripsCompactXml() => Assert.Equal(Xml, PresetModel.Parse(Xml).ToXml());

    [Fact]
    public void Parse_KeepsTextBeforeRoot()
    {
        var declared = "<?xml version=\"1.0\"?>\n" + Xml;
        Assert.Equal(declared, PresetModel.Parse(declared).ToXml());
    }

    [Fact]
    public void Parse_WithoutComplate_Throws() =>
        Assert.Throws<InvalidDataException>(() => PresetModel.Parse("<preset/>"));

    [Fact]
    public void Complate_IsReadFromName() => Assert.Equal("TestComplate", PresetModel.Parse(Xml).Complate);

    [Fact]
    public void Textures_ListsOnlyImgKeys_WithParsedKey()
    {
        var textures = PresetModel.Parse(Xml).Textures;
        Assert.Equal(["Multiplier", "Mask", "Logo"], textures.Select(t => t.Name));
        Assert.Equal(new ResourceKey(0x00B2D882, 0xA, 0xFF), textures[1].Key);
    }

    [Fact]
    public void Channels_ReportEnabledPatternAndColour()
    {
        var channels = PresetModel.Parse(Xml).Channels;
        Assert.Equal(2, channels.Count);
        Assert.True(channels[0].Enabled);
        Assert.Equal([1f, 0.5f, 0f, 1f], channels[0].Color);
        Assert.Equal("1,1", channels[0].Tiling);
        Assert.False(channels[1].Enabled);
        Assert.Null(channels[1].Color);
    }

    [Fact]
    public void Colors_ListsOnlyColourValues()
    {
        var colors = PresetModel.Parse(Xml).Colors;
        var only = Assert.Single(colors);
        Assert.Equal("Tip Color", only.Item1);
        Assert.Equal([0.1f, 0.2f, 0.3f, 1f], only.Item2);
    }

    [Fact]
    public void SetColor_ClampsAndFormats()
    {
        var m = PresetModel.Parse(Xml);
        m.SetColor("Tip Color", [2f, -1f, 0.5f, 1f]);
        Assert.Contains("""key="Tip Color" value="1.0000,0.0000,0.5000,1.0000" """.TrimEnd(), m.ToXml());
    }

    [Fact]
    public void SetColor_UnknownKey_Throws() =>
        Assert.Throws<KeyNotFoundException>(() => PresetModel.Parse(Xml).SetColor("Nope", [0, 0, 0]));

    [Fact]
    public void SetChannelColor_OnlyWhenPatternHasColour()
    {
        var m = PresetModel.Parse(Xml);
        Assert.True(m.SetChannelColor("Pattern A", [0, 1, 0, 1]));
        Assert.Equal([0f, 1f, 0f, 1f], m.Channels[0].Color);
        Assert.False(m.SetChannelColor("Pattern B", [0, 1, 0, 1]));
    }

    [Fact]
    public void SetChannelEnabled_Toggles()
    {
        var m = PresetModel.Parse(Xml);
        m.SetChannelEnabled("Pattern B", true);
        Assert.True(m.Channels[1].Enabled);
    }

    [Fact]
    public void IsTextureUsed_FollowsEnabledFlag()
    {
        var m = PresetModel.Parse(Xml);
        Assert.False(m.IsTextureUsed("Logo"));
        Assert.True(m.IsTextureUsed("Mask"));
    }

    [Fact]
    public void SetTexture_WritesKeyThatReadsBack()
    {
        var m = PresetModel.Parse(Xml);
        var key = new ResourceKey(0x00B2D882, 0x12, 0xABCDEF0123456789);
        m.SetTexture("Mask", key);
        Assert.Equal(key, m.Textures.Single(t => t.Name == "Mask").Key);
    }
}
