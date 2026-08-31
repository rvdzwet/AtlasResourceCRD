# Atlas Enterprise Platform ⚡

> **Centralized Enterprise Architecture, Software Catalog, Infrastructure Topology & Graph Intelligence Hub**  
> Synthesizes deep multi-diagram software catalogs into Kubernetes CustomResourceDefinitions (`atlas.io/v1alpha1`), feeds Neo4j property graphs, tracks runtime hosting coordinates (Kubernetes / VMs / IIS), and provides a real-time **Blazor Server** interactive dashboard styled with the **Stater Enterprise Design System** (`#562178` Royal Purple & `#F8A719` Amber Gold).
>
> Supports both **Cloud LLMs (Google Gemini 3.7 Flash with High Thinking)** and **100% Local / Self-Hosted LLMs (Qwen 2.5 Coder, Gemma 2, DeepSeek-R1 via Ollama, LM Studio, or vLLM)**.

---

## 🏛️ Monorepo Architecture

```
Atlas.slnx
├── src/Atlas.Core/            # Domain models, Multi-Model LLM abstraction, Map-Reduce agent pipeline, CRD validation, serialization, remote client
├── src/Atlas.Scanner.Cli/     # 100% Offline Standalone CLI tool (`atlas scan .`, `atlas sbom .`) with remote caching
├── src/Atlas.Server/          # 100% Stateless Hub (.NET 10 Blazor Server + REST API + Neo4j Graph + Background CVE sync)
├── tests/Atlas.Core.Tests/    # Automated unit & integration test suite (46 passing tests)
├── docker-compose.yml         # 1-click Neo4j & Atlas stack
├── CONTRIBUTING.md            # Guidelines for open-source contributors
├── SECURITY.md                # Security policy & vulnerability reporting
└── LICENSE                    # MIT License
```

---

## 🚀 Key Capabilities

### 1. 🤖 Multi-Model LLM Abstraction (Cloud & Local)
- **Named Profiles**: Seamlessly switch between cloud and local providers with `--profile` (`-p`):
  - **`gemini`** (Default): Google Gemini 3.7 Flash with native structured outputs and High Thinking reasoning budget.
  - **`local-qwen`**: Local Qwen 2.5 Coder (e.g. `qwen2.5-coder:32b` or `7b` via Ollama at `http://localhost:11434/v1` with 32k context).
  - **`local-gemma`**: Local Google Gemma 2 (e.g. `gemma2:27b` or `9b` via LM Studio / Ollama at `http://localhost:1234/v1`).
- **Adaptive Token Budgeting**: Automatically scales synthesis context limits based on the active model's context window (8k for Gemma, 32k for Qwen, 128k for Gemini).
- **Self-Healing Resilient Parser**: Multi-phase JSON extraction with markdown stripping and regex fallback repairs to ensure 100% reliable schema parsing across local models.

### 2. 🔍 100% Offline Atlas Scanner CLI (`Atlas.Scanner.Cli`)
- **Fast, Air-Gapped Codebase Analysis**:
  - Local manifest parser extracts package dependencies (`.csproj`, `package.json`, `requirements.txt`, `pom.xml`, `Cargo.toml`, `go.mod`).
  - Generates standard **CycloneDX 1.5 SBOM** with standardized PURLs with **0 outbound network calls**.
- **Pure Remote Caching Protocol**:
  - Zero local disk clutter; cache checks and synthesis stores happen strictly via Atlas Server Neo4j cache endpoints in real-time.
  - Unchanged files and commits evaluate in **<50ms** with **0 LLM tokens**.

### 3. 🌐 Runtime Hosting & Deployment Topology Ingestion
- **Standard REST API (`POST /api/v1/deployment/report`)**:
  - Direct HTTP webhook for **ArgoCD Sync Webhooks / GitOps**, **GitHub Actions / GitLab CI / Azure DevOps release pipelines**, and **legacy VM provisioning scripts (Ansible, PowerShell, `curl`)**.
- **Infrastructure Modeling in Neo4j**:
  - Maps `(:Environment)`, `(:Cluster)`, `(:Namespace)`, and `(:Host)` nodes linked via `[:DEPLOYED_TO]` and `[:HOSTED_ON]`.
  - Automatically identifies **Co-Located Microservices** sharing the same VM host or Kubernetes cluster.
  - Correlates runtime hosting exposure (`Public Ingress` vs `Internal Only`) with code-level STRIDE threats and blast radius calculations.

### 4. 🏛️ 100% Stateless Atlas Server (`Atlas.Server`)
- **Pure Neo4j Backbone**:
  - Completely eliminates in-memory caches and disk files. All services, historical snapshots, C4 relationships, and file summary hashes live directly in Neo4j.
  - Handles scale requirements for **>20,000 microservices** with fast Cypher lookups and pagination.
- **Centralized Continuous Vulnerability & License Monitoring**:
  - `VulnerabilityBackgroundSyncService` performs asynchronous `deps.dev` verified SPDX license extraction and `OSV.dev` real-time CVE audits upon catalog ingestion and on scheduled 24h cycles.
- **Interactive Blazor Server Dashboard**:
  - 🏛️ **Global C4 Architecture Studio**: Interactive Level 1 Context, Level 2 Component, and Level 3 Data Flow Mermaid graphs.
  - 🚀 **Hosting & Topology View**: Live deployment coordinates, orchestrators (ArgoCD/Ansible), ingress exposure, and co-located workloads.
  - 🚨 **Cross-Service Blast Radius & Risk Simulator**: Simulate primary outages and calculate shared infrastructure blast radius.
  - 📦 **Enterprise Service Catalog**: Live searchable fleet catalog with SIG stars, risk badges, and direct YAML export.

---

## 📦 Installation & Quickstart

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Docker & Docker Compose (for Neo4j)
- *(Optional)* [Ollama](https://ollama.com) or [LM Studio](https://lmstudio.ai) for local LLMs
- *(Optional)* Google Gemini API Key for cloud LLM

### 1. Start Neo4j Database
```bash
docker compose up -d
```
*Neo4j Browser will be available at `http://localhost:7474` (Bolt: `bolt://localhost:7687`).*

### 2. Build & Run Tests
```powershell
dotnet build Atlas.slnx
dotnet test Atlas.slnx
```

### 3. Launch Atlas Server
```powershell
dotnet run --project src/Atlas.Server
```
*Dashboard will be available at `http://localhost:5000`.*

---

## 🛠️ CLI Scanning & Multi-Model Examples

### Cloud Gemini Scan (Default)
```powershell
atlas scan . --server http://localhost:5000 -k <GEMINI_API_KEY>
```

### Local Qwen 2.5 Coder Scan (via Ollama)
```powershell
# 1. Pull Qwen model in Ollama
ollama run qwen2.5-coder:32b

# 2. Run Atlas scan using local-qwen profile
atlas scan . --profile local-qwen --server http://localhost:5000
```

### Local Gemma 2 Scan (via LM Studio or Ollama)
```powershell
atlas scan . --profile local-gemma --server http://localhost:5000
```

### Generate Offline CycloneDX 1.5 SBOM
```powershell
atlas sbom . -o cyclonedx-bom.json
```

---

## 📡 Reporting Runtime Deployments

### Kubernetes / ArgoCD PostSync Hook
```bash
curl -X POST http://localhost:5000/api/v1/deployment/report \
  -H "Content-Type: application/json" \
  -d '{
    "serviceName": "payments-core",
    "environment": "production",
    "platform": "Kubernetes",
    "cluster": "k8s-prod-weu",
    "namespace": "payments-prod",
    "tool": "ArgoCD",
    "imageOrArtifact": "ghcr.io/org/payments:v2.4.1",
    "gitCommit": "a1b2c3d",
    "replicas": 3,
    "ingress": {
      "publicUrl": "https://api.domain.com/payments",
      "exposure": "Public"
    }
  }'
```

### Legacy VM / IIS Deployment (PowerShell / Ansible)
```powershell
$body = @{
    serviceName     = "legacy-auth"
    environment     = "production"
    platform        = "VirtualMachine"
    host            = "srv-iis-01.corp.local"
    ipAddress       = "192.168.1.4"
    os              = "Windows Server 2022"
    tool            = "Ansible"
    imageOrArtifact = "C:\inetpub\wwwroot\LegacyAuth"
    gitCommit       = "f4e3d2c"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/v1/deployment/report" -Method Post -Body $body -ContentType "application/json"
```

---

## 🤝 Contributing & Security

- **Contributing**: Please review [CONTRIBUTING.md](CONTRIBUTING.md) for branch workflows, PR guidelines, and coding standards.
- **Security Disclosures**: Please see [SECURITY.md](SECURITY.md) to report vulnerabilities privately.
- **License**: [MIT License](LICENSE) © Atlas Authors.
