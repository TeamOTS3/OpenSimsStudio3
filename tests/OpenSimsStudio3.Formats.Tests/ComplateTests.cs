using System.Xml.Linq;
using OpenSimsStudio3.Formats.Compositing;

namespace OpenSimsStudio3.Formats.Tests;

public class ComplateTests
{
    static Complate Make(string variables = "", params (string Key, string Value)[] preset) =>
        new(XElement.Parse($"<complate name=\"T\"><variables>{variables}</variables></complate>"),
            preset.Select(p => new XElement("value", new XAttribute("key", p.Key), new XAttribute("value", p.Value))));

    [Theory]
    [InlineData("{1+2*3}", "7")]
    [InlineData("{(1+2)*3}", "9")]
    [InlineData("{10/4}", "2.5")]
    [InlineData("{-2*-3}", "6")]
    [InlineData("{ 1 + 1 }", "2")]
    [InlineData("a{2*2}b{3-1}c", "a4b2c")]
    [InlineData("no maths", "no maths")]
    public void Resolve_Arithmetic(string input, string expected) => Assert.Equal(expected, Make().Resolve(input));

    [Fact]
    public void Resolve_UsesTemplateDefault_PresetOverrides()
    {
        const string vars = "<param name=\"Angle\" default=\"2\"/>";
        Assert.Equal("{2*3}".Length > 0 ? "6" : "", Make(vars).Resolve("{($Angle)*3}"));
        Assert.Equal("12", Make(vars, ("angle", "4")).Resolve("{($Angle)*3}")); // names match case-insensitively
    }

    [Fact]
    public void Resolve_UnknownReferenceBecomesEmpty_AndDaeFileNamePrefixVanishes()
    {
        var c = Make();
        Assert.Equal("", c.Resolve("($Missing)"));
        Assert.Equal("Pattern A", c.Resolve("($daeFileName)Pattern A"));
    }

    [Fact]
    public void Resolve_NestedReferences()
    {
        var c = Make("<param name=\"A\" default=\"($B)\"/><param name=\"B\" default=\"5\"/>");
        Assert.Equal("5", c.Resolve("($A)"));
    }

    [Fact]
    public void Attr_IsCaseInsensitive_AndResolves()
    {
        var c = Make("", ("Size", "8"));
        var step = XElement.Parse("<step Width=\"($Size)\"/>");
        Assert.Equal("8", c.Attr(step, "width"));
        Assert.Null(c.Attr(step, "height"));
    }

    [Fact]
    public void Bool_And_Float_FallBackWhenMissingOrInvalid()
    {
        var c = Make();
        var step = XElement.Parse("<step on=\"true\" bad=\"x\" f=\"1.5, 2\"/>");
        Assert.True(c.Bool(step, "on", false));
        Assert.True(c.Bool(step, "bad", true));
        Assert.False(c.Bool(step, "missing", false));
        Assert.Equal(1.5f, c.Float(step, "f", 0f));
        Assert.Equal(9f, c.Float(step, "missing", 9f));
    }

    [Fact]
    public void Floats_ParsesList_BadItemsAreZero() =>
        Assert.Equal([1f, 0f, 2.5f], Complate.Floats("1, x ,2.5"));

    [Fact]
    public void ParseTemplate_AcceptsBom() =>
        Assert.Equal("T", new Complate(Complate.ParseTemplate([0xEF, 0xBB, 0xBF, .. "<complate name=\"T\"/>"u8.ToArray()]), []).Name);
}
