using ApiSmith.Config;

namespace ApiSmith.Generation.Architectures;

public static class LayoutFactory
{
    public static IArchitectureLayout Create(ArchitectureStyle style) => style switch
    {
        ArchitectureStyle.Flat          => new FlatLayout(),
        ArchitectureStyle.Clean         => new CleanLayout(),
        ArchitectureStyle.VerticalSlice => new VerticalSliceLayout(),
        ArchitectureStyle.Layered       => new LayeredLayout(),
        ArchitectureStyle.Onion         => new OnionLayout(),
        _ => throw new System.NotSupportedException($"Unknown architecture style: {style}."),
    };
}
