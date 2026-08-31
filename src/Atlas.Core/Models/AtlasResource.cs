using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace Atlas.Core.Models;

/// <summary>
/// Root Kubernetes Custom Resource definition for AtlasResource (atlas.io/v1alpha1).
/// </summary>
public sealed class AtlasResource
{
    [YamlMember(Alias = "apiVersion", Order = 1)]
    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; } = "atlas.io/v1alpha1";

    [YamlMember(Alias = "kind", Order = 2)]
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "AtlasResource";

    [YamlMember(Alias = "metadata", Order = 3)]
    [JsonPropertyName("metadata")]
    public AtlasResourceMetadata Metadata { get; set; } = new();

    [YamlMember(Alias = "spec", Order = 4)]
    [JsonPropertyName("spec")]
    public AtlasResourceSpec Spec { get; set; } = new();
}

public sealed class AtlasResourceMetadata
{
    [YamlMember(Alias = "name", Order = 1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "namespace", Order = 2)]
    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = "default";

    [YamlMember(Alias = "labels", Order = 3)]
    [JsonPropertyName("labels")]
    public Dictionary<string, string> Labels { get; set; } = new();

    [YamlMember(Alias = "annotations", Order = 4)]
    [JsonPropertyName("annotations")]
    public Dictionary<string, string> Annotations { get; set; } = new();
}

public sealed class AtlasResourceSpec
{
    [YamlMember(Alias = "componentOverview", Order = 1)]
    [JsonPropertyName("componentOverview")]
    public ComponentOverview ComponentOverview { get; set; } = new();

    [YamlMember(Alias = "techStack", Order = 2)]
    [JsonPropertyName("techStack")]
    public TechStack TechStack { get; set; } = new();

    [YamlMember(Alias = "architecture", Order = 3)]
    [JsonPropertyName("architecture")]
    public ArchitectureSpec Architecture { get; set; } = new();

    [YamlMember(Alias = "apiContracts", Order = 4)]
    [JsonPropertyName("apiContracts")]
    public ApiContractsSpec ApiContracts { get; set; } = new();

    [YamlMember(Alias = "dependencies", Order = 5)]
    [JsonPropertyName("dependencies")]
    public DependenciesSpec Dependencies { get; set; } = new();

    [YamlMember(Alias = "configuration", Order = 6)]
    [JsonPropertyName("configuration")]
    public ConfigurationSpec Configuration { get; set; } = new();

    [YamlMember(Alias = "dataStores", Order = 7)]
    [JsonPropertyName("dataStores")]
    public DataStoresSpec DataStores { get; set; } = new();

    [YamlMember(Alias = "observability", Order = 8)]
    [JsonPropertyName("observability")]
    public ObservabilitySpec Observability { get; set; } = new();

    [YamlMember(Alias = "security", Order = 9)]
    [JsonPropertyName("security")]
    public SecurityScanSpec Security { get; set; } = new();

    [YamlMember(Alias = "quality", Order = 10)]
    [JsonPropertyName("quality")]
    public QualityVerdictSpec Quality { get; set; } = new();

    [YamlMember(Alias = "codeReview", Order = 11)]
    [JsonPropertyName("codeReview")]
    public CodeReviewSpec CodeReview { get; set; } = new();

    [YamlMember(Alias = "riskSummary", Order = 12)]
    [JsonPropertyName("riskSummary")]
    public RiskSummarySpec RiskSummary { get; set; } = new();

    [YamlMember(Alias = "threatModel", Order = 13)]
    [JsonPropertyName("threatModel")]
    public ThreatModelSpec ThreatModel { get; set; } = new();

    [YamlMember(Alias = "functionalSpecs", Order = 14)]
    [JsonPropertyName("functionalSpecs")]
    public FunctionalSpecs FunctionalSpecs { get; set; } = new();

    [YamlMember(Alias = "deployment", Order = 15)]
    [JsonPropertyName("deployment")]
    public DeploymentSpec? Deployment { get; set; }
}

public sealed class ComponentOverview
{
    [YamlMember(Alias = "name", Order = 1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "description", Order = 2)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "tier", Order = 3)]
    [JsonPropertyName("tier")]
    public string Tier { get; set; } = "Backend"; // Backend, Frontend, CLI, Library, Worker, Gateway, etc.

    [YamlMember(Alias = "purpose", Order = 4)]
    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = string.Empty;

    [YamlMember(Alias = "lifecycle", Order = 5)]
    [JsonPropertyName("lifecycle")]
    public string Lifecycle { get; set; } = "Active"; // Experimental, Active, Staging, Deprecated

    [YamlMember(Alias = "repositoryUrl", Order = 6)]
    [JsonPropertyName("repositoryUrl")]
    public string? RepositoryUrl { get; set; }

    [YamlMember(Alias = "owner", Order = 7)]
    [JsonPropertyName("owner")]
    public string? Owner { get; set; }
}

public sealed class TechStack
{
    [YamlMember(Alias = "primaryLanguage", Order = 1)]
    [JsonPropertyName("primaryLanguage")]
    public string PrimaryLanguage { get; set; } = string.Empty;

    [YamlMember(Alias = "languages", Order = 2)]
    [JsonPropertyName("languages")]
    public List<TechItem> Languages { get; set; } = new();

    [YamlMember(Alias = "frameworks", Order = 3)]
    [JsonPropertyName("frameworks")]
    public List<TechItem> Frameworks { get; set; } = new();

    [YamlMember(Alias = "runtimes", Order = 4)]
    [JsonPropertyName("runtimes")]
    public List<TechItem> Runtimes { get; set; } = new();

    [YamlMember(Alias = "buildSystems", Order = 5)]
    [JsonPropertyName("buildSystems")]
    public List<TechItem> BuildSystems { get; set; } = new();

    [YamlMember(Alias = "packageManagers", Order = 6)]
    [JsonPropertyName("packageManagers")]
    public List<TechItem> PackageManagers { get; set; } = new();
}

[JsonConverter(typeof(TechItemJsonConverter))]
public sealed class TechItem
{
    [YamlMember(Alias = "name", Order = 1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "version", Order = 2)]
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [YamlMember(Alias = "details", Order = 3)]
    [JsonPropertyName("details")]
    public string? Details { get; set; }
}

public sealed class TechItemJsonConverter : JsonConverter<TechItem>
{
    public override TechItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new TechItem { Name = reader.GetString() ?? string.Empty };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var item = new TechItem();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return item;

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propName = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (propName == "name") item.Name = reader.GetString() ?? string.Empty;
                    else if (propName == "version") item.Version = reader.GetString();
                    else if (propName == "details") item.Details = reader.GetString();
                }
            }
            return item;
        }

        return new TechItem();
    }

    public override void Write(Utf8JsonWriter writer, TechItem value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        if (!string.IsNullOrEmpty(value.Version)) writer.WriteString("version", value.Version);
        if (!string.IsNullOrEmpty(value.Details)) writer.WriteString("details", value.Details);
        writer.WriteEndObject();
    }
}

public sealed class ArchitectureSpec
{
    [YamlMember(Alias = "summary", Order = 1)]
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [YamlMember(Alias = "pattern", Order = 2)]
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = string.Empty; // e.g. Clean Architecture, CQRS, Modular Monolith, CLI Tool

    [YamlMember(Alias = "components", Order = 3)]
    [JsonPropertyName("components")]
    public List<ArchComponent> Components { get; set; } = new();

    [YamlMember(Alias = "contextDiagram", Order = 4, ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
    [JsonPropertyName("contextDiagram")]
    public string ContextDiagram { get; set; } = string.Empty;

    [YamlMember(Alias = "componentDiagram", Order = 5, ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
    [JsonPropertyName("componentDiagram")]
    public string ComponentDiagram { get; set; } = string.Empty;

    [YamlMember(Alias = "dataFlowDiagram", Order = 6, ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
    [JsonPropertyName("dataFlowDiagram")]
    public string DataFlowDiagram { get; set; } = string.Empty;

    [YamlMember(Alias = "mermaidDiagram", Order = 7, ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
    [JsonPropertyName("mermaidDiagram")]
    public string MermaidDiagram
    {
        get => !string.IsNullOrWhiteSpace(field) ? field : (!string.IsNullOrWhiteSpace(ComponentDiagram) ? ComponentDiagram : ContextDiagram);
        set => field = value;
    }
}

[JsonConverter(typeof(ArchComponentJsonConverter))]
public sealed class ArchComponent
{
    [YamlMember(Alias = "name", Order = 1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "type", Order = 2)]
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // Service, Controller, Handler, Repository, Model, Scanner, Agent

    [YamlMember(Alias = "description", Order = 3)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "responsibilities", Order = 4)]
    [JsonPropertyName("responsibilities")]
    public List<string> Responsibilities { get; set; } = new();
}

public sealed class ArchComponentJsonConverter : JsonConverter<ArchComponent>
{
    public override ArchComponent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new ArchComponent { Name = reader.GetString() ?? string.Empty };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var item = new ArchComponent();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return item;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var prop = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (prop == "name") item.Name = reader.GetString() ?? string.Empty;
                    else if (prop == "type") item.Type = reader.GetString() ?? string.Empty;
                    else if (prop == "description") item.Description = reader.GetString() ?? string.Empty;
                }
            }
            return item;
        }

        return new ArchComponent();
    }

    public override void Write(Utf8JsonWriter writer, ArchComponent value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        if (!string.IsNullOrEmpty(value.Type)) writer.WriteString("type", value.Type);
        if (!string.IsNullOrEmpty(value.Description)) writer.WriteString("description", value.Description);
        writer.WriteEndObject();
    }
}

public sealed class ApiContractsSpec
{
    [YamlMember(Alias = "endpoints", Order = 1)]
    [JsonPropertyName("endpoints")]
    public List<ApiEndpoint> Endpoints { get; set; } = new();

    [YamlMember(Alias = "events", Order = 2)]
    [JsonPropertyName("events")]
    public List<EventContract> Events { get; set; } = new();

    [YamlMember(Alias = "grpcServices", Order = 3)]
    [JsonPropertyName("grpcServices")]
    public List<GrpcContract> GrpcServices { get; set; } = new();
}

public sealed class ApiEndpoint
{
    [YamlMember(Alias = "path", Order = 1)]
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [YamlMember(Alias = "method", Order = 2)]
    [JsonPropertyName("method")]
    public string Method { get; set; } = "GET";

    [YamlMember(Alias = "description", Order = 3)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "authRequired", Order = 4)]
    [JsonPropertyName("authRequired")]
    public bool AuthRequired { get; set; }

    [YamlMember(Alias = "requestType", Order = 5)]
    [JsonPropertyName("requestType")]
    public string? RequestType { get; set; }

    [YamlMember(Alias = "responseType", Order = 6)]
    [JsonPropertyName("responseType")]
    public string? ResponseType { get; set; }
}

public sealed class EventContract
{
    [YamlMember(Alias = "topicOrQueue", Order = 1)]
    [JsonPropertyName("topicOrQueue")]
    public string TopicOrQueue { get; set; } = string.Empty;

    [YamlMember(Alias = "action", Order = 2)]
    [JsonPropertyName("action")]
    public string Action { get; set; } = "Publish"; // Publish, Subscribe

    [YamlMember(Alias = "description", Order = 3)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "payloadType", Order = 4)]
    [JsonPropertyName("payloadType")]
    public string? PayloadType { get; set; }
}

public sealed class GrpcContract
{
    [YamlMember(Alias = "serviceName", Order = 1)]
    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [YamlMember(Alias = "methods", Order = 2)]
    [JsonPropertyName("methods")]
    public List<string> Methods { get; set; } = new();

    [YamlMember(Alias = "description", Order = 3)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

[JsonConverter(typeof(DependenciesSpecJsonConverter))]
public sealed class DependenciesSpec
{
    [YamlMember(Alias = "internalServices", Order = 1)]
    [JsonPropertyName("internalServices")]
    public List<DependencyItem> InternalServices { get; set; } = new();

    [YamlMember(Alias = "externalApis", Order = 2)]
    [JsonPropertyName("externalApis")]
    public List<DependencyItem> ExternalApis { get; set; } = new();

    [YamlMember(Alias = "keyPackages", Order = 3)]
    [JsonPropertyName("keyPackages")]
    public List<PackageDependency> KeyPackages { get; set; } = new();

    [YamlMember(Alias = "sbom", Order = 4)]
    [JsonPropertyName("sbom")]
    public CycloneDxSbom? Sbom { get; set; }

    [YamlMember(Alias = "vulnerabilityAudit", Order = 5)]
    [JsonPropertyName("vulnerabilityAudit")]
    public VulnerabilityAuditSummary? VulnerabilityAudit { get; set; }
}

public sealed class DependenciesSpecJsonConverter : JsonConverter<DependenciesSpec>
{
    public override DependenciesSpec Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var spec = new DependenciesSpec();

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray) return spec;
                var package = JsonSerializer.Deserialize<PackageDependency>(ref reader, options);
                if (package != null) spec.KeyPackages.Add(package);
            }
            return spec;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return spec;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var prop = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (prop is "internalservices" or "internal_services")
                    {
                        if (reader.TokenType == JsonTokenType.StartArray)
                            spec.InternalServices = JsonSerializer.Deserialize<List<DependencyItem>>(ref reader, options) ?? new();
                        else reader.Skip();
                    }
                    else if (prop is "externalapis" or "external_apis" or "external")
                    {
                        if (reader.TokenType == JsonTokenType.StartArray)
                            spec.ExternalApis = JsonSerializer.Deserialize<List<DependencyItem>>(ref reader, options) ?? new();
                        else reader.Skip();
                    }
                    else if (prop is "keypackages" or "packages" or "key_packages" or "dependencies")
                    {
                        if (reader.TokenType == JsonTokenType.StartArray)
                            spec.KeyPackages = JsonSerializer.Deserialize<List<PackageDependency>>(ref reader, options) ?? new();
                        else reader.Skip();
                    }
                    else if (prop is "sbom")
                    {
                        if (reader.TokenType == JsonTokenType.StartObject)
                            spec.Sbom = JsonSerializer.Deserialize<CycloneDxSbom>(ref reader, options);
                        else reader.Skip();
                    }
                    else if (prop is "vulnerabilityaudit" or "vulnerability_audit")
                    {
                        if (reader.TokenType == JsonTokenType.StartObject)
                            spec.VulnerabilityAudit = JsonSerializer.Deserialize<VulnerabilityAuditSummary>(ref reader, options);
                        else reader.Skip();
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
            }
            return spec;
        }

        return spec;
    }

    public override void Write(Utf8JsonWriter writer, DependenciesSpec value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.InternalServices?.Count > 0)
        {
            writer.WritePropertyName("internalServices");
            JsonSerializer.Serialize(writer, value.InternalServices, options);
        }
        if (value.ExternalApis?.Count > 0)
        {
            writer.WritePropertyName("externalApis");
            JsonSerializer.Serialize(writer, value.ExternalApis, options);
        }
        if (value.KeyPackages?.Count > 0)
        {
            writer.WritePropertyName("keyPackages");
            JsonSerializer.Serialize(writer, value.KeyPackages, options);
        }
        if (value.Sbom != null)
        {
            writer.WritePropertyName("sbom");
            JsonSerializer.Serialize(writer, value.Sbom, options);
        }
        if (value.VulnerabilityAudit != null)
        {
            writer.WritePropertyName("vulnerabilityAudit");
            JsonSerializer.Serialize(writer, value.VulnerabilityAudit, options);
        }
        writer.WriteEndObject();
    }
}

public sealed class VulnerabilityAuditSummary
{
    [YamlMember(Alias = "totalVulnerabilities", Order = 1)]
    [JsonPropertyName("totalVulnerabilities")]
    public int TotalVulnerabilities { get; set; }

    [YamlMember(Alias = "criticalCount", Order = 2)]
    [JsonPropertyName("criticalCount")]
    public int CriticalCount { get; set; }

    [YamlMember(Alias = "highCount", Order = 3)]
    [JsonPropertyName("highCount")]
    public int HighCount { get; set; }

    [YamlMember(Alias = "mediumCount", Order = 4)]
    [JsonPropertyName("mediumCount")]
    public int MediumCount { get; set; }

    [YamlMember(Alias = "lowCount", Order = 5)]
    [JsonPropertyName("lowCount")]
    public int LowCount { get; set; }

    [YamlMember(Alias = "auditTimestamp", Order = 6)]
    [JsonPropertyName("auditTimestamp")]
    public DateTime AuditTimestamp { get; set; } = DateTime.UtcNow;

    [YamlMember(Alias = "vulnerabilities", Order = 7)]
    [JsonPropertyName("vulnerabilities")]
    public List<CycloneDxVulnerability> Vulnerabilities { get; set; } = new();
}

[JsonConverter(typeof(DependencyItemJsonConverter))]
public sealed class DependencyItem
{
    [YamlMember(Alias = "name", Order = 1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "protocolOrHost", Order = 2)]
    [JsonPropertyName("protocolOrHost")]
    public string? ProtocolOrHost { get; set; }

    [YamlMember(Alias = "purpose", Order = 3)]
    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = string.Empty;

    [YamlMember(Alias = "criticality", Order = 4)]
    [JsonPropertyName("criticality")]
    public string Criticality { get; set; } = "High"; // Low, Medium, High, Critical
}

public sealed class DependencyItemJsonConverter : JsonConverter<DependencyItem>
{
    public override DependencyItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new DependencyItem { Name = reader.GetString() ?? string.Empty };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var item = new DependencyItem();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return item;

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propName = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (propName == "name") item.Name = reader.GetString() ?? string.Empty;
                    else if (propName == "protocolorhost") item.ProtocolOrHost = reader.GetString();
                    else if (propName == "purpose") item.Purpose = reader.GetString() ?? string.Empty;
                    else if (propName == "criticality") item.Criticality = reader.GetString() ?? "High";
                }
            }
            return item;
        }

        return new DependencyItem();
    }

    public override void Write(Utf8JsonWriter writer, DependencyItem value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        if (!string.IsNullOrEmpty(value.ProtocolOrHost)) writer.WriteString("protocolOrHost", value.ProtocolOrHost);
        if (!string.IsNullOrEmpty(value.Purpose)) writer.WriteString("purpose", value.Purpose);
        if (!string.IsNullOrEmpty(value.Criticality)) writer.WriteString("criticality", value.Criticality);
        writer.WriteEndObject();
    }
}

[JsonConverter(typeof(PackageDependencyJsonConverter))]
public sealed class PackageDependency
{
    [YamlMember(Alias = "name", Order = 1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "version", Order = 2)]
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [YamlMember(Alias = "purpose", Order = 3)]
    [JsonPropertyName("purpose")]
    public string? Purpose { get; set; }
}

public sealed class PackageDependencyJsonConverter : JsonConverter<PackageDependency>
{
    public override PackageDependency Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new PackageDependency { Name = reader.GetString() ?? string.Empty };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var item = new PackageDependency();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return item;

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propName = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (propName == "name") item.Name = reader.GetString() ?? string.Empty;
                    else if (propName == "version") item.Version = reader.GetString();
                    else if (propName == "purpose") item.Purpose = reader.GetString();
                }
            }
            return item;
        }

        return new PackageDependency();
    }

    public override void Write(Utf8JsonWriter writer, PackageDependency value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        if (!string.IsNullOrEmpty(value.Version)) writer.WriteString("version", value.Version);
        if (!string.IsNullOrEmpty(value.Purpose)) writer.WriteString("purpose", value.Purpose);
        writer.WriteEndObject();
    }
}

public sealed class ConfigurationSpec
{
    [YamlMember(Alias = "environmentVariables", Order = 1)]
    [JsonPropertyName("environmentVariables")]
    public List<EnvVarConfig> EnvironmentVariables { get; set; } = new();

    [YamlMember(Alias = "configFiles", Order = 2)]
    [JsonPropertyName("configFiles")]
    public List<ConfigFileSpec> ConfigFiles { get; set; } = new();
}

public sealed class EnvVarConfig
{
    [YamlMember(Alias = "name", Order = 1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "description", Order = 2)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "required", Order = 3)]
    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [YamlMember(Alias = "defaultValue", Order = 4)]
    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; set; }

    [YamlMember(Alias = "isSecret", Order = 5)]
    [JsonPropertyName("isSecret")]
    public bool IsSecret { get; set; }
}

public sealed class ConfigFileSpec
{
    [YamlMember(Alias = "path", Order = 1)]
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [YamlMember(Alias = "format", Order = 2)]
    [JsonPropertyName("format")]
    public string Format { get; set; } = "YAML"; // JSON, YAML, TOML, XML, INI, ENV

    [YamlMember(Alias = "description", Order = 3)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public sealed class DataStoresSpec
{
    [YamlMember(Alias = "databases", Order = 1)]
    [JsonPropertyName("databases")]
    public List<DataStoreSpec> Databases { get; set; } = new();

    [YamlMember(Alias = "caches", Order = 2)]
    [JsonPropertyName("caches")]
    public List<DataStoreSpec> Caches { get; set; } = new();

    [YamlMember(Alias = "messageBrokers", Order = 3)]
    [JsonPropertyName("messageBrokers")]
    public List<DataStoreSpec> MessageBrokers { get; set; } = new();

    [YamlMember(Alias = "objectStorage", Order = 4)]
    [JsonPropertyName("objectStorage")]
    public List<DataStoreSpec> ObjectStorage { get; set; } = new();
}

public sealed class DataStoreSpec
{
    [YamlMember(Alias = "name", Order = 1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "type", Order = 2)]
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // PostgreSQL, Redis, RabbitMQ, S3, MongoDB, SQLite, etc.

    [YamlMember(Alias = "role", Order = 3)]
    [JsonPropertyName("role")]
    public string Role { get; set; } = "Primary";

    [YamlMember(Alias = "description", Order = 4)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

[JsonConverter(typeof(ObservabilitySpecJsonConverter))]
public sealed class ObservabilitySpec
{
    [YamlMember(Alias = "healthChecks", Order = 1)]
    [JsonPropertyName("healthChecks")]
    public List<HealthCheckSpec> HealthChecks { get; set; } = new();

    [YamlMember(Alias = "logging", Order = 2)]
    [JsonPropertyName("logging")]
    public LoggingSpec Logging { get; set; } = new();

    [YamlMember(Alias = "metrics", Order = 3)]
    [JsonPropertyName("metrics")]
    public MetricsSpec Metrics { get; set; } = new();

    [YamlMember(Alias = "tracing", Order = 4)]
    [JsonPropertyName("tracing")]
    public TracingSpec Tracing { get; set; } = new();
}

public sealed class ObservabilitySpecJsonConverter : JsonConverter<ObservabilitySpec>
{
    public override ObservabilitySpec Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var spec = new ObservabilitySpec();

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString() ?? string.Empty;
            spec.Logging = new LoggingSpec { Framework = str, Format = str };
            return spec;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return spec;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var prop = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (prop is "healthchecks" or "health_checks" or "health" or "probes")
                    {
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            var val = reader.GetString() ?? string.Empty;
                            spec.HealthChecks = new List<HealthCheckSpec>
                            {
                                new HealthCheckSpec { EndpointOrCommand = val, Description = val, Type = "Liveness" }
                            };
                        }
                        else if (reader.TokenType == JsonTokenType.StartArray)
                        {
                            spec.HealthChecks = JsonSerializer.Deserialize<List<HealthCheckSpec>>(ref reader, options) ?? new();
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }
                    else if (prop is "logging" or "structuredlogging" or "structured_logging")
                    {
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            var val = reader.GetString() ?? string.Empty;
                            spec.Logging = new LoggingSpec { Framework = val, Format = "Structured" };
                        }
                        else if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            spec.Logging = JsonSerializer.Deserialize<LoggingSpec>(ref reader, options) ?? new();
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }
                    else if (prop is "metrics")
                    {
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            var val = reader.GetString() ?? string.Empty;
                            spec.Metrics = new MetricsSpec { Exporter = val, KeyMetrics = new() { val } };
                        }
                        else if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            spec.Metrics = JsonSerializer.Deserialize<MetricsSpec>(ref reader, options) ?? new();
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }
                    else if (prop is "tracing")
                    {
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            var val = reader.GetString() ?? string.Empty;
                            spec.Tracing = new TracingSpec { Exporter = val, Protocol = "OpenTelemetry" };
                        }
                        else if (reader.TokenType == JsonTokenType.StartObject)
                        {
                            spec.Tracing = JsonSerializer.Deserialize<TracingSpec>(ref reader, options) ?? new();
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
            }
            return spec;
        }

        return spec;
    }

    public override void Write(Utf8JsonWriter writer, ObservabilitySpec value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.HealthChecks?.Count > 0)
        {
            writer.WritePropertyName("healthChecks");
            JsonSerializer.Serialize(writer, value.HealthChecks, options);
        }
        if (value.Logging != null)
        {
            writer.WritePropertyName("logging");
            JsonSerializer.Serialize(writer, value.Logging, options);
        }
        if (value.Metrics != null)
        {
            writer.WritePropertyName("metrics");
            JsonSerializer.Serialize(writer, value.Metrics, options);
        }
        if (value.Tracing != null)
        {
            writer.WritePropertyName("tracing");
            JsonSerializer.Serialize(writer, value.Tracing, options);
        }
        writer.WriteEndObject();
    }
}

[JsonConverter(typeof(HealthCheckSpecJsonConverter))]
public sealed class HealthCheckSpec
{
    [YamlMember(Alias = "endpointOrCommand", Order = 1)]
    [JsonPropertyName("endpointOrCommand")]
    public string EndpointOrCommand { get; set; } = string.Empty;

    [YamlMember(Alias = "type", Order = 2)]
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Liveness"; // Liveness, Readiness, Startup

    [YamlMember(Alias = "description", Order = 3)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public sealed class HealthCheckSpecJsonConverter : JsonConverter<HealthCheckSpec>
{
    public override HealthCheckSpec Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString() ?? string.Empty;
            return new HealthCheckSpec { EndpointOrCommand = str, Description = str };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var item = new HealthCheckSpec();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return item;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var prop = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (prop is "endpointorcommand" or "endpoint" or "command" or "path" or "url") item.EndpointOrCommand = reader.GetString() ?? string.Empty;
                    else if (prop is "type") item.Type = reader.GetString() ?? "Liveness";
                    else if (prop is "description") item.Description = reader.GetString() ?? string.Empty;
                }
            }
            return item;
        }

        return new HealthCheckSpec();
    }

    public override void Write(Utf8JsonWriter writer, HealthCheckSpec value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("endpointOrCommand", value.EndpointOrCommand);
        writer.WriteString("type", value.Type);
        if (!string.IsNullOrEmpty(value.Description)) writer.WriteString("description", value.Description);
        writer.WriteEndObject();
    }
}

public sealed class LoggingSpec
{
    [YamlMember(Alias = "framework", Order = 1)]
    [JsonPropertyName("framework")]
    public string Framework { get; set; } = string.Empty; // Serilog, Microsoft.Extensions.Logging, NLog, Winston, Zap

    [YamlMember(Alias = "format", Order = 2)]
    [JsonPropertyName("format")]
    public string Format { get; set; } = "Structured JSON";

    [YamlMember(Alias = "sinks", Order = 3)]
    [JsonPropertyName("sinks")]
    public List<string> Sinks { get; set; } = new(); // Seq, Console, OpenTelemetry, ElasticSearch
}

public sealed class MetricsSpec
{
    [YamlMember(Alias = "exporter", Order = 1)]
    [JsonPropertyName("exporter")]
    public string Exporter { get; set; } = string.Empty; // Prometheus, OpenTelemetry, CloudWatch

    [YamlMember(Alias = "keyMetrics", Order = 2)]
    [JsonPropertyName("keyMetrics")]
    public List<string> KeyMetrics { get; set; } = new();
}

public sealed class TracingSpec
{
    [YamlMember(Alias = "protocol", Order = 1)]
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty; // OpenTelemetry, Jaeger, Zipkin, AWS X-Ray

    [YamlMember(Alias = "exporter", Order = 2)]
    [JsonPropertyName("exporter")]
    public string Exporter { get; set; } = string.Empty;
}

public sealed class SecurityScanSpec
{
    [YamlMember(Alias = "overallRating", Order = 1)]
    [JsonPropertyName("overallRating")]
    public string OverallRating { get; set; } = "A"; // A+, A, B, C, D, F

    [YamlMember(Alias = "securityScore", Order = 2)]
    [JsonPropertyName("securityScore")]
    [JsonConverter(typeof(FlexibleIntJsonConverter))]
    public int SecurityScore { get; set; } = 90; // 0-100

    [YamlMember(Alias = "owaspCompliance", Order = 3)]
    [JsonPropertyName("owaspCompliance")]
    public List<OwaspComplianceItem> OwaspCompliance { get; set; } = new();

    [YamlMember(Alias = "findings", Order = 4)]
    [JsonPropertyName("findings")]
    public List<SecurityFinding> Findings { get; set; } = new();

    [YamlMember(Alias = "recommendations", Order = 5)]
    [JsonPropertyName("recommendations")]
    public List<string> Recommendations { get; set; } = new();
}

[JsonConverter(typeof(OwaspComplianceItemJsonConverter))]
public sealed class OwaspComplianceItem
{
    [YamlMember(Alias = "category", Order = 1)]
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty; // e.g. A01:2021-Broken Access Control

    [YamlMember(Alias = "standard", Order = 2)]
    [JsonPropertyName("standard")]
    public string Standard { get; set; } = "OWASP Top 10";

    [YamlMember(Alias = "status", Order = 3)]
    [JsonPropertyName("status")]
    public string Status { get; set; } = "Compliant"; // Compliant, Partial, NonCompliant, NotApplicable

    [YamlMember(Alias = "evidence", Order = 4)]
    [JsonPropertyName("evidence")]
    public string Evidence { get; set; } = string.Empty;
}

public sealed class OwaspComplianceItemJsonConverter : JsonConverter<OwaspComplianceItem>
{
    public override OwaspComplianceItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString() ?? string.Empty;
            return new OwaspComplianceItem { Category = str, Evidence = str };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var item = new OwaspComplianceItem();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return item;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var prop = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (prop is "category" or "name" or "id") item.Category = reader.GetString() ?? string.Empty;
                    else if (prop is "standard") item.Standard = reader.GetString() ?? "OWASP Top 10";
                    else if (prop is "status") item.Status = reader.GetString() ?? "Compliant";
                    else if (prop is "evidence" or "description") item.Evidence = reader.GetString() ?? string.Empty;
                }
            }
            return item;
        }

        return new OwaspComplianceItem();
    }

    public override void Write(Utf8JsonWriter writer, OwaspComplianceItem value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("category", value.Category);
        writer.WriteString("standard", value.Standard);
        writer.WriteString("status", value.Status);
        writer.WriteString("evidence", value.Evidence);
        writer.WriteEndObject();
    }
}

[JsonConverter(typeof(SecurityFindingJsonConverter))]
public sealed class SecurityFinding
{
    [YamlMember(Alias = "title", Order = 1)]
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [YamlMember(Alias = "severity", Order = 2)]
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "Medium"; // Low, Medium, High, Critical

    [YamlMember(Alias = "owaspRef", Order = 3)]
    [JsonPropertyName("owaspRef")]
    public string OwaspRef { get; set; } = string.Empty;

    [YamlMember(Alias = "description", Order = 4)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "mitigation", Order = 5)]
    [JsonPropertyName("mitigation")]
    public string Mitigation { get; set; } = string.Empty;

    [YamlMember(Alias = "affectedFiles", Order = 6)]
    [JsonPropertyName("affectedFiles")]
    public List<string> AffectedFiles { get; set; } = new();
}

public sealed class SecurityFindingJsonConverter : JsonConverter<SecurityFinding>
{
    public override SecurityFinding Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString() ?? string.Empty;
            return new SecurityFinding { Title = str, Description = str };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var item = new SecurityFinding();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return item;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var prop = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (prop is "title" or "name") item.Title = reader.GetString() ?? string.Empty;
                    else if (prop is "severity") item.Severity = reader.GetString() ?? "Medium";
                    else if (prop is "owaspref" or "owaspcategory" or "category") item.OwaspRef = reader.GetString() ?? string.Empty;
                    else if (prop is "description") item.Description = reader.GetString() ?? string.Empty;
                    else if (prop is "mitigation" or "remediation") item.Mitigation = reader.GetString() ?? string.Empty;
                }
            }
            return item;
        }

        return new SecurityFinding();
    }

    public override void Write(Utf8JsonWriter writer, SecurityFinding value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("title", value.Title);
        writer.WriteString("severity", value.Severity);
        writer.WriteString("owaspRef", value.OwaspRef);
        writer.WriteString("description", value.Description);
        writer.WriteString("mitigation", value.Mitigation);
        writer.WriteEndObject();
    }
}

public sealed class QualityVerdictSpec
{
    [YamlMember(Alias = "sigStars", Order = 1)]
    [JsonPropertyName("sigStars")]
    [JsonConverter(typeof(FlexibleDoubleJsonConverter))]
    public double SigStars { get; set; } = 4.5; // 1.0 - 5.0 stars (SIG maintainability model)

    [YamlMember(Alias = "maintainabilityLevel", Order = 2)]
    [JsonPropertyName("maintainabilityLevel")]
    public string MaintainabilityLevel { get; set; } = "High"; // Very High, High, Moderate, Low, Very Low

    [YamlMember(Alias = "dimensions", Order = 3)]
    [JsonPropertyName("dimensions")]
    public List<SigDimensionScore> Dimensions { get; set; } = new();

    [YamlMember(Alias = "summary", Order = 4)]
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [YamlMember(Alias = "techDebtItems", Order = 5)]
    [JsonPropertyName("techDebtItems")]
    public List<string> TechDebtItems { get; set; } = new();
}

public sealed class SigDimensionScore
{
    [YamlMember(Alias = "dimension", Order = 1)]
    [JsonPropertyName("dimension")]
    public string Dimension { get; set; } = string.Empty; // Volume, ComponentIndependence, UnitComplexity, Testability, ArchitectureConsistency

    [YamlMember(Alias = "stars", Order = 2)]
    [JsonPropertyName("stars")]
    [JsonConverter(typeof(FlexibleDoubleJsonConverter))]
    public double Stars { get; set; } = 4.0; // 1.0 - 5.0

    [YamlMember(Alias = "evaluation", Order = 3)]
    [JsonPropertyName("evaluation")]
    public string Evaluation { get; set; } = string.Empty;
}

public sealed class FlexibleDoubleJsonConverter : JsonConverter<double>
{
    public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number) return reader.GetDouble();
        if (reader.TokenType == JsonTokenType.String && double.TryParse(reader.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            double result = 4.0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return result;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    reader.Read();
                    if (reader.TokenType == JsonTokenType.Number) result = reader.GetDouble();
                    else if (reader.TokenType == JsonTokenType.String && double.TryParse(reader.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var parsed)) result = parsed;
                }
            }
            return result;
        }
        return 4.0;
    }

    public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

public sealed class FlexibleIntJsonConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number) return reader.GetInt32();
        if (reader.TokenType == JsonTokenType.String && int.TryParse(reader.GetString(), out var i)) return i;
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            int result = 90;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return result;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    reader.Read();
                    if (reader.TokenType == JsonTokenType.Number) result = reader.GetInt32();
                    else if (reader.TokenType == JsonTokenType.String && int.TryParse(reader.GetString(), out var parsed)) result = parsed;
                }
            }
            return result;
        }
        return 90;
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}

public sealed class CodeReviewSpec
{
    [YamlMember(Alias = "reviewGrade", Order = 1)]
    [JsonPropertyName("reviewGrade")]
    public string ReviewGrade { get; set; } = "A"; // A+, A, B, C, D, F

    [YamlMember(Alias = "reviewScore", Order = 2)]
    [JsonPropertyName("reviewScore")]
    [JsonConverter(typeof(FlexibleIntJsonConverter))]
    public int ReviewScore { get; set; } = 90; // 0-100

    [YamlMember(Alias = "summary", Order = 3)]
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [YamlMember(Alias = "strengths", Order = 4)]
    [JsonPropertyName("strengths")]
    public List<string> Strengths { get; set; } = new();

    [YamlMember(Alias = "codeSmells", Order = 5)]
    [JsonPropertyName("codeSmells")]
    public List<CodeSmellItem> CodeSmells { get; set; } = new();

    [YamlMember(Alias = "findings", Order = 6)]
    [JsonPropertyName("findings")]
    public List<CodeReviewFinding> Findings { get; set; } = new();
}

[JsonConverter(typeof(CodeSmellItemJsonConverter))]
public sealed class CodeSmellItem
{
    [YamlMember(Alias = "smellType", Order = 1)]
    [JsonPropertyName("smellType")]
    public string SmellType { get; set; } = string.Empty; // e.g. God Class, Dead Code, Long Parameter List, Sync-over-Async

    [YamlMember(Alias = "description", Order = 2)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "affectedComponentOrFile", Order = 3)]
    [JsonPropertyName("affectedComponentOrFile")]
    public string AffectedComponentOrFile { get; set; } = string.Empty;
}

public sealed class CodeSmellItemJsonConverter : JsonConverter<CodeSmellItem>
{
    public override CodeSmellItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString() ?? string.Empty;
            return new CodeSmellItem { SmellType = "Code Smell", Description = str };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var item = new CodeSmellItem();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return item;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var prop = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (prop == "smelltype") item.SmellType = reader.GetString() ?? string.Empty;
                    else if (prop == "description") item.Description = reader.GetString() ?? string.Empty;
                    else if (prop == "affectedcomponentorfile") item.AffectedComponentOrFile = reader.GetString() ?? string.Empty;
                }
            }
            return item;
        }

        return new CodeSmellItem();
    }

    public override void Write(Utf8JsonWriter writer, CodeSmellItem value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("smellType", value.SmellType);
        writer.WriteString("description", value.Description);
        if (!string.IsNullOrEmpty(value.AffectedComponentOrFile))
            writer.WriteString("affectedComponentOrFile", value.AffectedComponentOrFile);
        writer.WriteEndObject();
    }
}

[JsonConverter(typeof(CodeReviewFindingJsonConverter))]
public sealed class CodeReviewFinding
{
    [YamlMember(Alias = "title", Order = 1)]
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [YamlMember(Alias = "category", Order = 2)]
    [JsonPropertyName("category")]
    public string Category { get; set; } = "Maintainability"; // Architecture, Performance, Maintainability, IdiomaticPractices, Robustness

    [YamlMember(Alias = "severity", Order = 3)]
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "Minor"; // Critical, Major, Minor, Info

    [YamlMember(Alias = "file", Order = 4)]
    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;

    [YamlMember(Alias = "symbol", Order = 5)]
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [YamlMember(Alias = "description", Order = 6)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "recommendation", Order = 7)]
    [JsonPropertyName("recommendation")]
    public string Recommendation { get; set; } = string.Empty;
}

public sealed class CodeReviewFindingJsonConverter : JsonConverter<CodeReviewFinding>
{
    public override CodeReviewFinding Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString() ?? string.Empty;
            return new CodeReviewFinding { Title = str, Description = str, Recommendation = string.Empty };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var item = new CodeReviewFinding();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return item;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var prop = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (prop == "title") item.Title = reader.GetString() ?? string.Empty;
                    else if (prop == "category") item.Category = reader.GetString() ?? "Maintainability";
                    else if (prop == "severity") item.Severity = reader.GetString() ?? "Minor";
                    else if (prop == "file") item.File = reader.GetString() ?? string.Empty;
                    else if (prop == "symbol") item.Symbol = reader.GetString() ?? string.Empty;
                    else if (prop == "description") item.Description = reader.GetString() ?? string.Empty;
                    else if (prop == "recommendation") item.Recommendation = reader.GetString() ?? string.Empty;
                }
            }
            return item;
        }

        return new CodeReviewFinding();
    }

    public override void Write(Utf8JsonWriter writer, CodeReviewFinding value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("title", value.Title);
        writer.WriteString("category", value.Category);
        writer.WriteString("severity", value.Severity);
        if (!string.IsNullOrEmpty(value.File)) writer.WriteString("file", value.File);
        if (!string.IsNullOrEmpty(value.Symbol)) writer.WriteString("symbol", value.Symbol);
        writer.WriteString("description", value.Description);
        if (!string.IsNullOrEmpty(value.Recommendation)) writer.WriteString("recommendation", value.Recommendation);
        writer.WriteEndObject();
    }
}

public sealed class RiskSummarySpec
{
    [YamlMember(Alias = "overallRiskLevel", Order = 1)]
    [JsonPropertyName("overallRiskLevel")]
    public string OverallRiskLevel { get; set; } = "Moderate"; // Critical, High, Moderate, Low

    [YamlMember(Alias = "productionReadiness", Order = 2)]
    [JsonPropertyName("productionReadiness")]
    public string ProductionReadiness { get; set; } = "Conditional"; // Approved, Conditional, Blocked

    [YamlMember(Alias = "executiveSummary", Order = 3)]
    [JsonPropertyName("executiveSummary")]
    public string ExecutiveSummary { get; set; } = string.Empty;

    [YamlMember(Alias = "blastRadiusEvaluation", Order = 4)]
    [JsonPropertyName("blastRadiusEvaluation")]
    public string BlastRadiusEvaluation { get; set; } = string.Empty;

    [YamlMember(Alias = "restrictedEnvironmentCompliance", Order = 5)]
    [JsonPropertyName("restrictedEnvironmentCompliance")]
    public string RestrictedEnvironmentCompliance { get; set; } = string.Empty;

    [YamlMember(Alias = "risks", Order = 6)]
    [JsonPropertyName("risks")]
    public List<RiskItem> Risks { get; set; } = new();
}

[JsonConverter(typeof(RiskItemJsonConverter))]
public class RiskItem
{
    [YamlMember(Alias = "riskTitle", Order = 1)]
    [JsonPropertyName("riskTitle")]
    public string RiskTitle { get; set; } = string.Empty;

    [YamlMember(Alias = "riskLevel", Order = 2)]
    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; set; } = "High"; // Critical, High, Medium, Low

    [YamlMember(Alias = "impact", Order = 3)]
    [JsonPropertyName("impact")]
    public string Impact { get; set; } = string.Empty;

    [YamlMember(Alias = "likelihood", Order = 4)]
    [JsonPropertyName("likelihood")]
    public string Likelihood { get; set; } = "Medium"; // High, Medium, Low

    [YamlMember(Alias = "triggerScenario", Order = 5)]
    [JsonPropertyName("triggerScenario")]
    public string TriggerScenario { get; set; } = string.Empty;

    [YamlMember(Alias = "requiredMitigation", Order = 6)]
    [JsonPropertyName("requiredMitigation")]
    public string RequiredMitigation { get; set; } = string.Empty;
}

public sealed class RiskItemJsonConverter : JsonConverter<RiskItem>
{
    public override RiskItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString() ?? string.Empty;
            return new RiskItem { RiskTitle = str, TriggerScenario = str };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var item = new RiskItem();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return item;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var prop = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (prop is "risktitle" or "title" or "name") item.RiskTitle = reader.GetString() ?? string.Empty;
                    else if (prop is "risklevel" or "severity" or "level") item.RiskLevel = reader.GetString() ?? "High";
                    else if (prop is "impact") item.Impact = reader.GetString() ?? string.Empty;
                    else if (prop is "likelihood") item.Likelihood = reader.GetString() ?? "Medium";
                    else if (prop is "triggerscenario" or "trigger" or "description") item.TriggerScenario = reader.GetString() ?? string.Empty;
                    else if (prop is "requiredmitigation" or "mitigation") item.RequiredMitigation = reader.GetString() ?? string.Empty;
                }
            }
            return item;
        }

        return new RiskItem();
    }

    public override void Write(Utf8JsonWriter writer, RiskItem value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("riskTitle", value.RiskTitle);
        writer.WriteString("riskLevel", value.RiskLevel);
        writer.WriteString("impact", value.Impact);
        writer.WriteString("likelihood", value.Likelihood);
        writer.WriteString("triggerScenario", value.TriggerScenario);
        writer.WriteString("requiredMitigation", value.RequiredMitigation);
        writer.WriteEndObject();
    }
}

// Backward-compatibility alias
public sealed class TopRiskItem : RiskItem { }

public sealed class ThreatModelSpec
{
    [YamlMember(Alias = "methodology", Order = 1)]
    [JsonPropertyName("methodology")]
    public string Methodology { get; set; } = "STRIDE";

    [YamlMember(Alias = "attackSurfaceSummary", Order = 2)]
    [JsonPropertyName("attackSurfaceSummary")]
    public string AttackSurfaceSummary { get; set; } = string.Empty;

    [YamlMember(Alias = "trustBoundaries", Order = 3)]
    [JsonPropertyName("trustBoundaries")]
    public List<TrustBoundary> TrustBoundaries { get; set; } = new();

    [YamlMember(Alias = "threats", Order = 4)]
    [JsonPropertyName("threats")]
    public List<ThreatVector> Threats { get; set; } = new();
}

[JsonConverter(typeof(TrustBoundaryJsonConverter))]
public sealed class TrustBoundary
{
    [YamlMember(Alias = "name", Order = 1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "description", Order = 2)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "assetsInside", Order = 3)]
    [JsonPropertyName("assetsInside")]
    public List<string> AssetsInside { get; set; } = new();
}

public sealed class TrustBoundaryJsonConverter : JsonConverter<TrustBoundary>
{
    public override TrustBoundary Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString() ?? string.Empty;
            return new TrustBoundary { Name = str, Description = str };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var item = new TrustBoundary();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return item;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var prop = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (prop is "name" or "id" or "title") item.Name = reader.GetString() ?? string.Empty;
                    else if (prop is "description") item.Description = reader.GetString() ?? string.Empty;
                    else if (prop is "assetsinside" or "assets")
                    {
                        if (reader.TokenType == JsonTokenType.StartArray)
                        {
                            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                            {
                                if (reader.TokenType == JsonTokenType.String) item.AssetsInside.Add(reader.GetString() ?? string.Empty);
                            }
                        }
                    }
                }
            }
            return item;
        }

        return new TrustBoundary();
    }

    public override void Write(Utf8JsonWriter writer, TrustBoundary value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        writer.WriteString("description", value.Description);
        writer.WriteStartArray("assetsInside");
        foreach (var a in value.AssetsInside) writer.WriteStringValue(a);
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}

[JsonConverter(typeof(ThreatVectorJsonConverter))]
public sealed class ThreatVector
{
    [YamlMember(Alias = "id", Order = 1)]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty; // e.g. T-01, STRIDE-01

    [YamlMember(Alias = "strideCategory", Order = 2)]
    [JsonPropertyName("strideCategory")]
    public string StrideCategory { get; set; } = "Tampering"; // Spoofing, Tampering, Repudiation, InformationDisclosure, DenialOfService, ElevationOfPrivilege

    [YamlMember(Alias = "targetAsset", Order = 3)]
    [JsonPropertyName("targetAsset")]
    public string TargetAsset { get; set; } = string.Empty;

    [YamlMember(Alias = "threatScenario", Order = 4)]
    [JsonPropertyName("threatScenario")]
    public string ThreatScenario { get; set; } = string.Empty;

    [YamlMember(Alias = "severity", Order = 5)]
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "High"; // Critical, High, Medium, Low

    [YamlMember(Alias = "mitigationControl", Order = 6)]
    [JsonPropertyName("mitigationControl")]
    public string MitigationControl { get; set; } = string.Empty;

    [YamlMember(Alias = "residualRisk", Order = 7)]
    [JsonPropertyName("residualRisk")]
    public string ResidualRisk { get; set; } = "Low"; // Low, Medium, High
}

public sealed class ThreatVectorJsonConverter : JsonConverter<ThreatVector>
{
    public override ThreatVector Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString() ?? string.Empty;
            return new ThreatVector { Id = "THREAT", ThreatScenario = str, MitigationControl = str };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var item = new ThreatVector();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return item;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var prop = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (prop is "id") item.Id = reader.GetString() ?? string.Empty;
                    else if (prop is "stridecategory" or "category") item.StrideCategory = reader.GetString() ?? "Tampering";
                    else if (prop is "targetasset" or "target") item.TargetAsset = reader.GetString() ?? string.Empty;
                    else if (prop is "threatscenario" or "scenario" or "description" or "threat") item.ThreatScenario = reader.GetString() ?? string.Empty;
                    else if (prop is "severity") item.Severity = reader.GetString() ?? "High";
                    else if (prop is "mitigationcontrol" or "mitigation" or "controls") item.MitigationControl = reader.GetString() ?? string.Empty;
                    else if (prop is "residualrisk") item.ResidualRisk = reader.GetString() ?? "Low";
                }
            }
            return item;
        }

        return new ThreatVector();
    }

    public override void Write(Utf8JsonWriter writer, ThreatVector value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteString("strideCategory", value.StrideCategory);
        writer.WriteString("targetAsset", value.TargetAsset);
        writer.WriteString("threatScenario", value.ThreatScenario);
        writer.WriteString("severity", value.Severity);
        writer.WriteString("mitigationControl", value.MitigationControl);
        writer.WriteString("residualRisk", value.ResidualRisk);
        writer.WriteEndObject();
    }
}

public sealed class FunctionalSpecs
{
    [YamlMember(Alias = "capabilities", Order = 1)]
    [JsonPropertyName("capabilities")]
    public List<CapabilityItem> Capabilities { get; set; } = new();

    [YamlMember(Alias = "useCases", Order = 2)]
    [JsonPropertyName("useCases")]
    public List<BusinessUseCase> UseCases { get; set; } = new();

    [YamlMember(Alias = "stateMachines", Order = 3)]
    [JsonPropertyName("stateMachines")]
    public List<DomainStateMachine> StateMachines { get; set; } = new();

    [YamlMember(Alias = "businessRulesAndFormulas", Order = 4)]
    [JsonPropertyName("businessRulesAndFormulas")]
    public List<BusinessRuleSpecification> BusinessRulesAndFormulas { get; set; } = new();

    [YamlMember(Alias = "dataDictionary", Order = 5)]
    [JsonPropertyName("dataDictionary")]
    public List<DataDictionaryEntity> DataDictionary { get; set; } = new();

    [YamlMember(Alias = "invariants", Order = 6)]
    [JsonPropertyName("invariants")]
    public List<SystemInvariant> Invariants { get; set; } = new();
}

public sealed class DomainStateMachine
{
    [YamlMember(Alias = "entityName", Order = 1)]
    [JsonPropertyName("entityName")]
    public string EntityName { get; set; } = string.Empty; // e.g. "LoanApplication", "MortgageMutation"

    [YamlMember(Alias = "initialState", Order = 2)]
    [JsonPropertyName("initialState")]
    public string InitialState { get; set; } = string.Empty;

    [YamlMember(Alias = "states", Order = 3)]
    [JsonPropertyName("states")]
    public List<string> States { get; set; } = new();

    [YamlMember(Alias = "transitions", Order = 4)]
    [JsonPropertyName("transitions")]
    public List<StateTransition> Transitions { get; set; } = new();

    [YamlMember(Alias = "mermaidDiagram", Order = 5)]
    [JsonPropertyName("mermaidDiagram")]
    public string MermaidDiagram { get; set; } = string.Empty;
}

public sealed class StateTransition
{
    [YamlMember(Alias = "fromState", Order = 1)]
    [JsonPropertyName("fromState")]
    public string FromState { get; set; } = string.Empty;

    [YamlMember(Alias = "triggerEvent", Order = 2)]
    [JsonPropertyName("triggerEvent")]
    public string TriggerEvent { get; set; } = string.Empty;

    [YamlMember(Alias = "toState", Order = 3)]
    [JsonPropertyName("toState")]
    public string ToState { get; set; } = string.Empty;

    [YamlMember(Alias = "guardCondition", Order = 4)]
    [JsonPropertyName("guardCondition")]
    public string? GuardCondition { get; set; }

    [YamlMember(Alias = "actionEffect", Order = 5)]
    [JsonPropertyName("actionEffect")]
    public string? ActionEffect { get; set; }
}

public sealed class BusinessRuleSpecification
{
    [YamlMember(Alias = "ruleId", Order = 1)]
    [JsonPropertyName("ruleId")]
    public string RuleId { get; set; } = string.Empty; // e.g. BR-01, FORMULA-02

    [YamlMember(Alias = "category", Order = 2)]
    [JsonPropertyName("category")]
    public string Category { get; set; } = "Calculation"; // Calculation, Validation, Policy, Workflow, Eligibility

    [YamlMember(Alias = "ruleTitle", Order = 3)]
    [JsonPropertyName("ruleTitle")]
    public string RuleTitle { get; set; } = string.Empty;

    [YamlMember(Alias = "formalLogicOrFormula", Order = 4)]
    [JsonPropertyName("formalLogicOrFormula")]
    public string FormalLogicOrFormula { get; set; } = string.Empty; // e.g. "InterestDue = Principal * (Rate / 100) * (Days / 360)"

    [YamlMember(Alias = "description", Order = 5)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "constraintOrValidation", Order = 6)]
    [JsonPropertyName("constraintOrValidation")]
    public string ConstraintOrValidation { get; set; } = string.Empty;

    [YamlMember(Alias = "errorMessage", Order = 7)]
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

public sealed class DataDictionaryEntity
{
    [YamlMember(Alias = "entityName", Order = 1)]
    [JsonPropertyName("entityName")]
    public string EntityName { get; set; } = string.Empty; // e.g. "MortgageAccount", "LoanMutation"

    [YamlMember(Alias = "description", Order = 2)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "primaryKey", Order = 3)]
    [JsonPropertyName("primaryKey")]
    public string PrimaryKey { get; set; } = "id";

    [YamlMember(Alias = "fields", Order = 4)]
    [JsonPropertyName("fields")]
    public List<DataDictionaryField> Fields { get; set; } = new();
}

[JsonConverter(typeof(DataDictionaryFieldJsonConverter))]
public sealed class DataDictionaryField
{
    [YamlMember(Alias = "name", Order = 1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "dataType", Order = 2)]
    [JsonPropertyName("dataType")]
    public string DataType { get; set; } = "string"; // string, integer, decimal, boolean, datetime, uuid

    [YamlMember(Alias = "required", Order = 3)]
    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [YamlMember(Alias = "constraints", Order = 4)]
    [JsonPropertyName("constraints")]
    public string? Constraints { get; set; } // e.g. "Min: 0, Max: 1000000", "Regex: ^[A-Z]{3}$"

    [YamlMember(Alias = "description", Order = 5)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "exampleValue", Order = 6)]
    [JsonPropertyName("exampleValue")]
    public string? ExampleValue { get; set; }
}

public sealed class DataDictionaryFieldJsonConverter : JsonConverter<DataDictionaryField>
{
    public override DataDictionaryField Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString() ?? string.Empty;
            return new DataDictionaryField { Name = str, Description = str };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var item = new DataDictionaryField();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    if (string.IsNullOrWhiteSpace(item.Name) && !string.IsNullOrWhiteSpace(item.Description))
                    {
                        var words = item.Description.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        item.Name = words.Length > 0 ? words[0] : "Field";
                    }
                    return item;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var prop = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (prop is "name" or "fieldname" or "column" or "property" or "attribute") item.Name = reader.GetString() ?? string.Empty;
                    else if (prop is "datatype" or "type") item.DataType = reader.GetString() ?? "string";
                    else if (prop is "required" or "isrequired" or "nullable")
                    {
                        if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False) item.Required = reader.GetBoolean();
                        else if (reader.TokenType == JsonTokenType.String && bool.TryParse(reader.GetString(), out var b)) item.Required = b;
                    }
                    else if (prop is "constraints" or "constraint" or "validation") item.Constraints = reader.GetString();
                    else if (prop is "description" or "desc") item.Description = reader.GetString() ?? string.Empty;
                    else if (prop is "examplevalue" or "example") item.ExampleValue = reader.GetString();
                }
            }
            return item;
        }

        return new DataDictionaryField();
    }

    public override void Write(Utf8JsonWriter writer, DataDictionaryField value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        writer.WriteString("dataType", value.DataType);
        writer.WriteBoolean("required", value.Required);
        if (!string.IsNullOrEmpty(value.Constraints)) writer.WriteString("constraints", value.Constraints);
        writer.WriteString("description", value.Description);
        if (!string.IsNullOrEmpty(value.ExampleValue)) writer.WriteString("exampleValue", value.ExampleValue);
        writer.WriteEndObject();
    }
}

[JsonConverter(typeof(SystemInvariantJsonConverter))]
public sealed class SystemInvariant
{
    [YamlMember(Alias = "id", Order = 1)]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty; // e.g. INV-01

    [YamlMember(Alias = "description", Order = 2)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "enforcementMechanism", Order = 3)]
    [JsonPropertyName("enforcementMechanism")]
    public string EnforcementMechanism { get; set; } = "Transactional Boundary"; // Transactional Boundary, Database Constraint, Optimistic Concurrency, Domain Event

    [YamlMember(Alias = "concurrencyRequirement", Order = 4)]
    [JsonPropertyName("concurrencyRequirement")]
    public string ConcurrencyRequirement { get; set; } = "Serializable / ROWLOCK";

    [YamlMember(Alias = "violationSeverity", Order = 5)]
    [JsonPropertyName("violationSeverity")]
    public string ViolationSeverity { get; set; } = "Critical";
}

public sealed class SystemInvariantJsonConverter : JsonConverter<SystemInvariant>
{
    public override SystemInvariant Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString() ?? string.Empty;
            return new SystemInvariant { Id = "INV", Description = str };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var item = new SystemInvariant();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return item;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var prop = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (prop is "id" or "invariantid") item.Id = reader.GetString() ?? string.Empty;
                    else if (prop is "description" or "statement" or "rule") item.Description = reader.GetString() ?? string.Empty;
                    else if (prop is "enforcementmechanism" or "enforcement") item.EnforcementMechanism = reader.GetString() ?? "Transactional Boundary";
                    else if (prop is "concurrencyrequirement" or "concurrency") item.ConcurrencyRequirement = reader.GetString() ?? "Serializable / ROWLOCK";
                    else if (prop is "violationseverity" or "severity") item.ViolationSeverity = reader.GetString() ?? "Critical";
                }
            }
            return item;
        }

        return new SystemInvariant();
    }

    public override void Write(Utf8JsonWriter writer, SystemInvariant value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteString("description", value.Description);
        writer.WriteString("enforcementMechanism", value.EnforcementMechanism);
        writer.WriteString("concurrencyRequirement", value.ConcurrencyRequirement);
        writer.WriteString("violationSeverity", value.ViolationSeverity);
        writer.WriteEndObject();
    }
}

[JsonConverter(typeof(CapabilityItemJsonConverter))]
public sealed class CapabilityItem
{
    [YamlMember(Alias = "name", Order = 1)]
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "description", Order = 2)]
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [YamlMember(Alias = "businessOutcome", Order = 3)]
    [JsonPropertyName("businessOutcome")]
    public string BusinessOutcome { get; set; } = string.Empty;
}

public sealed class CapabilityItemJsonConverter : JsonConverter<CapabilityItem>
{
    public override CapabilityItem Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString() ?? string.Empty;
            return new CapabilityItem { Name = str, Description = str, BusinessOutcome = str };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var item = new CapabilityItem();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return item;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var prop = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (prop is "name" or "title" or "capability") item.Name = reader.GetString() ?? string.Empty;
                    else if (prop is "description") item.Description = reader.GetString() ?? string.Empty;
                    else if (prop is "businessoutcome" or "outcome") item.BusinessOutcome = reader.GetString() ?? string.Empty;
                }
            }
            return item;
        }

        return new CapabilityItem();
    }

    public override void Write(Utf8JsonWriter writer, CapabilityItem value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("name", value.Name);
        writer.WriteString("description", value.Description);
        writer.WriteString("businessOutcome", value.BusinessOutcome);
        writer.WriteEndObject();
    }
}

public sealed class BusinessUseCase
{
    [YamlMember(Alias = "id", Order = 1)]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty; // e.g. UC-01

    [YamlMember(Alias = "title", Order = 2)]
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [YamlMember(Alias = "capability", Order = 3)]
    [JsonPropertyName("capability")]
    public string Capability { get; set; } = string.Empty;

    [YamlMember(Alias = "primaryActor", Order = 4)]
    [JsonPropertyName("primaryActor")]
    public string PrimaryActor { get; set; } = string.Empty;

    [YamlMember(Alias = "businessValue", Order = 5)]
    [JsonPropertyName("businessValue")]
    public string BusinessValue { get; set; } = string.Empty;

    [YamlMember(Alias = "trigger", Order = 6)]
    [JsonPropertyName("trigger")]
    public string Trigger { get; set; } = string.Empty;

    [YamlMember(Alias = "preconditions", Order = 7)]
    [JsonPropertyName("preconditions")]
    public List<string> Preconditions { get; set; } = new();

    [YamlMember(Alias = "mainFlow", Order = 8)]
    [JsonPropertyName("mainFlow")]
    public List<string> MainFlow { get; set; } = new();

    [YamlMember(Alias = "businessRules", Order = 9)]
    [JsonPropertyName("businessRules")]
    public List<string> BusinessRules { get; set; } = new();

    [YamlMember(Alias = "inputDataContracts", Order = 10)]
    [JsonPropertyName("inputDataContracts")]
    public List<string> InputDataContracts { get; set; } = new();

    [YamlMember(Alias = "alternativeAndExceptionFlows", Order = 11)]
    [JsonPropertyName("alternativeAndExceptionFlows")]
    public List<string> AlternativeAndExceptionFlows { get; set; } = new();

    [YamlMember(Alias = "outputStateChanges", Order = 12)]
    [JsonPropertyName("outputStateChanges")]
    public List<string> OutputStateChanges { get; set; } = new();

    [YamlMember(Alias = "acceptanceScenarios", Order = 13)]
    [JsonPropertyName("acceptanceScenarios")]
    public List<BddScenario> AcceptanceScenarios { get; set; } = new();

    [YamlMember(Alias = "architecturalAdvice", Order = 14)]
    [JsonPropertyName("architecturalAdvice")]
    public string ArchitecturalAdvice { get; set; } = string.Empty;

    [YamlMember(Alias = "associatedComponents", Order = 15)]
    [JsonPropertyName("associatedComponents")]
    public List<string> AssociatedComponents { get; set; } = new();

    [YamlMember(Alias = "associatedApis", Order = 16)]
    [JsonPropertyName("associatedApis")]
    public List<string> AssociatedApis { get; set; } = new();
}

[JsonConverter(typeof(BddScenarioJsonConverter))]
public sealed class BddScenario
{
    [YamlMember(Alias = "scenarioTitle", Order = 1)]
    [JsonPropertyName("scenarioTitle")]
    public string ScenarioTitle { get; set; } = string.Empty;

    [YamlMember(Alias = "given", Order = 2)]
    [JsonPropertyName("given")]
    public string Given { get; set; } = string.Empty;

    [YamlMember(Alias = "when", Order = 3)]
    [JsonPropertyName("when")]
    public string When { get; set; } = string.Empty;

    [YamlMember(Alias = "then", Order = 4)]
    [JsonPropertyName("then")]
    public string Then { get; set; } = string.Empty;
}

public sealed class BddScenarioJsonConverter : JsonConverter<BddScenario>
{
    public override BddScenario Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString() ?? string.Empty;
            return new BddScenario { ScenarioTitle = str, Given = str, When = str, Then = str };
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            var item = new BddScenario();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject) return item;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var prop = reader.GetString()?.ToLowerInvariant();
                    reader.Read();
                    if (prop is "scenariotitle" or "title" or "scenario" or "name") item.ScenarioTitle = reader.GetString() ?? string.Empty;
                    else if (prop is "given") item.Given = reader.GetString() ?? string.Empty;
                    else if (prop is "when") item.When = reader.GetString() ?? string.Empty;
                    else if (prop is "then") item.Then = reader.GetString() ?? string.Empty;
                }
            }
            return item;
        }

        return new BddScenario();
    }

    public override void Write(Utf8JsonWriter writer, BddScenario value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("scenarioTitle", value.ScenarioTitle);
        writer.WriteString("given", value.Given);
        writer.WriteString("when", value.When);
        writer.WriteString("then", value.Then);
        writer.WriteEndObject();
    }
}

public sealed class DeploymentSpec
{
    [YamlMember(Alias = "primaryPlatform", Order = 1)]
    [JsonPropertyName("primaryPlatform")]
    public string PrimaryPlatform { get; set; } = "Kubernetes";

    [YamlMember(Alias = "environments", Order = 2)]
    [JsonPropertyName("environments")]
    public List<EnvironmentDeployment> Environments { get; set; } = new();
}

public sealed class EnvironmentDeployment
{
    [YamlMember(Alias = "environment", Order = 1)]
    [JsonPropertyName("environment")]
    public string Environment { get; set; } = "production";

    [YamlMember(Alias = "platform", Order = 2)]
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "Kubernetes";

    [YamlMember(Alias = "clusterOrHost", Order = 3)]
    [JsonPropertyName("clusterOrHost")]
    public string ClusterOrHost { get; set; } = string.Empty;

    [YamlMember(Alias = "namespaceOrPath", Order = 4)]
    [JsonPropertyName("namespaceOrPath")]
    public string NamespaceOrPath { get; set; } = string.Empty;

    [YamlMember(Alias = "ipAddress", Order = 5)]
    [JsonPropertyName("ipAddress")]
    public string? IpAddress { get; set; }

    [YamlMember(Alias = "os", Order = 6)]
    [JsonPropertyName("os")]
    public string? Os { get; set; }

    [YamlMember(Alias = "ingress", Order = 7)]
    [JsonPropertyName("ingress")]
    public IngressConfig Ingress { get; set; } = new();

    [YamlMember(Alias = "orchestration", Order = 8)]
    [JsonPropertyName("orchestration")]
    public OrchestrationConfig Orchestration { get; set; } = new();

    [YamlMember(Alias = "lastDeployedAt", Order = 9)]
    [JsonPropertyName("lastDeployedAt")]
    public DateTime? LastDeployedAt { get; set; }

    [YamlMember(Alias = "deployedBy", Order = 10)]
    [JsonPropertyName("deployedBy")]
    public string? DeployedBy { get; set; }
}

public sealed class IngressConfig
{
    [YamlMember(Alias = "publicUrl", Order = 1)]
    [JsonPropertyName("publicUrl")]
    public string? PublicUrl { get; set; }

    [YamlMember(Alias = "internalHost", Order = 2)]
    [JsonPropertyName("internalHost")]
    public string? InternalHost { get; set; }

    [YamlMember(Alias = "exposure", Order = 3)]
    [JsonPropertyName("exposure")]
    public string Exposure { get; set; } = "InternalOnly";

    [YamlMember(Alias = "tlsTermination", Order = 4)]
    [JsonPropertyName("tlsTermination")]
    public string? TlsTermination { get; set; }
}

public sealed class OrchestrationConfig
{
    [YamlMember(Alias = "tool", Order = 1)]
    [JsonPropertyName("tool")]
    public string Tool { get; set; } = "ArgoCD";

    [YamlMember(Alias = "imageOrArtifact", Order = 2)]
    [JsonPropertyName("imageOrArtifact")]
    public string? ImageOrArtifact { get; set; }

    [YamlMember(Alias = "gitCommit", Order = 3)]
    [JsonPropertyName("gitCommit")]
    public string? GitCommit { get; set; }

    [YamlMember(Alias = "replicas", Order = 4)]
    [JsonPropertyName("replicas")]
    public int Replicas { get; set; } = 1;
}
