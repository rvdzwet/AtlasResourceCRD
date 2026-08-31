using System.Collections.Generic;
using Atlas.Core.Models;
using Atlas.Core.Serialization;
using Atlas.Core.Validation;
using FluentAssertions;
using Xunit;

namespace Atlas.Tests;

public class SerializationTests
{
    [Fact]
    public void SerializeAndDeserializeYaml_ShouldPreserveStructure()
    {
        var resource = new AtlasResource
        {
            ApiVersion = "atlas.io/v1alpha1",
            Kind = "AtlasResource",
            Metadata = new AtlasResourceMetadata
            {
                Name = "user-service",
                Namespace = "production",
                Labels = new Dictionary<string, string> { ["tier"] = "backend" },
                Annotations = new Dictionary<string, string> { ["atlas.io/generator"] = "Atlas CLI" }
            },
            Spec = new AtlasResourceSpec
            {
                ComponentOverview = new ComponentOverview
                {
                    Name = "user-service",
                    Description = "Handles user authentication and account profiles.",
                    Tier = "Backend",
                    Purpose = "User Authentication"
                },
                TechStack = new TechStack
                {
                    PrimaryLanguage = "C#",
                    Frameworks = new List<TechItem> { new() { Name = "ASP.NET Core", Version = "10.0" } }
                },
                Architecture = new ArchitectureSpec
                {
                    Summary = "Clean Architecture modular microservice.",
                    Pattern = "Clean Architecture",
                    MermaidDiagram = "flowchart TD\n  Client --> Gateway\n  Gateway --> API"
                },
                ApiContracts = new ApiContractsSpec
                {
                    Endpoints = new List<ApiEndpoint>
                    {
                        new() { Path = "/api/v1/users", Method = "GET", Description = "List all users", AuthRequired = true }
                    }
                }
            }
        };

        var yaml = CrdYamlSerializer.SerializeYaml(resource);
        yaml.Should().Contain("apiVersion: atlas.io/v1alpha1");
        yaml.Should().Contain("kind: AtlasResource");
        yaml.Should().Contain("name: user-service");
        yaml.Should().Contain("flowchart TD");

        var deserialized = CrdYamlSerializer.DeserializeYaml(yaml);
        deserialized.Should().NotBeNull();
        deserialized.ApiVersion.Should().Be("atlas.io/v1alpha1");
        deserialized.Kind.Should().Be("AtlasResource");
        deserialized.Metadata.Name.Should().Be("user-service");
        deserialized.Spec.ComponentOverview.Name.Should().Be("user-service");
        deserialized.Spec.ApiContracts.Endpoints.Should().HaveCount(1);
    }

    [Fact]
    public void Validate_ShouldReturnValidForProperResource()
    {
        var resource = new AtlasResource
        {
            ApiVersion = "atlas.io/v1alpha1",
            Kind = "AtlasResource",
            Metadata = new AtlasResourceMetadata
            {
                Name = "valid-k8s-name",
                Namespace = "default"
            },
            Spec = new AtlasResourceSpec
            {
                ComponentOverview = new ComponentOverview
                {
                    Name = "valid-k8s-name",
                    Description = "Valid description"
                },
                TechStack = new TechStack
                {
                    PrimaryLanguage = "Go"
                },
                Architecture = new ArchitectureSpec
                {
                    MermaidDiagram = "flowchart TD\n  A --> B"
                }
            }
        };

        var result = CrdValidator.Validate(resource);
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldDetectInvalidK8sName()
    {
        var resource = new AtlasResource
        {
            ApiVersion = "atlas.io/v1alpha1",
            Kind = "AtlasResource",
            Metadata = new AtlasResourceMetadata
            {
                Name = "INVALID_NAME_WITH_UPPERCASE"
            },
            Spec = new AtlasResourceSpec
            {
                ComponentOverview = new ComponentOverview { Name = "test" }
            }
        };

        var result = CrdValidator.Validate(resource);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("DNS-1123"));
    }
}
