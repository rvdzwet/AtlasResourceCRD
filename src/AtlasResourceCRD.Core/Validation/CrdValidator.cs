using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AtlasResourceCRD.Core.Models;

namespace AtlasResourceCRD.Core.Validation;

public sealed class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
}

public static class CrdValidator
{
    private static readonly Regex K8sNameRegex = new(@"^[a-z0-9]([-a-z0-9]*[a-z0-9])?(\.[a-z0-9]([-a-z0-9]*[a-z0-9])?)*$", RegexOptions.Compiled);

    public static ValidationResult Validate(AtlasResource? resource)
    {
        var result = new ValidationResult();

        if (resource == null)
        {
            result.Errors.Add("Resource is null.");
            return result;
        }

        // ApiVersion & Kind
        if (string.IsNullOrWhiteSpace(resource.ApiVersion))
        {
            result.Errors.Add("Missing required field: 'apiVersion'. Expected 'atlas.io/v1alpha1'.");
        }
        else if (resource.ApiVersion != "atlas.io/v1alpha1")
        {
            result.Warnings.Add($"Unrecognized apiVersion: '{resource.ApiVersion}'. Expected 'atlas.io/v1alpha1'.");
        }

        if (string.IsNullOrWhiteSpace(resource.Kind))
        {
            result.Errors.Add("Missing required field: 'kind'. Expected 'AtlasResource'.");
        }
        else if (resource.Kind != "AtlasResource")
        {
            result.Errors.Add($"Invalid kind: '{resource.Kind}'. Expected 'AtlasResource'.");
        }

        // Metadata
        if (resource.Metadata == null)
        {
            result.Errors.Add("Missing required section: 'metadata'.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(resource.Metadata.Name))
            {
                result.Errors.Add("Missing required field: 'metadata.name'.");
            }
            else if (!K8sNameRegex.IsMatch(resource.Metadata.Name) || resource.Metadata.Name.Length > 253)
            {
                result.Errors.Add($"Invalid metadata.name '{resource.Metadata.Name}'. Must be a valid DNS-1123 subdomain (lowercase letters, numbers, and hyphens/dots, max 253 chars).");
            }

            if (string.IsNullOrWhiteSpace(resource.Metadata.Namespace))
            {
                result.Warnings.Add("Missing 'metadata.namespace'; defaulting to 'default'.");
            }
        }

        // Spec
        if (resource.Spec == null)
        {
            result.Errors.Add("Missing required section: 'spec'.");
        }
        else
        {
            // ComponentOverview
            if (resource.Spec.ComponentOverview == null || string.IsNullOrWhiteSpace(resource.Spec.ComponentOverview.Name))
            {
                result.Errors.Add("Missing required field: 'spec.componentOverview.name'.");
            }

            if (string.IsNullOrWhiteSpace(resource.Spec.ComponentOverview?.Tier))
            {
                result.Errors.Add("Missing required field: 'spec.componentOverview.tier'.");
            }

            // TechStack
            if (resource.Spec.TechStack == null || string.IsNullOrWhiteSpace(resource.Spec.TechStack.PrimaryLanguage))
            {
                result.Warnings.Add("Field 'spec.techStack.primaryLanguage' is missing.");
            }

            // Architecture & Multi-Diagrams
            if (resource.Spec.Architecture == null)
            {
                result.Errors.Add("Missing 'spec.architecture' section.");
            }
            else
            {
                ValidateDiagram(resource.Spec.Architecture.ContextDiagram, "contextDiagram", result);
                ValidateDiagram(resource.Spec.Architecture.ComponentDiagram, "componentDiagram", result);
                ValidateDiagram(resource.Spec.Architecture.DataFlowDiagram, "dataFlowDiagram", result);
                ValidateDiagram(resource.Spec.Architecture.MermaidDiagram, "mermaidDiagram", result);
            }

            // Security Scan Validation
            if (resource.Spec.Security != null)
            {
                if (resource.Spec.Security.SecurityScore < 0 || resource.Spec.Security.SecurityScore > 100)
                {
                    result.Warnings.Add($"SecurityScore '{resource.Spec.Security.SecurityScore}' should be between 0 and 100.");
                }
            }

            // Quality Verdict Validation
            if (resource.Spec.Quality != null)
            {
                if (resource.Spec.Quality.SigStars < 1.0 || resource.Spec.Quality.SigStars > 5.0)
                {
                    result.Warnings.Add($"SIG maintainability stars '{resource.Spec.Quality.SigStars}' should be between 1.0 and 5.0.");
                }
            }

            // Code Review Validation
            if (resource.Spec.CodeReview != null)
            {
                if (resource.Spec.CodeReview.ReviewScore < 0 || resource.Spec.CodeReview.ReviewScore > 100)
                {
                    result.Warnings.Add($"CodeReviewScore '{resource.Spec.CodeReview.ReviewScore}' should be between 0 and 100.");
                }
            }
        }

        return result;
    }

    private static void ValidateDiagram(string diagramText, string diagramName, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(diagramText)) return;

        var trimmed = diagramText.Trim();
        if (!trimmed.StartsWith("flowchart", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("graph", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("sequenceDiagram", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("C4Context", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("C4Container", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("stateDiagram", StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add($"Diagram '{diagramName}' does not start with a recognized Mermaid diagram header (flowchart, graph, sequenceDiagram, C4Context).");
        }
    }
}
