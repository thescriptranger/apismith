using ApiSmith.Console.Wizard;

namespace ApiSmith.UnitTests.Console;

public sealed class InstalledSdkProbeTests
{
    [Fact]
    public void Ga_versions_sort_descending_then_previews_last()
    {
        var stdout = """
            8.0.404 [/usr/share/dotnet/sdk]
            9.0.100 [/usr/share/dotnet/sdk]
            10.0.100-preview.2 [/usr/share/dotnet/sdk]
            """;
        var tfms = InstalledSdkProbe.ParseListSdks(stdout);
        Assert.Equal(new[] { "net9.0", "net8.0", "net10.0" }, tfms);
    }

    [Fact]
    public void Ga_overrides_preview_when_both_present_for_same_major_minor()
    {
        var stdout = """
            10.0.100-preview.2 [/usr/share/dotnet/sdk]
            10.0.200 [/usr/share/dotnet/sdk]
            9.0.100 [/usr/share/dotnet/sdk]
            """;
        var tfms = InstalledSdkProbe.ParseListSdks(stdout);
        Assert.Equal(new[] { "net10.0", "net9.0" }, tfms);
    }

    [Fact]
    public void Duplicate_patches_within_same_tfm_collapse()
    {
        var stdout = """
            8.0.404 [/usr/share/dotnet/sdk]
            8.0.300 [/opt/dotnet/sdk]
            """;
        var tfms = InstalledSdkProbe.ParseListSdks(stdout);
        Assert.Equal(new[] { "net8.0" }, tfms);
    }

    [Fact]
    public void Empty_stdout_returns_empty_array()
    {
        Assert.Empty(InstalledSdkProbe.ParseListSdks(""));
        Assert.Empty(InstalledSdkProbe.ParseListSdks("\n\n\n"));
    }

    [Fact]
    public void Malformed_lines_are_ignored()
    {
        var stdout = """
            not-a-version [path]
            8.0 [path]
            8.0.404 [path]
            """;
        var tfms = InstalledSdkProbe.ParseListSdks(stdout);
        Assert.Equal(new[] { "net8.0" }, tfms);
    }
}
