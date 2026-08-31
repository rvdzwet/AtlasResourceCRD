using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace Atlas.Core.Models;

/// <summary>
/// Represents an industry-standard CycloneDX 1.5 Software Bill of Materials (SBOM).
/// Compatible with OWASP Dependency-Track, Snyk, and GitHub Dependency Graph.
/// </summary>
public sealed class CycloneDxSbom
{
    [JsonPropertyName("bomFormat")]
    [YamlMember(Alias = "bomFormat")]
    public string BomFormat { get; set; } = "CycloneDX";

    [JsonPropertyName("specVersion")]
    [YamlMember(Alias = "specVersion")]
    public string SpecVersion { get; set; } = "1.5";

    [JsonPropertyName("serialNumber")]
    [YamlMember(Alias = "serialNumber")]
    public string SerialNumber { get; set; } = $"urn:uuid:{Guid.NewGuid()}";

    [JsonPropertyName("version")]
    [YamlMember(Alias = "version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("metadata")]
    [YamlMember(Alias = "metadata")]
    public CycloneDxMetadata Metadata { get; set; } = new();

    [JsonPropertyName("components")]
    [YamlMember(Alias = "components")]
    public List<CycloneDxComponent> Components { get; set; } = new();

    [JsonPropertyName("dependencies")]
    [YamlMember(Alias = "dependencies")]
    public List<CycloneDxDependencyLink> Dependencies { get; set; } = new();

    [JsonPropertyName("vulnerabilities")]
    [YamlMember(Alias = "vulnerabilities")]
    public List<CycloneDxVulnerability> Vulnerabilities { get; set; } = new();
}

public sealed class CycloneDxMetadata
{
    [JsonPropertyName("timestamp")]
    [YamlMember(Alias = "timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("tools")]
    [YamlMember(Alias = "tools")]
    public List<CycloneDxTool> Tools { get; set; } = new()
    {
        new CycloneDxTool { Vendor = "Atlas Enterprise", Name = "Atlas Scanner CLI", Version = "1.7.0" }
    };

    [JsonPropertyName("component")]
    [YamlMember(Alias = "component")]
    public CycloneDxComponent? Component { get; set; }
}

public sealed class CycloneDxTool
{
    [JsonPropertyName("vendor")]
    [YamlMember(Alias = "vendor")]
    public string Vendor { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    [YamlMember(Alias = "version")]
    public string Version { get; set; } = string.Empty;
}

public sealed class CycloneDxComponent
{
    [JsonPropertyName("type")]
    [YamlMember(Alias = "type")]
    public string Type { get; set; } = "library"; // application, framework, library, container, operating-system

    [JsonPropertyName("bom-ref")]
    [YamlMember(Alias = "bom-ref")]
    public string BomRef { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    [YamlMember(Alias = "version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("purl")]
    [YamlMember(Alias = "purl")]
    public string Purl { get; set; } = string.Empty; // e.g. pkg:nuget/Newtonsoft.Json@13.0.1

    [JsonPropertyName("scope")]
    [YamlMember(Alias = "scope")]
    public string Scope { get; set; } = "required"; // required, optional, excluded

    [JsonPropertyName("licenses")]
    [YamlMember(Alias = "licenses")]
    public List<CycloneDxLicenseChoice> Licenses { get; set; } = new();

    [JsonPropertyName("hashes")]
    [YamlMember(Alias = "hashes")]
    public List<CycloneDxHash> Hashes { get; set; } = new();

    [JsonPropertyName("externalReferences")]
    [YamlMember(Alias = "externalReferences")]
    public List<CycloneDxExternalReference> ExternalReferences { get; set; } = new();

    [JsonPropertyName("properties")]
    [YamlMember(Alias = "properties")]
    public List<CycloneDxProperty> Properties { get; set; } = new();
}

public sealed class CycloneDxLicenseChoice
{
    [JsonPropertyName("license")]
    [YamlMember(Alias = "license")]
    public CycloneDxLicense? License { get; set; }

    [JsonPropertyName("expression")]
    [YamlMember(Alias = "expression")]
    public string? Expression { get; set; }
}

public sealed class CycloneDxLicense
{
    [JsonPropertyName("id")]
    [YamlMember(Alias = "id")]
    public string? Id { get; set; } // SPDX ID, e.g. MIT, Apache-2.0, GPL-3.0

    [JsonPropertyName("name")]
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    [YamlMember(Alias = "url")]
    public string? Url { get; set; }
}

public sealed class CycloneDxHash
{
    [JsonPropertyName("alg")]
    [YamlMember(Alias = "alg")]
    public string Alg { get; set; } = "SHA-256";

    [JsonPropertyName("content")]
    [YamlMember(Alias = "content")]
    public string Content { get; set; } = string.Empty;
}

public sealed class CycloneDxExternalReference
{
    [JsonPropertyName("type")]
    [YamlMember(Alias = "type")]
    public string Type { get; set; } = "vcs"; // vcs, issue-tracker, website, documentation

    [JsonPropertyName("url")]
    [YamlMember(Alias = "url")]
    public string Url { get; set; } = string.Empty;
}

public sealed class CycloneDxProperty
{
    [JsonPropertyName("name")]
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    [YamlMember(Alias = "value")]
    public string Value { get; set; } = string.Empty;
}

public sealed class CycloneDxDependencyLink
{
    [JsonPropertyName("ref")]
    [YamlMember(Alias = "ref")]
    public string Ref { get; set; } = string.Empty;

    [JsonPropertyName("dependsOn")]
    [YamlMember(Alias = "dependsOn")]
    public List<string> DependsOn { get; set; } = new();
}

public sealed class CycloneDxVulnerability
{
    [JsonPropertyName("id")]
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty; // e.g. CVE-2023-12345 or GHSA-xxxx

    [JsonPropertyName("source")]
    [YamlMember(Alias = "source")]
    public CycloneDxVulnerabilitySource Source { get; set; } = new();

    [JsonPropertyName("ratings")]
    [YamlMember(Alias = "ratings")]
    public List<CycloneDxVulnerabilityRating> Ratings { get; set; } = new();

    [JsonPropertyName("cwes")]
    [YamlMember(Alias = "cwes")]
    public List<int> Cwes { get; set; } = new();

    [JsonPropertyName("description")]
    [YamlMember(Alias = "description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    [YamlMember(Alias = "detail")]
    public string Detail { get; set; } = string.Empty;

    [JsonPropertyName("recommendation")]
    [YamlMember(Alias = "recommendation")]
    public string Recommendation { get; set; } = string.Empty;

    [JsonPropertyName("affects")]
    [YamlMember(Alias = "affects")]
    public List<CycloneDxAffectTarget> Affects { get; set; } = new();

    [JsonPropertyName("fixedVersion")]
    [YamlMember(Alias = "fixedVersion")]
    public string FixedVersion { get; set; } = string.Empty;

    [JsonPropertyName("cveId")]
    [YamlMember(Alias = "cveId")]
    public string? CveId { get; set; }

    [JsonPropertyName("aliases")]
    [YamlMember(Alias = "aliases")]
    public List<string> Aliases { get; set; } = new();

    [JsonPropertyName("published")]
    [YamlMember(Alias = "published")]
    public DateTime? Published { get; set; }

    [JsonPropertyName("updated")]
    [YamlMember(Alias = "updated")]
    public DateTime? Updated { get; set; }
}

public sealed class CycloneDxVulnerabilitySource
{
    [JsonPropertyName("name")]
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = "NVD"; // NVD, GitHub Advisory, OSV

    [JsonPropertyName("url")]
    [YamlMember(Alias = "url")]
    public string Url { get; set; } = string.Empty;
}

public sealed class CycloneDxVulnerabilityRating
{
    [JsonPropertyName("source")]
    [YamlMember(Alias = "source")]
    public CycloneDxVulnerabilitySource Source { get; set; } = new();

    [JsonPropertyName("score")]
    [YamlMember(Alias = "score")]
    public double Score { get; set; } // CVSS Score e.g. 7.5

    [JsonPropertyName("severity")]
    [YamlMember(Alias = "severity")]
    public string Severity { get; set; } = "Medium"; // Critical, High, Medium, Low, Info

    [JsonPropertyName("method")]
    [YamlMember(Alias = "method")]
    public string Method { get; set; } = "CVSSv31"; // CVSSv2, CVSSv3, CVSSv31, CVSSv4

    [JsonPropertyName("vector")]
    [YamlMember(Alias = "vector")]
    public string Vector { get; set; } = string.Empty;
}

public sealed class CycloneDxAffectTarget
{
    [JsonPropertyName("ref")]
    [YamlMember(Alias = "ref")]
    public string Ref { get; set; } = string.Empty; // PURL or BOM-ref
}
