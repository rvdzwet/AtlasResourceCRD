using System.Collections.Generic;
using AtlasResourceCRD.Core.Models;
using AtlasResourceCRD.Core.Validation;
using FluentAssertions;
using Xunit;

namespace AtlasResourceCRD.Tests;

public class SelfHealingTests
{
    [Fact]
    public void Validate_ShouldCatchMalformedMermaidDiagramHeader()
    {
        var resource = new AtlasResource
        {
            ApiVersion = "atlas.io/v1alpha1",
            Kind = "AtlasResource",
            Metadata = new AtlasResourceMetadata
            {
                Name = "test-component"
            },
            Spec = new AtlasResourceSpec
            {
                ComponentOverview = new ComponentOverview
                {
                    Name = "test-component",
                    Tier = "Backend"
                },
                Architecture = new ArchitectureSpec
                {
                    ComponentDiagram = "INVALID HEADER: Component A -> Component B"
                }
            }
        };

        var result = CrdValidator.Validate(resource);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("componentDiagram") && e.Contains("header"));
    }

    [Fact]
    public void Validate_ShouldPassForValidMultiDiagramSuite()
    {
        var resource = new AtlasResource
        {
            ApiVersion = "atlas.io/v1alpha1",
            Kind = "AtlasResource",
            Metadata = new AtlasResourceMetadata
            {
                Name = "test-component"
            },
            Spec = new AtlasResourceSpec
            {
                ComponentOverview = new ComponentOverview
                {
                    Name = "test-component",
                    Tier = "Backend"
                },
                Architecture = new ArchitectureSpec
                {
                    ContextDiagram = "flowchart TD\n  User --> System",
                    ComponentDiagram = "flowchart TD\n  API --> Service",
                    DataFlowDiagram = "flowchart LR\n  Sensor --> InfluxDB"
                }
            }
        };

        var result = CrdValidator.Validate(resource);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
