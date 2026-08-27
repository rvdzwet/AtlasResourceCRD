# AtlasResourceCRD ⚡

> **Agentic Codebase Architecture, OWASP Security & SIG Quality Scanner**  
> Synthesizes deep multi-diagram software catalogs into Kubernetes CustomResourceDefinitions (`atlas.io/v1alpha1`), Backstage entities, and interactive standalone HTML dashboards powered by **.NET 10** and **Google Gemini 3.7 Flash with High Thinking**.

---

## 🚀 Key Features

- 🗺️ **Map-Reduce Multi-Agent Architecture**:
  - **Map Phase**: Concurrently extracts lightweight semantic summaries across all source files in parallel (`SemaphoreSlim` concurrency control).
  - **Reduce Phase**: Global Architect Agent (Gemini 3.7 Flash + High Thinking with 24k token reasoning budget) merges file summaries, manifests, and git metadata into a unified architectural catalog.
- ⚡ **Multi-Tier Git Caching & Instant Reruns**:
  - **Map Caching**: Git Blob SHA-1 hashes stored at `.atlas/cache/files/{sha}.json`. Unchanged files execute in **<100ms** with **0 LLM tokens**.
  - **Synthesis & Artifact Caching**: Stores full `AtlasResource` and rendered HTML in `.atlas/cache/synth/latest.json`. Re-running scans without code changes produces an **instant 100% Cache HIT** in **<50ms**.
- 🔄 **Idempotent Incremental Diff Patching**:
  - On new commits, Atlas calculates file diffs (added/modified/deleted), summarizes only changed files, and feeds the previous baseline `AtlasResource` + diffs into an incremental delta prompt.
  - **Topological Stability**: Preserves diagram node IDs, layout structure, and unaffected security/quality findings without flapping or drastic re-writes.
- 🛡️ **Mermaid AST Linter & Iterative Auto-Repair Loop**:
  - Validates diagram syntax (balanced subgraphs, bracket matching, arrow escaping, pipe syntax).
  - Automatically sanitizes common syntax hazards (e.g. `->` inside quotes).
  - Iterative LLM repair loop auto-corrects broken diagrams with deterministic fallback generators.
- 📖 **Deep Functional Specifications & Living Logic Blueprints**:
  - High-level **Business Capabilities** linked to measurable business outcomes.
  - Executable-grade **Business Use-Cases** detailed enough to rebuild/reimplement domain logic:
    - **Input Data Contracts**: Parameter schemas, units, data types, and valid ranges.
    - **Execution Logic**: Detailed step-by-step algorithms and main workflows.
    - **Alternative & Exception Flows**: Error handling, timeouts, fallbacks, and circuit breaker policies.
    - **Business Invariants**: Formulas, domain guardrails, rate limits, and timing thresholds.
    - **Output State Mutations**: Persisted database records, in-memory cache updates, and emitted CloudEvents.
    - **Acceptance Criteria**: Formatted BDD Given-When-Then scenarios covering happy paths and edge cases.
    - **💡 Architectural Modernization Advice**: Concrete, actionable recommendations on every use-case to modernize, decouple, or scale the implementation (e.g. MediatR outboxes, saga orchestrators, reactive event streams).
- 🏛️ **Interactive Architecture Suite (Official C4 Model Standard)**:
  - **Official C4 Model Palette**: Person (`#08427B`), Software System (`#1168BD`), Container (`#2366A0`), Component (`#438DD5`), External System (`#686868`), and Database (`#08427B`).
  - **Interactive C4 Legend**: Visual color-coded palette bar embedded in the diagram header.
  - **360-Degree Architecture Repository Drawer**: Click **any diagram node** or component card to slide open an in-depth inspector with mapped business use cases, active API endpoints, source files, and review findings.
  - **1-Click High-Res Export**: Instant download of rendered diagrams to vector **SVG** and **PNG** for design reviews and Confluence.
  - **Node Spotlight on Hover**: Dims unrelated nodes to clearly trace connected communication links and protocols.
  - **`contextDiagram` (C4 Level 1)**, **`componentDiagram` (C4 Level 2/3)**, and **`dataFlowDiagram` (Lifecycle)**.
- ⚡ **High-Throughput Parallelism & Strict Sequential Output Pipeline**:
  - Parallel Map Phase scaling up to **16 concurrent workers** for fast file analysis.
  - **Strict 2-Step Sequential Pipeline**: Guarantees `atlas.yaml` is written and flushed to disk first, then deserialized sequentially to generate `atlas.html` with 100% deterministic parity.
  - Multi-Tier Git Blob SHA caching + Synthesis Artifact Caching for **<50ms instant cache hits**.
- 🚨 **Exhaustive Executive Risk Assessment & Blast Radius**:
  - Uncompromising, critical principal auditor persona designed specifically for **high-security, air-gapped, and regulated environments**.
  - **Production Readiness Verdict**: 🟢 `Approved`, 🟡 `Conditional`, or 🔴 `Blocked`.
  - **Blast Radius & Cascade Containment**: Evaluation of catastrophic failure scenarios, dependency downtime, and crash isolation.
  - **Restricted Environment & Air-Gap Compliance**: Validates offline operation, credential zero-trust, and boundary isolation.
  - **Exhaustive Architectural Risk Register**: Complete catalog of all discovered risks mapping risk levels, impacts, trigger scenarios, and required mitigations.
- 🛡️ **STRIDE Threat Model & Attack Surface Mapping**:
  - Delineates **Trust Boundaries** (e.g. Public Internet/Cloud vs Local LAN vs In-Process Memory vs Encrypted Storage).
  - Evaluates Attack Surface across exposed ports (HTTP, WebSockets, MQTT 1883/8883, Matter UDP 5540, config files).
  - Enumerate concrete Threat Vectors across **STRIDE** categories: **S**poofing, **T**ampering, **R**epudiation, **I**nformation Disclosure, **D**enial of Service, and **E**levation of Privilege.
  - Interactive, searchable threat vectors table with severity, mitigation controls, and residual risk ratings.
- 🛡️ **OWASP Top 10 Security Audit**:
  - Evaluates Broken Access Control, Cryptographic Failures, Injection, Insecure Design, Misconfiguration, Dependency Posture, Auth Failures, and SSRF.
  - Generates an **Overall Security Grade** (`A+`, `A`, `B`...), compliance checklist, and prioritized findings with actionable mitigations.
- ⭐ **SIG / ISO 25010 Quality Verdict**:
  - 5-Star Maintainability scorecard evaluating **Volume**, **Component Independence**, **Unit Complexity**, **Testability**, and **Architecture Consistency**.
  - Identifies concrete Technical Debt and refactoring action items.
- 🔍 **Automated Architectural & Code Review**:
  - Overall **Review Grade** (`A+`, `A`, `B`...) and score (`0-100`).
  - Highlights **Architectural Strengths & Modern Idioms** executed cleanly.
  - Detects **Anti-Patterns & Code Smells** (e.g. God classes, sync-over-async, tight coupling, improper disposal).
  - Prioritized **Review Findings Table** with file/symbol links, severity tags (`Critical`, `Major`, `Minor`, `Info`), observations, and concrete refactoring advice.
- 🖥️ **Interactive Fullscreen HTML Visualizer**:
  - Standalone, zero-dependency `atlas.html` dashboard with live client-side Mermaid rendering.
  - **Fullscreen Modal Viewport** with smooth mouse-wheel zooming and drag-to-pan.
  - **Filterable Code Review, Threat Model, API, and Living Documentation Tables**.
  - **Auto-Browser Launch**: Automatically opens the generated dashboard in your default browser.
- 📜 **Kubernetes CRD & Backstage Standardized**:
  - Outputs compliant `atlas.io/v1alpha1` Kubernetes manifests validated against RFC-1123 DNS naming rules.
  - Self-healing auto-repair loop guarantees 100% deterministic schema conformance.

---

## 📦 Installation & Setup

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Google Gemini API Key (`GEMINI_API_KEY`)

```powershell
# Clone the repository
git clone https://github.com/rvdzwet/AtlasResourceCRD.git
cd AtlasResourceCRD

# Build the solution
dotnet build

# Run test suite (14 automated unit tests)
dotnet test
```

---

## 🛠️ CLI Usage

```powershell
# Scan a repository (generates atlas.yaml + atlas.html and opens browser)
atlas-crd scan "C:\path\to\your\repo" -k <API_KEY> --thinking high -v

# Scan with custom concurrency and unlimited files
atlas-crd scan . -k <API_KEY> --concurrency 16 --all-files

# Scan without opening the browser (ideal for CI/CD pipelines)
atlas-crd scan . -k <API_KEY> --no-open -o catalog/atlas.yaml

# Validate an existing CRD manifest
atlas-crd validate atlas.yaml

# Render / regenerate interactive HTML dashboard from existing CRD manifest
atlas-crd html atlas.yaml

# Generate the Kubernetes CustomResourceDefinition schema
atlas-crd schema k8s/atlas-crd-definition.yaml

# Initialize a starter .atlas.yaml repo config
atlas-crd init
```

### CLI Command Options

| Option | Description | Default |
| :--- | :--- | :--- |
| `-o, --output <file>` | Output file destination for CRD manifest | `atlas.yaml` |
| `-k, --api-key <key>` | Google Gemini API key (or `GEMINI_API_KEY` env var) | Required |
| `-m, --model <name>` | Gemini model name | `gemini-3.7-flash` |
| `--thinking <level>` | Thinking mode: `high` (24k), `max` (65k), `dynamic` (-1), `medium`, `low`, `off` | `high` |
| `--concurrency <n>` | Concurrent workers for parallel Map phase | `8` |
| `--max-files <n>` | Max source files to analyze | `unlimited` |
| `--all-files` | Scan all discovered source files | `true` |
| `--no-cache` | Disable Git Blob SHA and Synthesis caching | `false` |
| `--clear-cache` | Clear existing `.atlas/cache` before scan | `false` |
| `--force-synth` | Force fresh global synthesis without incremental diff patching | `false` |
| `--no-open` | Do not automatically open `atlas.html` in browser | `false` |
| `-v, --verbose` | Enable debug logging | `false` |
| `-vv, --trace` | Enable extreme trace logging (prompts, payloads, tokens) | `false` |

---

## ⚙️ Repository Configuration (`.atlas.yaml`)

Create an optional `.atlas.yaml` in your repository root to configure metadata overrides and custom ignore rules:

```yaml
name: romars-iot-engine
namespace: production
tier: Backend # Backend | Frontend | CLI | Library | Worker | Gateway
owner: rvdzwet

ignoreGlobs:
  - "legacy/**"
  - "docs/archive/**"
  - "tmp/**"

labels:
  team: iot-platform
  environment: production

annotations:
  atlas.io/criticality: high
```

---

## 🧩 CRD Manifest Schema (`atlas.io/v1alpha1`)

```yaml
apiVersion: atlas.io/v1alpha1
kind: AtlasResource
metadata:
  name: romars-iot-engine
  namespace: default
  labels:
    app.kubernetes.io/name: romars-iot-engine
    app.kubernetes.io/part-of: backend
    app.kubernetes.io/managed-by: atlas
    atlas.io/language: c#
  annotations:
    atlas.io/scanned-at: "2026-08-27T07:53:46Z"
    atlas.io/git-commit-short: "96d091c"
    atlas.io/git-branch: "master"
spec:
  componentOverview:
    name: RoMars.IoT.Engine
    description: Smart home IoT automation engine and telemetry aggregation platform.
    tier: Backend
    purpose: Centralized smart home automation, energy management, and sensor telemetry.
    lifecycle: Active
  techStack:
    primaryLanguage: C#
    frameworks:
      - name: ASP.NET Core
        version: "10.0"
      - name: Angular
        version: "19.0"
  architecture:
    pattern: Event-Driven Modular Monolith
    contextDiagram: |
      flowchart TD
        User --> Engine
    componentDiagram: |
      flowchart TD
        SPA -- "HTTP/REST" --> API
        API -- "Influx Line Protocol" --> InfluxDB
    dataFlowDiagram: |
      flowchart LR
        Sensors --> InfluxDB
  security:
    overallRating: A-
    securityScore: 88
    owaspCompliance:
      - category: A01:2021-Broken Access Control
        status: Partial
        evidence: Local Kestrel API endpoints require LAN network isolation.
      - category: A02:2021-Cryptographic Failures
        status: Compliant
        evidence: Secrets encrypted with SQLCipher (AES-256).
  quality:
    sigStars: 4.6
    maintainabilityLevel: High
    dimensions:
      - dimension: ComponentIndependence
        stars: 5
        evaluation: Outstanding modular decoupling with shared interfaces.
```

---

## 🧪 Testing

```powershell
dotnet test
Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14, Duration: 564 ms - AtlasResourceCRD.Tests.dll (net10.0)
```

---

## 📄 License

MIT License © 2026 Romano van der Zwet
