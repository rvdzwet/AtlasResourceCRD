using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace AtlasResourceCRD.Core.Models;

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
}

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

public sealed class QualityVerdictSpec
{
    [YamlMember(Alias = "sigStars", Order = 1)]
    [JsonPropertyName("sigStars")]
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
    public int Stars { get; set; } = 4; // 1-5

    [YamlMember(Alias = "evaluation", Order = 3)]
    [JsonPropertyName("evaluation")]
    public string Evaluation { get; set; } = string.Empty;
}

public sealed class CodeReviewSpec
{
    [YamlMember(Alias = "reviewGrade", Order = 1)]
    [JsonPropertyName("reviewGrade")]
    public string ReviewGrade { get; set; } = "A"; // A+, A, B, C, D, F

    [YamlMember(Alias = "reviewScore", Order = 2)]
    [JsonPropertyName("reviewScore")]
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

    [YamlMember(Alias = "topRisks", Order = 6)]
    [JsonPropertyName("topRisks")]
    public List<TopRiskItem> TopRisks { get; set; } = new();
}

public sealed class TopRiskItem
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
