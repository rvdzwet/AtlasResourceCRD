namespace AtlasResourceCRD.Core.Agents;

public static class Prompts
{
    public const string SystemInstruction = """
You are Atlas, a Principal Software Architect and DevOps AI Agent specializing in automated software cataloging and C4 architectural modeling.
Your task is to analyze codebase structures, manifests, architecture documents, and source files to produce standardized, deeply detailed software catalog specifications formatted strictly as JSON matching the requested schema.

Guidelines:
1. Be accurate, comprehensive, and objective. Base conclusions directly on codebase evidence (manifests, source files, directory tree, README).
2. Generate 3 DISTINCT, HIGH-QUALITY Mermaid diagrams:
   - `contextDiagram`: C4 Level 1 System Context Diagram (`flowchart TD`) showing End Users, Client Interfaces (Web/Mobile), the Primary System Boundary, External Third-Party APIs, and External Hardware/Protocols.
   - `componentDiagram`: C4 Level 2/3 Component & Subsystem Diagram (`flowchart TD`) showing internal modules, DI services, controllers, rule engines, storage layers, and communication gateways with EXACT communication protocols annotated on pipe links (e.g. `-->|"HTTP / REST"|`, `-->|"MQTT / mTLS (8883)"|`, `-->|"Influx Line Protocol"|`, `-->|"SSE / WebSockets"|`).
   - `dataFlowDiagram`: End-to-end Telemetry, Event, and Ingestion Lifecycle (`flowchart LR` or `flowchart TD`) tracing: Ingestion Trigger -> Parsing & Normalization -> Rule Engine & State Updates -> AI / Notification Dispatch -> Persistent TimeSeries Storage.
3. Always use valid Mermaid syntax:
   - Start diagrams with `flowchart TD` or `flowchart LR`.
   - Use clean alphanumeric node IDs without spaces or special characters (e.g. `A_1`, `P_Item`, `GoogleCloud`, `MqttBroker`).
   - Always quote node labels: `NodeId["Component Name (Role)"]`.
   - Never use raw arrow symbols (`->` or `-->`) inside node labels or quotes (use `to` or unicode `→` instead, e.g. `["BaseReading to Domain Item Mapping"]`).
   - For annotated links, use standard pipe syntax: `NodeA -->|"Protocol / Action"| NodeB`.
   - Use `subgraph` blocks with quoted titles: `subgraph Users ["Users & Client Interfaces"]`.
4. Perform an OWASP Security Evaluation:
   - Audit authentication, authorization, secret management (e.g. SQLCipher, environment variables vs hardcoded tokens), input handling, and dependency risks against OWASP Top 10 standards.
   - Assign an overall security rating (A+, A, B, C, D, F) and list prioritized actionable findings.
5. Perform a SIG (Software Improvement Group) & ISO 25010 Quality Verdict:
   - Evaluate the 5 core maintainability dimensions on a 1 to 5 star scale: Volume, Component Independence, Unit Complexity, Testability, and Architecture Consistency.
   - Calculate overall SIG Stars (e.g. 4.5) and list prioritized technical debt items.
6. Perform an Automated Architectural & Code Review:
   - Identify key strengths and modern architectural patterns executed well.
   - Detect anti-patterns and code smells (e.g. God classes, sync-over-async, tight coupling, hardcoded values).
   - Generate prioritized code review findings with concrete file/symbol references and actionable recommendations.
   - Assign an overall Code Review Grade (A+, A, B, C, D, F) and score (0-100).
7. Classify APIs, events, dependencies, environment variables, databases, and observability mechanisms meticulously.
""";

    public const string ExtractionPromptTemplate = """
Analyze the following codebase blueprint and file summaries to generate the complete Multi-Diagram Software Catalog Specification.

=== REPOSITORY SUMMARY ===
Name: {REPO_NAME}
Total Files: {TOTAL_FILES}
File Extensions: {EXTENSIONS_SUMMARY}

=== GIT METADATA ===
Branch: {GIT_BRANCH}
Commit: {GIT_COMMIT}
Remote URL: {GIT_REMOTE}

=== DISCOVERED MANIFESTS & RUNTIMES ===
{MANIFESTS_SUMMARY}

=== README DOCUMENTATION ===
{README_SNIPPET}

=== SOURCE FILE SUMMARIES (MAP PHASE) ===
{SOURCE_FILES_SNIPPET}

=== INSTRUCTIONS ===
Return a single JSON object matching this exact schema:

{
  "componentOverview": {
    "name": "string (service or app name)",
    "description": "string (comprehensive summary)",
    "tier": "Backend | Frontend | CLI | Library | Worker | Gateway | DataPipeline",
    "purpose": "string (business or technical purpose)",
    "lifecycle": "Active | Experimental | Staging | Deprecated",
    "owner": "string or null"
  },
  "techStack": {
    "primaryLanguage": "string",
    "languages": [
      { "name": "string", "version": "string or null", "details": "string or null" }
    ],
    "frameworks": [
      { "name": "string", "version": "string or null", "details": "string or null" }
    ],
    "runtimes": [
      { "name": "string", "version": "string or null" }
    ],
    "buildSystems": [
      { "name": "string", "version": "string or null" }
    ],
    "packageManagers": [
      { "name": "string", "version": "string or null" }
    ]
  },
  "architecture": {
    "summary": "string (detailed architectural approach and design patterns)",
    "pattern": "string (e.g. Modular Monolith, Clean Architecture, Microservices, Event-Driven)",
    "components": [
      {
        "name": "string",
        "type": "string (Core Host, API Controller, Plugin Provider, Rule Engine, Storage Layer, CLI Tool)",
        "description": "string",
        "responsibilities": ["string"]
      }
    ],
    "contextDiagram": "flowchart TD\n  ...valid C4 Level 1 system context diagram...",
    "componentDiagram": "flowchart TD\n  ...valid C4 Level 2/3 component diagram with protocol annotations on links...",
    "dataFlowDiagram": "flowchart LR\n  ...valid data and event ingestion flow diagram...",
    "mermaidDiagram": "flowchart TD\n  ...primary component diagram..."
  },
  "apiContracts": {
    "endpoints": [
      {
        "path": "string",
        "method": "GET | POST | PUT | DELETE | PATCH",
        "description": "string",
        "authRequired": false,
        "requestType": "string or null",
        "responseType": "string or null"
      }
    ],
    "events": [
      {
        "topicOrQueue": "string",
        "action": "Publish | Subscribe",
        "description": "string",
        "payloadType": "string or null"
      }
    ],
    "grpcServices": [
      {
        "serviceName": "string",
        "methods": ["string"],
        "description": "string"
      }
    ]
  },
  "dependencies": {
    "internalServices": [
      {
        "name": "string",
        "protocolOrHost": "string or null",
        "purpose": "string",
        "criticality": "Low | Medium | High | Critical"
      }
    ],
    "externalApis": [
      {
        "name": "string",
        "protocolOrHost": "string or null",
        "purpose": "string",
        "criticality": "Low | Medium | High | Critical"
      }
    ],
    "keyPackages": [
      {
        "name": "string",
        "version": "string or null",
        "purpose": "string or null"
      }
    ]
  },
  "configuration": {
    "environmentVariables": [
      {
        "name": "string",
        "description": "string",
        "required": false,
        "defaultValue": "string or null",
        "isSecret": false
      }
    ],
    "configFiles": [
      {
        "path": "string",
        "format": "YAML | JSON | TOML | ENV | XML",
        "description": "string"
      }
    ]
  },
  "dataStores": {
    "databases": [
      { "name": "string", "type": "string", "role": "Primary", "description": "string" }
    ],
    "caches": [
      { "name": "string", "type": "string", "role": "Cache", "description": "string" }
    ],
    "messageBrokers": [
      { "name": "string", "type": "string", "role": "Broker", "description": "string" }
    ],
    "objectStorage": [
      { "name": "string", "type": "string", "role": "Storage", "description": "string" }
    ]
  },
  "observability": {
    "healthChecks": [
      { "endpointOrCommand": "string", "type": "Liveness | Readiness | Startup", "description": "string" }
    ],
    "logging": {
      "framework": "string",
      "format": "Structured JSON | Plain Text",
      "sinks": ["string"]
    },
    "metrics": {
      "exporter": "string",
      "keyMetrics": ["string"]
    },
    "tracing": {
      "protocol": "string",
      "exporter": "string"
    }
  },
  "security": {
    "overallRating": "A+ | A | B | C | D | F",
    "securityScore": 90,
    "owaspCompliance": [
      {
        "category": "string (e.g. A01:2021-Broken Access Control, A02:2021-Cryptographic Failures, A03:2021-Injection, A04:2021-Insecure Design, A05:2021-Security Misconfiguration, A06:2021-Vulnerable Components, A07:2021-Auth Failures, A08:2021-Integrity Failures, A09:2021-Logging Failures, A10:2021-SSRF)",
        "standard": "OWASP Top 10",
        "status": "Compliant | Partial | NonCompliant | NotApplicable",
        "evidence": "string"
      }
    ],
    "findings": [
      {
        "title": "string",
        "severity": "Low | Medium | High | Critical",
        "owaspRef": "string",
        "description": "string",
        "mitigation": "string",
        "affectedFiles": ["string"]
      }
    ],
    "recommendations": ["string"]
  },
  "quality": {
    "sigStars": 4.5,
    "maintainabilityLevel": "Very High | High | Moderate | Low | Very Low",
    "dimensions": [
      { "dimension": "Volume", "stars": 4, "evaluation": "string" },
      { "dimension": "ComponentIndependence", "stars": 5, "evaluation": "string" },
      { "dimension": "UnitComplexity", "stars": 4, "evaluation": "string" },
      { "dimension": "Testability", "stars": 4, "evaluation": "string" },
      { "dimension": "ArchitectureConsistency", "stars": 5, "evaluation": "string" }
    ],
    "summary": "string (executive maintainability verdict)",
    "techDebtItems": ["string"]
  },
  "codeReview": {
    "reviewGrade": "A+ | A | B | C | D | F",
    "reviewScore": 90,
    "summary": "string (executive code review summary)",
    "strengths": ["string"],
    "codeSmells": [
      {
        "smellType": "string (e.g. God Class, Dead Code, Long Parameter List, Sync-over-Async)",
        "description": "string",
        "affectedComponentOrFile": "string"
      }
    ],
    "findings": [
      {
        "title": "string",
        "category": "Architecture | Performance | Maintainability | IdiomaticPractices | Robustness",
        "severity": "Critical | Major | Minor | Info",
        "file": "string",
        "symbol": "string",
        "description": "string",
        "recommendation": "string"
      }
    ]
  }
}
""";

    public const string SchemaRepairPromptTemplate = """
The previously generated software catalog specification failed strict schema validation with the following errors:

=== VALIDATION ERRORS ===
{VALIDATION_ERRORS}

=== PREVIOUS OUTPUT ===
{PREVIOUS_OUTPUT}

=== INSTRUCTIONS ===
Fix the errors identified above and output the complete, corrected JSON specification adhering strictly to the schema rules:
1. Ensure all 3 Mermaid diagrams (contextDiagram, componentDiagram, dataFlowDiagram) start with a valid header ('flowchart TD' or 'flowchart LR' or 'graph TD').
2. Ensure metadata, security, quality, and codeReview sections are populated with valid ratings.
3. Preserve all extracted component facts, endpoints, dependencies, and architecture insights.
4. Output strictly valid JSON.
""";

    public const string DiagramRepairPromptTemplate = """
You are a Mermaid diagram syntax and layout repair expert.
The following Mermaid architecture diagram failed strict syntax validation:

=== DIAGRAM TYPE ===
{DIAGRAM_NAME}

=== SYNTAX ERRORS DETECTED ===
{SYNTAX_ERRORS}

=== BROKEN MERMAID CODE ===
{BROKEN_DIAGRAM}

=== REPAIR INSTRUCTIONS ===
1. Correct all syntax errors (unbalanced brackets, unclosed subgraphs, unquoted strings with special characters, raw '->' inside quotes).
2. Ensure the diagram starts with a valid header (e.g. `flowchart TD` or `flowchart LR`).
3. Ensure all links use standard pipe format (e.g. `A -->|"Protocol"| B`).
4. Output ONLY the raw fixed Mermaid diagram code without markdown fences, explanation, or commentary.
""";

    public const string IncrementalPatchPromptTemplate = """
You are Atlas, performing an incremental, idempotent architectural update on an existing software catalog specification.

=== BASELINE ATLAS RESOURCE SPECIFICATION ===
{BASELINE_SPEC_JSON}

=== CODEBASE CHANGES IN CURRENT COMMIT ({GIT_COMMIT}) ===

--- ADDED FILES ({ADDED_COUNT}) ---
{ADDED_FILES_SUMMARY}

--- MODIFIED FILES ({MODIFIED_COUNT}) ---
{MODIFIED_FILES_SUMMARY}

--- DELETED FILES ({DELETED_COUNT}) ---
{DELETED_FILES_SUMMARY}

=== INCREMENTAL UPDATE GUIDELINES ===
1. **Idempotency & Topology Stability**:
   - PRESERVE existing Mermaid diagrams (`contextDiagram`, `componentDiagram`, `dataFlowDiagram`) topology, existing node IDs, and relationships unless the file changes explicitly add/remove components, protocols, or data flows.
   - Do NOT rewrite or reorder unaffected diagrams or components.
2. **Patch Modified Areas**:
   - Update API endpoints, models, data stores, dependencies, or configuration if the changed files modified them.
   - Update security posture and findings if the changes fixed or introduced security risks.
   - Update code review items and smells if the changes refactored or introduced new patterns.
3. **Format**:
   - Output the complete, updated `AtlasResourceSpec` formatted strictly as valid JSON matching the schema.
""";
}
