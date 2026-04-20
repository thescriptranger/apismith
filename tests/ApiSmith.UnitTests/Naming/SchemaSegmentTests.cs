using ApiSmith.Naming;

namespace ApiSmith.UnitTests.Naming;

public sealed class SchemaSegmentTests
{
    [Theory]
    [InlineData("dbo", "Dbo")]
    [InlineData("audit", "Audit")]
    [InlineData("HR", "Hr")]
    [InlineData("report_generation", "ReportGeneration")]
    public void ToPascal_segment_is_pascalized(string schema, string expected)
    {
        Assert.Equal(expected, SchemaSegment.ToPascal(schema));
    }
}
