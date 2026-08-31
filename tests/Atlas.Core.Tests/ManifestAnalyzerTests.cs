using System.IO;
using Atlas.Core.Scanner;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Atlas.Tests;

public class ManifestAnalyzerTests
{
    private readonly ManifestAnalyzer _analyzer = new(NullLogger<ManifestAnalyzer>.Instance);

    [Fact]
    public void AnalyzeCsproj_ShouldExtractTargetFrameworkAndPackages()
    {
        var tempFile = Path.GetTempFileName() + ".csproj";
        const string xml = """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="YamlDotNet" Version="18.1.0" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="10.0.11" />
  </ItemGroup>
</Project>
""";
        File.WriteAllText(tempFile, xml);

        try
        {
            var result = _analyzer.Analyze("test.csproj", tempFile);

            result.Should().NotBeNull();
            result!.ManifestType.Should().Be("DotNetCsproj");
            result.TargetRuntime.Should().Be("net10.0");
            result.ExtractedPackages.Should().HaveCount(2);
            result.ExtractedPackages.Should().Contain(p => p.Name == "YamlDotNet" && p.Version == "18.1.0");
            result.ExtractedPackages.Should().Contain(p => p.Name == "Microsoft.Extensions.Logging" && p.Version == "10.0.11");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void AnalyzePackageJson_ShouldExtractDependencies()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "package.json");
        const string json = """
{
  "name": "my-frontend-app",
  "version": "1.0.0",
  "dependencies": {
    "react": "^18.2.0",
    "axios": "^1.6.0"
  }
}
""";
        File.WriteAllText(tempFile, json);

        try
        {
            var result = _analyzer.Analyze("package.json", tempFile);

            result.Should().NotBeNull();
            result!.ManifestType.Should().Be("NodePackageJson");
            result.ExtractedPackages.Should().HaveCount(2);
            result.ExtractedPackages.Should().Contain(p => p.Name == "react" && p.Version == "^18.2.0");
            result.ExtractedPackages.Should().Contain(p => p.Name == "axios" && p.Version == "^1.6.0");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void AnalyzeDockerfile_ShouldExtractPortsAndEnv()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "Dockerfile");
        const string dockerfile = """
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 8080 8443
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "App.dll"]
""";
        File.WriteAllText(tempFile, dockerfile);

        try
        {
            var result = _analyzer.Analyze("Dockerfile", tempFile);

            result.Should().NotBeNull();
            result!.ManifestType.Should().Be("Dockerfile");
            result.ExposedPorts.Should().Contain(new[] { "8080", "8443" });
            result.EnvironmentVariables.Should().Contain("ASPNETCORE_ENVIRONMENT");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
