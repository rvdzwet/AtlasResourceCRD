# Contributing to Atlas

Thank you for your interest in contributing to **Atlas**! We welcome contributions from developers, architects, and DevOps engineers worldwide.

---

## Code of Conduct

Please treat all community members with respect, professionalism, and empathy.

---

## Development Setup

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker & Docker Compose](https://www.docker.com/) (for local Neo4j graph database)
- Optional: [Ollama](https://ollama.com/) or [LM Studio](https://lmstudio.ai/) for running local LLMs (Qwen 2.5 / Gemma 2)

### Quick Start
1. Clone the repository:
   ```bash
   git clone https://github.com/rvdzwet/AtlasResourceCRD.git
   cd AtlasResourceCRD
   ```

2. Start the local Neo4j graph database:
   ```bash
   docker compose up -d neo4j
   ```

3. Run the automated test suite:
   ```bash
   dotnet test Atlas.slnx
   ```

4. Launch Atlas Server:
   ```bash
   dotnet run --project src/Atlas.Server
   ```
   Open [http://localhost:5000](http://localhost:5000) in your browser.

---

## Submitting Pull Requests

1. Fork the repository and create a new feature branch (`feature/your-feature-name`).
2. Ensure all unit and integration tests pass:
   ```bash
   dotnet test Atlas.slnx
   ```
3. Commit your changes with clear, descriptive commit messages.
4. Push to your fork and submit a Pull Request against the `main` branch.
5. Provide a detailed summary of your changes and any testing steps.

---

## Architecture Principles

1. **Stateless Server Backbone**: Atlas Server maintains zero state in-memory or on local disk; all entities, topologies, and relationships are stored and queried in Neo4j.
2. **Multi-Model LLM Agnosticism**: All agentic pipelines must code to `ILlmClient` and support both Google Gemini and OpenAI-compatible local/remote inference servers.
3. **Enterprise Scalability**: Code must be designed to handle >20,000 microservices with sub-second Cypher queries and minimal memory footprint.
