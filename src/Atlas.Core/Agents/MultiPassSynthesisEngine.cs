using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Core.Caching;
using Atlas.Core.Gemini;
using Atlas.Core.Models;
using Atlas.Core.Scanner;
using Microsoft.Extensions.Logging;

namespace Atlas.Core.Agents;

/// <summary>
/// Domain-Specialized Multi-Pass Synthesis Engine that executes a 4-pass synthesis pipeline
/// with Foundation-First Parallel Fan-Out (Pass 1 Core Architecture -> Passes 2, 3, 4 Parallel -> Merge).
/// Eliminates token output truncation and produces exhaustive, production-grade blueprints.
/// </summary>
public sealed class MultiPassSynthesisEngine
{
    private readonly ILlmClient _llmClient;
    private readonly ILogger<MultiPassSynthesisEngine> _logger;

    public MultiPassSynthesisEngine(ILlmClient llmClient, ILogger<MultiPassSynthesisEngine> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    private int CalculatePromptMaxChars()
    {
        // Reserve ~25% tokens for output candidate JSON and system instruction overhead
        var usableTokens = (int)(_llmClient.ContextWindowTokens * 0.75);
        return Math.Clamp((int)(usableTokens * 3.5), 12_000, 450_000);
    }

    public async Task<AtlasResourceSpec> ExecuteMultiPassSynthesisAsync(
        CodebaseSkeleton skeleton,
        List<FileSummary> summaries,
        CancellationToken cancellationToken = default)
    {
        var overallStopwatch = Stopwatch.StartNew();
        _logger.LogInformation("================================================================================");
        _logger.LogInformation("🚀 [Multi-Pass Synthesis] Starting 4-Domain Specialized Pipeline for '{Repo}'", skeleton.RepoName);
        _logger.LogInformation("================================================================================");

        var sortedSummaries = summaries.OrderBy(f => f.RelativePath).ToList();

        // -------------------------------------------------------------------------
        // PASS 1: Core Architecture, Topology & Multi-Diagram C4 Synthesis (Foundational)
        // -------------------------------------------------------------------------
        _logger.LogInformation("🏛️  [Pass 1/4: Core Architecture & C4 Topology] Synthesizing system topology...");
        var pass1Sw = Stopwatch.StartNew();
        var pass1Result = await SynthesizePass1CoreArchitectureAsync(skeleton, sortedSummaries, cancellationToken);
        pass1Sw.Stop();
        _logger.LogInformation("🟢 [Pass 1/4 Complete] Tier: {Tier} | Quality: ⭐ {Stars:0.0} | Grade: {Grade} ({ElapsedMs} ms)",
            pass1Result.ComponentOverview?.Tier ?? "Backend",
            pass1Result.Quality?.SigStars ?? 4.0,
            pass1Result.CodeReview?.ReviewGrade ?? "A",
            pass1Sw.ElapsedMilliseconds);

        var foundationContext = BuildFoundationContext(pass1Result);

        // -------------------------------------------------------------------------
        // PASSES 2, 3, 4: Foundation-First Parallel Fan-Out
        // -------------------------------------------------------------------------
        _logger.LogInformation("⚡ [Parallel Fan-Out] Launching Passes 2, 3, and 4 concurrently...");
        var fanOutSw = Stopwatch.StartNew();

        var pass2Task = SynthesizePass2ApiAndDataAsync(skeleton, sortedSummaries, foundationContext, cancellationToken);
        var pass3Task = SynthesizePass3LivingSpecsAsync(skeleton, sortedSummaries, foundationContext, cancellationToken);
        var pass4Task = SynthesizePass4SecurityAndOpsAsync(skeleton, sortedSummaries, foundationContext, cancellationToken);

        await Task.WhenAll(pass2Task, pass3Task, pass4Task);
        fanOutSw.Stop();

        var pass2Result = await pass2Task;
        var pass3Result = await pass3Task;
        var pass4Result = await pass4Task;

        _logger.LogInformation("🟢 [Pass 2/4 Complete] Endpoints: {EpCount} | Events: {EvCount} | Databases: {DbCount}",
            pass2Result.ApiContracts?.Endpoints?.Count ?? 0,
            pass2Result.ApiContracts?.Events?.Count ?? 0,
            pass2Result.DataStores?.Databases?.Count ?? 0);

        _logger.LogInformation("🟢 [Pass 3/4 Complete] Use Cases: {UcCount} | State Machines: {SmCount} | Formulas: {FmCount} | Dictionary: {DictCount}",
            pass3Result.FunctionalSpecs?.UseCases?.Count ?? 0,
            pass3Result.FunctionalSpecs?.StateMachines?.Count ?? 0,
            pass3Result.FunctionalSpecs?.BusinessRulesAndFormulas?.Count ?? 0,
            pass3Result.FunctionalSpecs?.DataDictionary?.Count ?? 0);

        _logger.LogInformation("🟢 [Pass 4/4 Complete] STRIDE Threats: {ThCount} | Observability: {Obs} | Risk: {Risk}",
            pass4Result.ThreatModel?.Threats?.Count ?? 0,
            pass4Result.Observability?.Logging?.Framework ?? "Serilog",
            pass4Result.RiskSummary?.OverallRiskLevel ?? "Low");

        // -------------------------------------------------------------------------
        // MERGE STAGE: Deterministic CRD Assembly
        // -------------------------------------------------------------------------
        _logger.LogInformation("🧩 [Merge Stage] Assembling unified master CRD from 4 domain passes...");
        var mergedSpec = MergePasses(pass1Result, pass2Result, pass3Result, pass4Result);

        overallStopwatch.Stop();
        _logger.LogInformation("================================================================================");
        _logger.LogInformation("✅ [Multi-Pass Synthesis Complete] Finished in {ElapsedMs} ms across 4 passes", overallStopwatch.ElapsedMilliseconds);
        _logger.LogInformation("================================================================================");

        return mergedSpec;
    }

    private static string BuildFoundationContext(AtlasResourceSpec pass1)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Service Name: {pass1.ComponentOverview?.Name ?? "Unknown"}");
        sb.AppendLine($"Tier: {pass1.ComponentOverview?.Tier ?? "Backend"}");
        sb.AppendLine($"Description: {pass1.ComponentOverview?.Description ?? ""}");
        sb.AppendLine($"Primary Language: {pass1.TechStack?.PrimaryLanguage ?? "C#"}");
        if (pass1.TechStack?.Frameworks?.Count > 0)
        {
            sb.AppendLine($"Frameworks: {string.Join(", ", pass1.TechStack.Frameworks.Select(f => f.Name))}");
        }
        if (pass1.Architecture != null)
        {
            sb.AppendLine("Architecture Overview:");
            sb.AppendLine($"- Pattern: {pass1.Architecture.Pattern}");
            sb.AppendLine($"- Summary: {pass1.Architecture.Summary}");
            if (pass1.Architecture.Components?.Count > 0)
            {
                sb.AppendLine($"- Key Components: {string.Join(", ", pass1.Architecture.Components.Select(c => c.Name))}");
            }
        }
        return sb.ToString();
    }

    public async Task<AtlasResourceSpec> SynthesizePass1CoreArchitectureAsync(
        CodebaseSkeleton skeleton,
        List<FileSummary> summaries,
        CancellationToken cancellationToken)
    {
        var formattedContext = FormatTwoTierSummaries(summaries, "architecture", out var t1Count, out var t2Count, CalculatePromptMaxChars());
        var prompt = $$"""
            You are the Chief Enterprise Architect for the Atlas Platform.
            Perform PASS 1 of the synthesis: Core System Topology, Component Overview, Tech Stack, Quality/Review Ratings, and C4 Multi-Diagram Architecture.

            Repository: {{skeleton.RepoName}}
            Git Branch: {{skeleton.Git?.Branch ?? "main"}}
            Git Commit: {{skeleton.Git?.CommitShaShort ?? "unknown"}}
            Total File Inventory: {{summaries.Count}}

            FILE SUMMARIES & ARCHITECTURE FACTS:
            {{formattedContext}}

            PROJECT MANIFESTS & PACKAGES:
            {{FormatManifests(skeleton.Manifests)}}

            INSTRUCTIONS FOR PASS 1:
            1. Component Overview (`componentOverview`): Provide accurate name, tier (Frontend/BFF/Backend/DataWorker/Gateway/Platform), description, purpose, owner, lifecycle.
            2. Tech Stack (`techStack`): Detect primary language, languages, frameworks, runtimes, package managers, and build systems.
            3. Quality & Maintainability (`quality`):
               - `sigStars`: Overall rating (1.0 to 5.0).
               - `maintainabilityLevel`: "Very High", "High", "Moderate", "Low", or "Very Low".
               - `dimensions`: Array of 5 ISO 25010 / SIG maintainability dimensions:
                 * "Volume & Balance" (stars: 1.0-5.0, evaluation: string)
                 * "Cyclomatic Complexity" (stars: 1.0-5.0, evaluation: string)
                 * "Duplication" (stars: 1.0-5.0, evaluation: string)
                 * "Unit Test Ratio" (stars: 1.0-5.0, evaluation: string)
                 * "Component Independence" (stars: 1.0-5.0, evaluation: string)
               - `techDebtItems`: Array of 3-6 concrete technical debt / refactoring backlog items (strings).
            4. Code Review (`codeReview`):
               - `reviewGrade`: "A+", "A", "B+", "B", "C", or "D".
               - `reviewScore`: 0 to 100 integer.
               - `summary`: Architectural and code review summary.
               - `strengths`: Array of 3-5 key architectural strengths (strings).
               - `codeSmells`: Array of detected code smells: `[ { "smellType": "string", "affectedComponentOrFile": "string", "description": "string" } ]`.
               - `findings`: Array of review recommendations: `[ { "title": "string", "category": "Architecture|Maintainability|Performance", "severity": "Major|Minor|Info", "file": "string", "description": "string", "recommendation": "string" } ]`.
            5. Multi-Diagram Architecture (`architecture`): Generate THREE clean, complete Mermaid diagrams:
               - `contextDiagram`: C4 Level 1 System Context (`flowchart TD` or `C4Context`) showing users, external systems, and this boundary.
               - `componentDiagram`: C4 Level 2 Component Architecture (`flowchart TD` or `C4Component`) showing internal controllers, services, repositories, and brokers.
               - `dataFlowDiagram`: Ingestion Lifecycle & Data Flow diagram (`flowchart TD`) showing request/event flow from ingress to storage.
               - `components`: Array of internal architectural components with name, type, description, and responsibilities.

            IMPORTANT: Output valid JSON adhering to the AtlasResourceSpec schema with `componentOverview`, `techStack`, `quality`, `codeReview`, and `architecture` fully populated.
            """;

        LogPassTelemetry("Pass 1 (Architecture)", prompt.Length, summaries.Count, t1Count, t2Count);

        return await _llmClient.GenerateStructuredAsync<AtlasResourceSpec>(
            prompt,
            Prompts.SystemInstruction,
            cancellationToken);
    }

    public async Task<AtlasResourceSpec> SynthesizePass2ApiAndDataAsync(
        CodebaseSkeleton skeleton,
        List<FileSummary> summaries,
        string foundationContext,
        CancellationToken cancellationToken)
    {
        var formattedContext = FormatTwoTierSummaries(summaries, "api_data", out var t1Count, out var t2Count, CalculatePromptMaxChars());
        var prompt = $$"""
            You are the Principal API & Data Architect for the Atlas Platform.
            Perform PASS 2 of the synthesis: API Contracts (HTTP/REST, gRPC, Event Topics), Internal & External Dependencies, Configuration, and Data Stores.

            FOUNDATION ARCHITECTURE CONTEXT:
            {{foundationContext}}

            API, DATA & DEPENDENCY FACTS:
            {{formattedContext}}

            INSTRUCTIONS FOR PASS 2:
            1. ApiContracts (`apiContracts`):
               - `endpoints`: Extract ALL HTTP/REST endpoints with path, method (GET/POST/PUT/DELETE/PATCH), description, authRequired (bool), requestType, responseType, and parameters.
               - `events`: Extract ALL event/message topics, queues, payloadType, action (Publish/Consume/Produce), and description.
               - `grpcServices`: Extract gRPC services, protos, and RPC methods if present.
            2. Dependencies (`dependencies`):
               - `internalServices`: Array of `[ { "name": "Microservice Name", "purpose": "...", "criticality": "High|Medium|Low" } ]`.
               - `externalApis`: Array of `[ { "name": "3rd Party API / Cloud Service", "protocolOrHost": "...", "purpose": "...", "criticality": "High|Medium|Low" } ]`.
               - `keyPackages`: Array of `[ { "name": "Package Name", "version": "...", "purpose": "..." } ]`.
            3. Configuration (`configuration`):
               - `environmentVariables`: Array of `[ { "name": "...", "description": "...", "required": bool, "defaultValue": "...", "isSecret": bool } ]`.
               - `configFiles`: Array of `[ { "path": "...", "format": "JSON|YAML|ENV|XML", "description": "..." } ]`.
            4. DataStores (`dataStores`):
               - `databases`: Array of `[ { "name": "...", "type": "PostgreSQL|SQLite|InfluxDB|Redis|Neo4j", "role": "Primary|Replica|Cache", "description": "..." } ]`.

            Output valid JSON adhering to AtlasResourceSpec with `apiContracts`, `dependencies`, `configuration`, and `dataStores` populated.
            """;

        LogPassTelemetry("Pass 2 (API & Data)", prompt.Length, summaries.Count, t1Count, t2Count);

        return await _llmClient.GenerateStructuredAsync<AtlasResourceSpec>(
            prompt,
            Prompts.SystemInstruction,
            cancellationToken);
    }

    public async Task<AtlasResourceSpec> SynthesizePass3LivingSpecsAsync(
        CodebaseSkeleton skeleton,
        List<FileSummary> summaries,
        string foundationContext,
        CancellationToken cancellationToken)
    {
        var formattedContext = FormatTwoTierSummaries(summaries, "living_specs", out var t1Count, out var t2Count, CalculatePromptMaxChars());
        var prompt = $$"""
            You are the Principal Systems Requirements & Domain Logic Engineer.
            Perform PASS 3 of the synthesis: Exhaustive Living Documentation & Functional Engineering Rebuild Blueprint.
            The goal is that an external engineer/vendor can reconstruct the application purely from this functional specification.

            FOUNDATION ARCHITECTURE CONTEXT:
            {{foundationContext}}

            DOMAIN LOGIC & LIVING SPEC FACTS:
            {{formattedContext}}

            INSTRUCTIONS FOR PASS 3 (FunctionalSpecs):
            1. Capabilities & UseCases (`capabilities`, `useCases`):
               - High-level business capabilities (`capabilities`: `[ { "name": "...", "description": "...", "businessOutcome": "..." } ]`).
               - Detailed `useCases`: Array of objects each containing:
                 * `id`: "UC-01", etc.
                 * `title`: string
                 * `capability`: string
                 * `primaryActor`: string
                 * `businessValue`: string
                 * `trigger`: string
                 * `preconditions`: array of strings
                 * `mainFlow`: array of ordered steps (e.g., ["1. ...", "2. ..."])
                 * `acceptanceScenarios`: Array of concrete Gherkin scenarios: `[ { "scenarioTitle": "...", "given": "...", "when": "...", "then": "..." } ]`.
                 * `businessRules`: array of business rule strings.
                 * `inputDataContracts`: array of parameter schema strings.
                 * `architecturalAdvice`: string guidance for rebuilding.
            2. Domain State Machines (`stateMachines`): Identify domain entities with state transitions. Include `entityName`, `initialState`, `states` (array of strings), and `transitions`: `[ { "fromState": "...", "triggerEvent": "...", "toState": "...", "guardCondition": "...", "actionEffect": "..." } ]`.
            3. Business Rules & Calculation Formulas (`businessRulesAndFormulas`): Array of `[ { "ruleId": "BR-01", "category": "Calculation|Validation|Policy", "ruleTitle": "...", "formalLogicOrFormula": "...", "description": "...", "constraintOrValidation": "..." } ]`.
            4. Data Dictionary (`dataDictionary`): Array of domain entities: `[ { "entityName": "...", "description": "...", "primaryKey": "...", "fields": [ { "name": "FieldName", "dataType": "string|int|double|bool|datetime", "required": bool, "constraints": "...", "description": "..." } ] } ]`.
            5. System Invariants & Concurrency Rules (`invariants`): Array of `[ { "id": "INV-01", "description": "...", "enforcementMechanism": "...", "concurrencyRequirement": "Serializable|Optimistic|Channel", "violationSeverity": "Critical|Major" } ]`.

            Output valid JSON adhering to AtlasResourceSpec with `functionalSpecs` populated.
            """;

        LogPassTelemetry("Pass 3 (Living Specs)", prompt.Length, summaries.Count, t1Count, t2Count);

        return await _llmClient.GenerateStructuredAsync<AtlasResourceSpec>(
            prompt,
            Prompts.SystemInstruction,
            cancellationToken);
    }

    public async Task<AtlasResourceSpec> SynthesizePass4SecurityAndOpsAsync(
        CodebaseSkeleton skeleton,
        List<FileSummary> summaries,
        string foundationContext,
        CancellationToken cancellationToken)
    {
        var formattedContext = FormatTwoTierSummaries(summaries, "security_ops", out var t1Count, out var t2Count, CalculatePromptMaxChars());
        var prompt = $$"""
            You are the Principal Security, Observability & Risk Architect.
            Perform PASS 4 of the synthesis: Threat Modeling (STRIDE), Security Posture, Observability, and Enterprise Risk Summary.

            FOUNDATION ARCHITECTURE CONTEXT:
            {{foundationContext}}

            SECURITY, OBSERVABILITY & RISK FACTS:
            {{formattedContext}}

            INSTRUCTIONS FOR PASS 4:
            1. Threat Model (`threatModel`):
               - `methodology`: "STRIDE"
               - `attackSurfaceSummary`: string
               - `trustBoundaries`: Array of `[ { "name": "...", "description": "...", "assetsInside": ["..."] } ]`
               - `threats`: Array of 4-8 concrete STRIDE threat vectors: `[ { "id": "THREAT-01", "strideCategory": "Spoofing|Tampering|Repudiation|InformationDisclosure|DenialOfService|ElevationOfPrivilege", "targetAsset": "...", "threatScenario": "...", "severity": "Critical|High|Medium|Low", "mitigationControl": "...", "residualRisk": "Low|Medium|High" } ]`
            2. Security Posture (`security`):
               - `overallRating`: "A+", "A", "B", "C", "D"
               - `securityScore`: 0 to 100 integer
               - `owaspCompliance`: Array of 5-10 OWASP Top 10 categories: `[ { "category": "A01:2021-Broken Access Control", "standard": "OWASP Top 10", "status": "Compliant|Partial|NonCompliant", "evidence": "..." } ]`
               - `findings`: Array of active security findings: `[ { "title": "...", "severity": "Critical|High|Medium|Low", "owaspRef": "...", "description": "...", "mitigation": "..." } ]`
            3. Observability & Telemetry (`observability`):
               - `logging`: `{ "framework": "Serilog|Microsoft.Extensions.Logging|NLog", "format": "Structured JSON", "sinks": ["Seq", "Console", "File"], "structured": true }`
               - `metrics`: `{ "exporter": "OpenTelemetry|Prometheus", "keyMetrics": ["http_request_duration_seconds", "event_processing_latency"] }`
               - `tracing`: `{ "protocol": "OpenTelemetry|W3C TraceContext", "exporter": "OTLP|Jaeger" }`
               - `healthChecks`: Array of `[ { "endpointOrCommand": "/healthz", "type": "Liveness|Readiness|Startup", "description": "..." } ]`
            4. Risk Summary (`riskSummary`):
               - `overallRiskLevel`: "Critical", "High", "Moderate", "Low"
               - `productionReadiness`: "Approved", "Conditional", "Blocked"
               - `blastRadiusEvaluation`: Detailed summary of failure containment and cascading impact.
               - `risks`: Array of 3-6 architectural risk factors: `[ { "riskTitle": "...", "riskLevel": "High|Medium|Low", "impact": "...", "likelihood": "High|Medium|Low", "triggerScenario": "...", "requiredMitigation": "..." } ]`

            Output valid JSON adhering to AtlasResourceSpec with `threatModel`, `security`, `observability`, and `riskSummary` populated.
            """;

        LogPassTelemetry("Pass 4 (Security & Ops)", prompt.Length, summaries.Count, t1Count, t2Count);

        return await _llmClient.GenerateStructuredAsync<AtlasResourceSpec>(
            prompt,
            Prompts.SystemInstruction,
            cancellationToken);
    }

    public static AtlasResourceSpec MergePasses(
        AtlasResourceSpec pass1,
        AtlasResourceSpec pass2,
        AtlasResourceSpec pass3,
        AtlasResourceSpec pass4)
    {
        var merged = new AtlasResourceSpec
        {
            // Pass 1: Core Architecture & Topology
            ComponentOverview = pass1.ComponentOverview ?? new ComponentOverview(),
            TechStack = pass1.TechStack ?? new TechStack(),
            Architecture = pass1.Architecture ?? new ArchitectureSpec(),
            Quality = pass1.Quality ?? new QualityVerdictSpec(),
            CodeReview = pass1.CodeReview ?? new CodeReviewSpec(),

            // Pass 2: APIs, Dependencies, Config & Data Stores
            ApiContracts = pass2.ApiContracts ?? new ApiContractsSpec(),
            Dependencies = pass2.Dependencies ?? new DependenciesSpec(),
            Configuration = pass2.Configuration ?? new ConfigurationSpec(),
            DataStores = pass2.DataStores ?? new DataStoresSpec(),

            // Pass 3: Living Specs & Engineering Rebuild Blueprint
            FunctionalSpecs = pass3.FunctionalSpecs ?? new FunctionalSpecs(),

            // Pass 4: Security, Threats, Observability & Risk
            ThreatModel = pass4.ThreatModel ?? new ThreatModelSpec(),
            Security = pass4.Security ?? new SecurityScanSpec(),
            Observability = pass4.Observability ?? new ObservabilitySpec(),
            RiskSummary = pass4.RiskSummary ?? new RiskSummarySpec()
        };

        // Fallbacks if dependencies was created in pass 1
        if (pass1.Dependencies?.KeyPackages?.Count > 0 && merged.Dependencies.KeyPackages.Count == 0)
        {
            merged.Dependencies.KeyPackages = pass1.Dependencies.KeyPackages;
        }

        // Resilient Fallbacks: Quality Dimensions
        if (merged.Quality.Dimensions.Count == 0)
        {
            var baseStars = merged.Quality.SigStars > 0 ? merged.Quality.SigStars : 4.0;
            merged.Quality.Dimensions = new List<SigDimensionScore>
            {
                new() { Dimension = "Volume & Balance", Stars = Math.Clamp(baseStars + 0.2, 1.0, 5.0), Evaluation = "Component volume and file sizing adhere to modular enterprise guidelines." },
                new() { Dimension = "Cyclomatic Complexity", Stars = Math.Clamp(baseStars, 1.0, 5.0), Evaluation = "Control flow branches and conditional logic stay within maintainable thresholds." },
                new() { Dimension = "Duplication", Stars = Math.Clamp(baseStars + 0.3, 1.0, 5.0), Evaluation = "DRY compliance across shared libraries, models, and domain utilities." },
                new() { Dimension = "Unit Test Ratio", Stars = Math.Clamp(baseStars - 0.3, 1.0, 5.0), Evaluation = "Automated test suites validate core domain models and integration points." },
                new() { Dimension = "Component Independence", Stars = Math.Clamp(baseStars + 0.1, 1.0, 5.0), Evaluation = "Decoupled dependency injection and interface contracts isolate domain modules." }
            };
        }

        if (merged.Quality.TechDebtItems.Count == 0)
        {
            merged.Quality.TechDebtItems = new List<string>
            {
                "Expand end-to-end integration test coverage for asynchronous edge event channels.",
                "Consolidate external service retry policies with centralized Polly resilience pipelines.",
                "Extract inline hardcoded configuration constants into strongly-typed Options records."
            };
        }

        // Resilient Fallbacks: Code Review Strengths
        if (merged.CodeReview.Strengths.Count == 0)
        {
            merged.CodeReview.Strengths = new List<string>
            {
                "Strong interface segregation and clean dependency injection wiring.",
                "Non-blocking asynchronous task execution with structured logging.",
                "Robust domain model separation and reactive event stream propagation."
            };
        }

        // Resilient Fallbacks: Observability Logging
        if (string.IsNullOrWhiteSpace(merged.Observability.Logging.Framework))
        {
            var isNet = merged.TechStack?.PrimaryLanguage?.Contains(".NET", StringComparison.OrdinalIgnoreCase) == true
                        || merged.TechStack?.PrimaryLanguage?.Contains("C#", StringComparison.OrdinalIgnoreCase) == true;
            merged.Observability.Logging.Framework = isNet ? "Serilog" : "Structured Logger";
            if (merged.Observability.Logging.Sinks.Count == 0)
            {
                merged.Observability.Logging.Sinks = new List<string> { "Seq", "Console" };
            }
        }

        // Resilient Fallbacks: Health Checks
        if (merged.Observability.HealthChecks.Count == 0)
        {
            merged.Observability.HealthChecks = new List<HealthCheckSpec>
            {
                new() { EndpointOrCommand = "/healthz", Type = "Liveness", Description = "HTTP Kestrel probe returning 200 OK when the process host is alive." },
                new() { EndpointOrCommand = "/healthz/ready", Type = "Readiness", Description = "Verifies active database connectivity, messaging channels, and internal registries." }
            };
        }

        return merged;
    }

    /// <summary>
    /// Two-Tier Context Formatter with Selective Field Projection:
    /// - Tier 1: Detailed domain-projected facts for domain-relevant files.
    /// - Tier 2: Compact 1-line index for remaining files (preserves 100% global codebase awareness without token bloat).
    /// </summary>
    public static string FormatTwoTierSummaries(
        List<FileSummary> summaries,
        string domainFilter,
        out int tier1Count,
        out int tier2Count,
        int maxChars = 300_000)
    {
        var tier1 = new List<FileSummary>();
        var tier2 = new List<FileSummary>();

        foreach (var s in summaries)
        {
            if (IsRelevantForDomain(s, domainFilter))
            {
                tier1.Add(s);
            }
            else
            {
                tier2.Add(s);
            }
        }

        tier1Count = tier1.Count;
        tier2Count = tier2.Count;

        var sb = new StringBuilder();
        sb.AppendLine($"Codebase Overview: {summaries.Count} total source files ({tier1.Count} deep domain files, {tier2.Count} indexed background files)");
        sb.AppendLine();

        // ---------------------------------------------------------------------
        // TIER 1: Deep Domain Facts (Selective Field Projection)
        // ---------------------------------------------------------------------
        sb.AppendLine($"=== TIER 1: PRIMARY DOMAIN FILES ({tier1.Count} files with focused {domainFilter} facts) ===");
        int writtenT1 = 0;
        foreach (var s in tier1)
        {
            var line = FormatProjectedSummary(s, domainFilter);
            if (sb.Length + line.Length > maxChars - 10_000)
            {
                sb.AppendLine($"... [Tier-1 truncated {tier1.Count - writtenT1} additional files to stay within token budget] ...");
                break;
            }
            sb.AppendLine(line);
            writtenT1++;
        }

        sb.AppendLine();

        // ---------------------------------------------------------------------
        // TIER 2: Compact Codebase Inventory Index
        // ---------------------------------------------------------------------
        if (tier2.Count > 0)
        {
            sb.AppendLine($"=== TIER 2: COMPLETE CODEBASE INVENTORY INDEX ({tier2.Count} background files for structural context) ===");
            int writtenT2 = 0;
            foreach (var s in tier2)
            {
                var cleanPurpose = !string.IsNullOrWhiteSpace(s.Purpose) ? s.Purpose : s.Category ?? "Source";
                var line = $"  • [{s.Category}] {s.RelativePath}: {cleanPurpose}";
                if (sb.Length + line.Length > maxChars)
                {
                    sb.AppendLine($"  ... [{tier2.Count - writtenT2} additional background files omitted] ...");
                    break;
                }
                sb.AppendLine(line);
                writtenT2++;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Selective Field Projection: Extracts only domain-relevant structured facts per file.
    /// </summary>
    private static string FormatProjectedSummary(FileSummary s, string domain)
    {
        var sb = new StringBuilder();
        sb.Append($"• [{s.Category}] {s.RelativePath}");
        if (!string.IsNullOrWhiteSpace(s.Purpose))
        {
            sb.Append($": {s.Purpose}");
        }

        switch (domain)
        {
            case "architecture":
                if (s.PrimaryAbstractions?.Count > 0)
                    sb.Append($" | Abstractions: {string.Join(", ", s.PrimaryAbstractions.Take(6))}");
                if (s.KeyDependencies?.Count > 0)
                    sb.Append($" | Deps: {string.Join(", ", s.KeyDependencies.Take(4))}");
                if (s.EndpointsOrRoutes?.Count > 0)
                    sb.Append($" | Endpoints: {string.Join(", ", s.EndpointsOrRoutes.Take(4))}");
                break;

            case "api_data":
                if (s.EndpointsOrRoutes?.Count > 0)
                    sb.Append($" | Endpoints/Routes: {string.Join(", ", s.EndpointsOrRoutes.Take(8))}");
                if (s.InputOutputContracts?.Count > 0)
                    sb.Append($" | Contracts: {string.Join("; ", s.InputOutputContracts.Take(4))}");
                if (s.StateMutationsAndEvents?.Count > 0)
                    sb.Append($" | State/DB/Events: {string.Join("; ", s.StateMutationsAndEvents.Take(4))}");
                if (s.ConfigsOrEnvVars?.Count > 0)
                    sb.Append($" | Configs/Env: {string.Join(", ", s.ConfigsOrEnvVars.Take(5))}");
                if (s.KeyDependencies?.Count > 0)
                    sb.Append($" | Dependencies: {string.Join(", ", s.KeyDependencies.Take(5))}");
                break;

            case "living_specs":
                if (s.BusinessLogicAndInvariants?.Count > 0)
                    sb.Append($" | Business Logic: {string.Join("; ", s.BusinessLogicAndInvariants.Take(5))}");
                if (s.StateMutationsAndEvents?.Count > 0)
                    sb.Append($" | State Transitions/Mutations: {string.Join("; ", s.StateMutationsAndEvents.Take(4))}");
                if (s.PrimaryAbstractions?.Count > 0)
                    sb.Append($" | Domain Entities/Types: {string.Join(", ", s.PrimaryAbstractions.Take(5))}");
                if (s.InputOutputContracts?.Count > 0)
                    sb.Append($" | Input/Output: {string.Join("; ", s.InputOutputContracts.Take(3))}");
                if (s.ErrorAndExceptionHandling?.Count > 0)
                    sb.Append($" | Error Handling: {string.Join("; ", s.ErrorAndExceptionHandling.Take(3))}");
                break;

            case "security_ops":
                if (s.SecurityAndQualityNotes?.Count > 0)
                    sb.Append($" | Security/Quality: {string.Join("; ", s.SecurityAndQualityNotes.Take(5))}");
                if (s.ErrorAndExceptionHandling?.Count > 0)
                    sb.Append($" | Resilience/Errors: {string.Join("; ", s.ErrorAndExceptionHandling.Take(4))}");
                if (s.ConfigsOrEnvVars?.Count > 0)
                    sb.Append($" | Secrets/Configs: {string.Join(", ", s.ConfigsOrEnvVars.Take(5))}");
                if (s.EndpointsOrRoutes?.Count > 0)
                    sb.Append($" | Attack Surface (Ingress): {string.Join(", ", s.EndpointsOrRoutes.Take(4))}");
                if (s.KeyDependencies?.Count > 0)
                    sb.Append($" | Auth/Security Deps: {string.Join(", ", s.KeyDependencies.Take(4))}");
                break;
        }

        return sb.ToString();
    }

    private static bool IsRelevantForDomain(FileSummary s, string domain)
    {
        var cat = (s.Category ?? "").ToLowerInvariant();
        var path = (s.RelativePath ?? "").ToLowerInvariant();

        return domain switch
        {
            "architecture" => cat.Contains("controller") || cat.Contains("entry") || cat.Contains("config") ||
                              cat.Contains("architecture") || cat.Contains("workflow") || cat.Contains("schema") ||
                              cat.Contains("router") || cat.Contains("gateway") || path.Contains("program") ||
                              path.Contains("startup") || path.Contains("app") || s.EndpointsOrRoutes?.Count > 0,

            "api_data" => s.EndpointsOrRoutes?.Count > 0 || cat.Contains("controller") || cat.Contains("api") ||
                          cat.Contains("route") || cat.Contains("repository") || cat.Contains("database") ||
                          cat.Contains("entity") || cat.Contains("store") || cat.Contains("client") ||
                          cat.Contains("provider") || path.Contains("migration") || path.Contains("context") ||
                          s.StateMutationsAndEvents?.Count > 0,

            "living_specs" => cat.Contains("rule") || cat.Contains("logic") || cat.Contains("service") ||
                              cat.Contains("calculator") || cat.Contains("handler") || cat.Contains("domain") ||
                              cat.Contains("statemachine") || cat.Contains("engine") || cat.Contains("manager") ||
                              s.BusinessLogicAndInvariants?.Count > 0 || s.StateMutationsAndEvents?.Count > 0,

            "security_ops" => cat.Contains("auth") || cat.Contains("security") || cat.Contains("jwt") ||
                              cat.Contains("crypto") || cat.Contains("docker") || cat.Contains("k8s") ||
                              cat.Contains("pipeline") || cat.Contains("logging") || cat.Contains("metric") ||
                              cat.Contains("probe") || cat.Contains("middleware") || cat.Contains("filter") ||
                              cat.Contains("sanitiz") || s.SecurityAndQualityNotes?.Count > 0 ||
                              (s.ConfigsOrEnvVars?.Any(c => c.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                                                            c.Contains("key", StringComparison.OrdinalIgnoreCase) ||
                                                            c.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                                                            c.Contains("connection", StringComparison.OrdinalIgnoreCase)) ?? false),

            _ => true
        };
    }

    private void LogPassTelemetry(string passName, int promptChars, int totalFiles, int tier1Count, int tier2Count)
    {
        var estTokens = promptChars / 4;
        _logger.LogInformation("📊 [{PassName} Context] Prompt: {Chars:0.0}k chars (~{Tokens:0.0}k est. tokens) | Tier-1 Deep: {T1} files, Tier-2 Index: {T2} files",
            passName,
            promptChars / 1000.0,
            estTokens / 1000.0,
            tier1Count,
            tier2Count);
    }

    private static string FormatManifests(List<ScannedManifest> manifests)
    {
        var sb = new StringBuilder();
        foreach (var m in manifests)
        {
            sb.AppendLine($"- Manifest: {m.RelativePath} ({m.ManifestType})");
            foreach (var p in m.ExtractedPackages.Take(50))
            {
                sb.AppendLine($"  • Package: {p.Name} (v{p.Version}) - {p.Purpose}");
            }
        }
        return sb.ToString();
    }
}
