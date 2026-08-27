using System.Collections.Generic;
using AtlasResourceCRD.Core.Validation;
using FluentAssertions;
using Xunit;

namespace AtlasResourceCRD.Tests;

public class MermaidValidatorTests
{
    [Fact]
    public void Validate_ShouldPass_ForValidFlowchart()
    {
        var diagram = """
flowchart TD
  subgraph Users ["Users"]
    User["End User"]
  end
  subgraph Backend ["Core System"]
    API["REST API"]
  end
  User -->|"HTTPS GET"| API
""";

        var result = MermaidValidator.Validate(diagram);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldDetectUnbalancedSubgraphs()
    {
        var broken = """
flowchart TD
  subgraph GroupA ["Group A"]
    NodeA["A"]
  subgraph GroupB ["Group B"]
    NodeB["B"]
  end
""";

        // Sanitize auto-fixes missing end tags
        var sanitized = MermaidValidator.Sanitize(broken);
        var result = MermaidValidator.Validate(sanitized);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Sanitize_ShouldReplaceArrowSymbolsInsideLabels()
    {
        var raw = "flowchart TD\n  Node1[\"Step 1 -> Step 2\"]\n  Node2[\"Step 2 --> Step 3\"]";
        var sanitized = MermaidValidator.Sanitize(raw);

        sanitized.Should().Contain("Step 1 → Step 2");
        sanitized.Should().Contain("Step 2 → Step 3");
        sanitized.Should().NotContain("->");
    }

    [Fact]
    public void GenerateFallbackDiagram_ShouldProduceValidFlowchart()
    {
        var fallback = MermaidValidator.GenerateFallbackDiagram(
            "Fallback Architecture",
            new List<string> { "Module A", "Module B", "Module C" });

        fallback.Should().Contain("flowchart TD");
        fallback.Should().Contain("Module A");
        var result = MermaidValidator.Validate(fallback);
        result.IsValid.Should().BeTrue();
    }
}
