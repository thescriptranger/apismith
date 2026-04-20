using ApiSmith.Config;

namespace ApiSmith.Generation.Architectures;

/// <summary>csproj snippets shared across architectures; bump versions here.</summary>
internal static class CsprojTemplates
{
    public const string AspNetCoreOpenApiVersion = "9.0.0";
    public const string EfCoreVersion = "9.0.0";
    public const string ScalarAspNetCoreVersion = "2.0.4";
    public const string SqlClientVersion = "5.2.2";
    public const string DapperVersion = "2.1.66";

    public static string WebProject(ApiSmithConfig config, string rootNamespace, string assemblyName, string projectReferences)
    {
        var extra = ExtraApiPackageReferences(config);
        return $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">

              <PropertyGroup>
                <TargetFramework>{config.TargetFramework}</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <LangVersion>latest</LangVersion>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <WarningsAsErrors />
                <NoWarn>$(NoWarn);CS1591</NoWarn>
                <RootNamespace>{rootNamespace}</RootNamespace>
                <AssemblyName>{assemblyName}</AssemblyName>
                <InvariantGlobalization>false</InvariantGlobalization>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="{AspNetCoreOpenApiVersion}" />
                <PackageReference Include="Scalar.AspNetCore" Version="{ScalarAspNetCoreVersion}" />{extra}
              </ItemGroup>
            {projectReferences}
            </Project>
            """;
    }

    private static string ExtraApiPackageReferences(ApiSmithConfig config)
    {
        var refs = Emitters.AuthEmitter.CsprojPackageRefs(config).ToList();
        if (refs.Count == 0)
        {
            return string.Empty;
        }

        return "\n    " + string.Join("\n    ", refs);
    }

    public static string WebProjectWithEfCore(ApiSmithConfig config, string rootNamespace, string assemblyName, string projectReferences)
    {
        var extra = ExtraApiPackageReferences(config);
        return $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">

              <PropertyGroup>
                <TargetFramework>{config.TargetFramework}</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <LangVersion>latest</LangVersion>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <WarningsAsErrors />
                <NoWarn>$(NoWarn);CS1591</NoWarn>
                <RootNamespace>{rootNamespace}</RootNamespace>
                <AssemblyName>{assemblyName}</AssemblyName>
                <InvariantGlobalization>false</InvariantGlobalization>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="{AspNetCoreOpenApiVersion}" />
                <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="{EfCoreVersion}" />
                <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="{EfCoreVersion}">
                  <PrivateAssets>all</PrivateAssets>
                  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
                </PackageReference>
                <PackageReference Include="Scalar.AspNetCore" Version="{ScalarAspNetCoreVersion}" />{extra}
              </ItemGroup>
            {projectReferences}
            </Project>
            """;
    }

    public static string WebProjectWithDapper(ApiSmithConfig config, string rootNamespace, string assemblyName, string projectReferences)
    {
        var extra = ExtraApiPackageReferences(config);
        return $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">

              <PropertyGroup>
                <TargetFramework>{config.TargetFramework}</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <LangVersion>latest</LangVersion>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <WarningsAsErrors />
                <NoWarn>$(NoWarn);CS1591</NoWarn>
                <RootNamespace>{rootNamespace}</RootNamespace>
                <AssemblyName>{assemblyName}</AssemblyName>
                <InvariantGlobalization>false</InvariantGlobalization>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="{AspNetCoreOpenApiVersion}" />
                <PackageReference Include="Microsoft.Data.SqlClient" Version="{SqlClientVersion}" />
                <PackageReference Include="Dapper" Version="{DapperVersion}" />
                <PackageReference Include="Scalar.AspNetCore" Version="{ScalarAspNetCoreVersion}" />{extra}
              </ItemGroup>
            {projectReferences}
            </Project>
            """;
    }

    public static string ClassLibrary(ApiSmithConfig config, string rootNamespace, string assemblyName, string projectReferences, bool withEfCore = false, bool withDapper = false)
    {
        var items = new System.Text.StringBuilder();
        if (withEfCore)
        {
            items.Append($"""


              <ItemGroup>
                <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="{EfCoreVersion}" />
              </ItemGroup>
            """);
        }
        if (withDapper)
        {
            items.Append($"""


              <ItemGroup>
                <PackageReference Include="Microsoft.Data.SqlClient" Version="{SqlClientVersion}" />
                <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="{AspNetCoreOpenApiVersion}" />
                <PackageReference Include="Dapper" Version="{DapperVersion}" />
              </ItemGroup>
            """);
        }
        var efItems = items.ToString();

        return $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>{config.TargetFramework}</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <LangVersion>latest</LangVersion>
                <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                <WarningsAsErrors />
                <NoWarn>$(NoWarn);CS1591</NoWarn>
                <RootNamespace>{rootNamespace}</RootNamespace>
                <AssemblyName>{assemblyName}</AssemblyName>
              </PropertyGroup>
            {efItems}{projectReferences}
            </Project>
            """;
    }

    public static string SharedClassLibrary(ApiSmithConfig config) => $"""
        <Project Sdk="Microsoft.NET.Sdk">

          <PropertyGroup>
            <TargetFramework>{config.TargetFramework}</TargetFramework>
            <LangVersion>latest</LangVersion>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
            <WarningsAsErrors />
            <NoWarn>$(NoWarn);CS1591</NoWarn>
            <IsPackable>true</IsPackable>
            <PackageId>{config.ProjectName}.Shared</PackageId>
            <Version>1.0.0</Version>
            <Description>API contracts for {config.ProjectName}.</Description>
            <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
            <!-- Optional: fill in before publishing -->
            <!-- <PackageProjectUrl>https://github.com/org/repo</PackageProjectUrl> -->
            <!-- <PackageLicenseExpression>MIT</PackageLicenseExpression> -->
            <!-- <RepositoryUrl>https://github.com/org/repo.git</RepositoryUrl> -->
            <!-- <Authors>Your Name</Authors> -->
            <!-- <Company>Your Company</Company> -->
          </PropertyGroup>

        </Project>
        """;

    public static string ProjectReferencesBlock(params string[] relativePaths)
    {
        if (relativePaths.Length == 0)
        {
            return string.Empty;
        }

        var lines = string.Join("\n    ", relativePaths.Select(p => $"<ProjectReference Include=\"{p}\" />"));
        return $"""


              <ItemGroup>
                {lines}
              </ItemGroup>
            """;
    }
}
