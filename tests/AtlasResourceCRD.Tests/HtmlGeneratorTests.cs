using System.Collections.Generic;
using AtlasResourceCRD.Core.Html;
using AtlasResourceCRD.Core.Models;
using FluentAssertions;
using Xunit;

namespace AtlasResourceCRD.Tests;

public class HtmlGeneratorTests
{
    [Fact]
    public void Generate_ShouldProduceValidHtmlWithMultiDiagramTabsAndControls()
    {
        var resource = new AtlasResource
        {
            ApiVersion = "atlas.io/v1alpha1",
            Kind = "AtlasResource",
            Metadata = new AtlasResourceMetadata
            {
                Name = "iot-engine",
                Namespace = "production",
                Annotations = new Dictionary<string, string>
                {
                    ["atlas.io/git-commit-short"] = "96d091c",
                    ["atlas.io/git-branch"] = "master"
                }
            },
            Spec = new AtlasResourceSpec
            {
                ComponentOverview = new ComponentOverview
                {
                    Name = "RoMars.IoT.Engine",
                    Description = "Smart home IoT automation platform",
                    Tier = "Backend",
                    Purpose = "Home Automation"
                },
                TechStack = new TechStack
                {
                    PrimaryLanguage = "C#",
                    Languages = new List<TechItem> { new() { Name = "C#", Version = "13" } }
                },
                Architecture = new ArchitectureSpec
                {
                    Summary = "Modular monolith architecture.",
                    Pattern = "Modular Monolith",
                    ContextDiagram = "flowchart TD\n  User --> System",
                    ComponentDiagram = "flowchart TD\n  API --> Engine",
                    DataFlowDiagram = "flowchart LR\n  Sensor --> InfluxDB",
                    Components = new List<ArchComponent>
                    {
                        new() { Name = "Engine", Type = "Core", Description = "Main orchestrator" }
                    }
                },
                ApiContracts = new ApiContractsSpec
                {
                    Endpoints = new List<ApiEndpoint>
                    {
                        new() { Path = "/api/items", Method = "GET", Description = "Get all items", AuthRequired = false }
                    }
                }
            }
        };

        var html = HtmlVisualizerGenerator.Generate(resource);

        html.Should().Contain("<!DOCTYPE html>");
        html.Should().Contain("iot-engine - Atlas Architecture");
        html.Should().Contain("mermaid.min.js");
        html.Should().Contain("diag-component");
        html.Should().Contain("diag-context");
        html.Should().Contain("diag-dataflow");
        html.Should().Contain("zoomDiagram");
        html.Should().Contain("toggleFullscreen");
        html.Should().Contain("inspectorDrawer");
        html.Should().Contain("/api/items");
        html.Should().Contain("96d091c");
        html.Should().Contain("copyCrdYaml");
    }
}
