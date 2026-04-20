namespace ApiSmith.Generation;

public sealed record ScaffoldReport(
    int FileCount,
    int TableCount,
    System.TimeSpan IntrospectDuration,
    System.TimeSpan PlanDuration,
    System.TimeSpan RenderDuration,
    System.TimeSpan WriteDuration,
    System.TimeSpan TotalDuration);
