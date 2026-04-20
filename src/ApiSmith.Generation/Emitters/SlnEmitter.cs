using System.Globalization;
using ApiSmith.Config;
using ApiSmith.Core.Pipeline;
using ApiSmith.Generation.Architectures;
using ApiSmith.Generation.IO;

namespace ApiSmith.Generation.Emitters;

public static class SlnEmitter
{
    private const string CsharpSdkProjectTypeGuid = "9A19103F-16F7-4668-BE54-9A1E7A4F7556";

    public static EmittedFile Emit(ApiSmithConfig config, IArchitectureLayout layout)
    {
        var projects = layout.Projects(config);
        var projectTypeGuid = "{" + CsharpSdkProjectTypeGuid + "}";
        var slnGuid = StableGuid.From($"sln::{config.ProjectName}").ToString("B").ToUpperInvariant();

        var entries = projects
            .Select(p => new
            {
                Project = p,
                Guid = StableGuid.From($"project::{p.AssemblyName}").ToString("B").ToUpperInvariant(),
                Path = p.RelativeCsprojPath.Replace('/', '\\'),
            })
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        sb.AppendLine("# Visual Studio Version 17");

        foreach (var e in entries)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "Project(\"{0}\") = \"{1}\", \"{2}\", \"{3}\"",
                projectTypeGuid, e.Project.AssemblyName, e.Path, e.Guid).AppendLine();
            sb.AppendLine("EndProject");
        }

        sb.AppendLine("Global");
        sb.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        sb.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
        sb.AppendLine("\t\tRelease|Any CPU = Release|Any CPU");
        sb.AppendLine("\tEndGlobalSection");
        sb.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");

        foreach (var e in entries)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "\t\t{0}.Debug|Any CPU.ActiveCfg = Debug|Any CPU", e.Guid).AppendLine();
            sb.AppendFormat(CultureInfo.InvariantCulture, "\t\t{0}.Debug|Any CPU.Build.0 = Debug|Any CPU", e.Guid).AppendLine();
            sb.AppendFormat(CultureInfo.InvariantCulture, "\t\t{0}.Release|Any CPU.ActiveCfg = Release|Any CPU", e.Guid).AppendLine();
            sb.AppendFormat(CultureInfo.InvariantCulture, "\t\t{0}.Release|Any CPU.Build.0 = Release|Any CPU", e.Guid).AppendLine();
        }

        sb.AppendLine("\tEndGlobalSection");
        sb.AppendLine("\tGlobalSection(SolutionProperties) = preSolution");
        sb.AppendLine("\t\tHideSolutionNode = FALSE");
        sb.AppendLine("\tEndGlobalSection");
        sb.AppendLine("\tGlobalSection(ExtensibilityGlobals) = postSolution");
        sb.AppendFormat(CultureInfo.InvariantCulture, "\t\tSolutionGuid = {0}", slnGuid).AppendLine();
        sb.AppendLine("\tEndGlobalSection");
        sb.AppendLine("EndGlobal");

        return new EmittedFile(layout.SolutionPath(config), sb.ToString());
    }
}
