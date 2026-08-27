# AtlasResourceCRD ⚡

> **Agentic Codebase Architecture, OWASP Security & SIG Quality Scanner**  
> Synthesizes deep multi-diagram software catalogs into Kubernetes CustomResourceDefinitions (`atlas.io/v1alpha1`), Backstage entities, and interactive standalone HTML dashboards powered by **.NET 10** and **Google Gemini 3.7 Flash with High Thinking**.

---

## 🚀 Key Features

- 🗺️ **Map-Reduce Multi-Agent Architecture**:
  - **Map Phase**: Concurrently extracts lightweight semantic summaries across all source files in parallel (`SemaphoreSlim` concurrency control).
  - **Reduce Phase**: Global Architect Agent (Gemini 3.7 Flash + High Thinking with 24k token reasoning budget) merges file summaries, manifests, and git metadata into a unified architectural catalog.
- ⚡ **Pure Git Blob SHA Caching**:
  - Computes native Git SHA-1 hashes (`sha1("blob <len>\0<content>")`) stored at `.atlas/cache/files/{sha}.json`.
  - Unchanged files produce an **instant 100% cache hit** with **0 LLM token consumption** and **<100ms** Map execution.
- 🏛️ **Interactive Multi-Diagram Suite (C4 Model)**:
  - **`contextDiagram` (C4 Level 1 System Context)**: Maps End Users, Client Interfaces, Core System Boundaries, External Cloud APIs, and Local Network Hardware.
  - **`componentDiagram` (C4 Level 2/3 Component & Subsystem)**: Visualizes internal modules, controllers, rule engines, and plugin layers with **exact protocol annotations on links** (`HTTP/REST`, `MQTT/mTLS 8883`, `Influx Line Protocol`, `Matter UDP`, `SSE`).
  - **`dataFlowDiagram` (Data & Event Lifecycle)**: Traces the end-to-end telemetry ingestion, normalization, rule execution, AI analysis, and time-series persistence pipeline.
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
  - **Interactive Component Inspector Drawer**: Click any node to inspect responsibilities and active contracts.
  - **Filterable Code Review & API Tables & 1-Click CRD YAML Copy**.
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
| `--no-cache` | Disable Git Blob SHA caching | `false` |
| `--clear-cache` | Clear existing `.atlas/cache` before scan | `false` |
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
