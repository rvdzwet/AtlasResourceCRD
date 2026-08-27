using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using AtlasResourceCRD.Core.Models;
using AtlasResourceCRD.Core.Serialization;

namespace AtlasResourceCRD.Core.Html;

public static class HtmlVisualizerGenerator
{
    public static string Generate(AtlasResource resource)
    {
        var spec = resource.Spec;
        var meta = resource.Metadata;
        var arch = spec.Architecture;
        var sec = spec.Security;
        var qual = spec.Quality;
        var yaml = CrdYamlSerializer.SerializeYaml(resource);

        var contextDiagram = !string.IsNullOrWhiteSpace(arch.ContextDiagram) ? arch.ContextDiagram : arch.MermaidDiagram;
        var componentDiagram = !string.IsNullOrWhiteSpace(arch.ComponentDiagram) ? arch.ComponentDiagram : arch.MermaidDiagram;
        var dataFlowDiagram = !string.IsNullOrWhiteSpace(arch.DataFlowDiagram) ? arch.DataFlowDiagram : arch.MermaidDiagram;

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>{HttpUtility.HtmlEncode(meta.Name)} - Atlas Architecture & Quality</title>");
        sb.AppendLine("  <link rel=\"preconnect\" href=\"https://fonts.googleapis.com\">");
        sb.AppendLine("  <link href=\"https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;600&family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap\" rel=\"stylesheet\">");
        sb.AppendLine("  <script src=\"https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js\"></script>");
        sb.AppendLine("  <style>");
        sb.AppendLine(GetCssStyles());
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Top Navigation Bar
        sb.AppendLine("  <header class=\"navbar\">");
        sb.AppendLine("    <div class=\"nav-container\">");
        sb.AppendLine("      <div class=\"brand\">");
        sb.AppendLine("        <div class=\"logo-icon\">⚡</div>");
        sb.AppendLine($"        <span class=\"brand-title\">{HttpUtility.HtmlEncode(meta.Name)}</span>");
        sb.AppendLine($"        <span class=\"badge tier-badge\">{HttpUtility.HtmlEncode(spec.ComponentOverview.Tier)}</span>");
        sb.AppendLine($"        <span class=\"badge lang-badge\">{HttpUtility.HtmlEncode(spec.TechStack.PrimaryLanguage)}</span>");
        if (sec != null && !string.IsNullOrWhiteSpace(sec.OverallRating))
        {
            sb.AppendLine($"        <span class=\"badge sec-badge\">🛡️ OWASP: {HttpUtility.HtmlEncode(sec.OverallRating)}</span>");
        }
        if (qual != null && qual.SigStars > 0)
        {
            sb.AppendLine($"        <span class=\"badge qual-badge\">⭐ SIG: {qual.SigStars.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}★</span>");
        }
        sb.AppendLine("      </div>");
        sb.AppendLine("      <div class=\"nav-meta\">");
        if (meta.Annotations.TryGetValue("atlas.io/git-commit-short", out var commit))
        {
            sb.AppendLine($"        <span class=\"meta-tag\"><span class=\"meta-label\">Commit:</span> <code>{commit}</code></span>");
        }
        if (meta.Annotations.TryGetValue("atlas.io/git-branch", out var branch))
        {
            sb.AppendLine($"        <span class=\"meta-tag\"><span class=\"meta-label\">Branch:</span> <code>{branch}</code></span>");
        }
        sb.AppendLine($"        <span class=\"meta-tag\"><span class=\"meta-label\">CRD:</span> {resource.ApiVersion}</span>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </header>");

        sb.AppendLine("  <main class=\"container\">");

        // Hero Card
        sb.AppendLine("    <section class=\"hero-card\">");
        sb.AppendLine($"      <h1 class=\"hero-title\">{HttpUtility.HtmlEncode(spec.ComponentOverview.Name)}</h1>");
        sb.AppendLine($"      <p class=\"hero-desc\">{HttpUtility.HtmlEncode(spec.ComponentOverview.Description)}</p>");
        sb.AppendLine("      <div class=\"hero-details\">");
        sb.AppendLine($"        <div><strong>Purpose:</strong> {HttpUtility.HtmlEncode(spec.ComponentOverview.Purpose)}</div>");
        if (!string.IsNullOrEmpty(spec.ComponentOverview.RepositoryUrl))
        {
            sb.AppendLine($"        <div><strong>Repository:</strong> <a href=\"{spec.ComponentOverview.RepositoryUrl}\" target=\"_blank\">{HttpUtility.HtmlEncode(spec.ComponentOverview.RepositoryUrl)}</a></div>");
        }
        if (!string.IsNullOrEmpty(spec.ComponentOverview.Owner))
        {
            sb.AppendLine($"        <div><strong>Owner:</strong> {HttpUtility.HtmlEncode(spec.ComponentOverview.Owner)}</div>");
        }
        sb.AppendLine("      </div>");
        sb.AppendLine("    </section>");

        // Quality (SIG) & Security (OWASP) Dual Scorecard Grid
        sb.AppendLine("    <div class=\"grid-2\">");

        // 1. SIG / ISO 25010 Quality Card
        sb.AppendLine("      <section class=\"card quality-card\">");
        sb.AppendLine("        <div class=\"card-header\">");
        sb.AppendLine("          <h2>⭐ SIG Maintainability & Quality</h2>");
        if (qual != null)
        {
            sb.AppendLine($"          <span class=\"stars-rating\">{RenderStars(qual.SigStars)} <strong class=\"stars-num\">{qual.SigStars.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} / 5.0</strong></span>");
        }
        sb.AppendLine("        </div>");
        if (qual != null)
        {
            sb.AppendLine($"        <p class=\"qual-summary\">{HttpUtility.HtmlEncode(qual.Summary)}</p>");
            sb.AppendLine("        <div class=\"sig-dimensions-grid\">");
            foreach (var dim in qual.Dimensions)
            {
                sb.AppendLine("          <div class=\"sig-dim-item\">");
                sb.AppendLine("            <div class=\"dim-header\">");
                sb.AppendLine($"              <span class=\"dim-name\">{HttpUtility.HtmlEncode(dim.Dimension)}</span>");
                sb.AppendLine($"              <span class=\"dim-stars\">{RenderStars(dim.Stars)}</span>");
                sb.AppendLine("            </div>");
                sb.AppendLine($"            <p class=\"dim-desc\">{HttpUtility.HtmlEncode(dim.Evaluation)}</p>");
                sb.AppendLine("          </div>");
            }
            sb.AppendLine("        </div>");
            if (qual.TechDebtItems.Count > 0)
            {
                sb.AppendLine("        <div class=\"tech-debt-box\">");
                sb.AppendLine("          <h4>⚠️ Technical Debt & Refactoring Focus:</h4>");
                sb.AppendLine("          <ul>");
                foreach (var td in qual.TechDebtItems)
                {
                    sb.AppendLine($"            <li>• {HttpUtility.HtmlEncode(td)}</li>");
                }
                sb.AppendLine("          </ul>");
                sb.AppendLine("        </div>");
            }
        }
        else
        {
            sb.AppendLine("        <p class=\"empty-text\">Quality verdict not available.</p>");
        }
        sb.AppendLine("      </section>");

        // 2. OWASP Security Audit Card
        sb.AppendLine("      <section class=\"card security-card\">");
        sb.AppendLine("        <div class=\"card-header\">");
        sb.AppendLine("          <h2>🛡️ OWASP Security Posture</h2>");
        if (sec != null)
        {
            var ratingClass = sec.OverallRating.StartsWith("A") ? "rating-a" : (sec.OverallRating.StartsWith("B") ? "rating-b" : "rating-c");
            sb.AppendLine($"          <span class=\"sec-rating-pill {ratingClass}\">Grade: {HttpUtility.HtmlEncode(sec.OverallRating)} ({sec.SecurityScore}/100)</span>");
        }
        sb.AppendLine("        </div>");
        if (sec != null)
        {
            // OWASP Compliance Checklist Pills
            if (sec.OwaspCompliance.Count > 0)
            {
                sb.AppendLine("        <div class=\"owasp-checks-grid\">");
                foreach (var chk in sec.OwaspCompliance)
                {
                    var statusClass = chk.Status.ToLowerInvariant() switch
                    {
                        "compliant" => "chk-compliant",
                        "partial" => "chk-partial",
                        "noncompliant" => "chk-noncompliant",
                        _ => "chk-na"
                    };
                    var icon = chk.Status.ToLowerInvariant() switch
                    {
                        "compliant" => "✅",
                        "partial" => "⚠️",
                        "noncompliant" => "❌",
                        _ => "ℹ️"
                    };
                    sb.AppendLine($"          <div class=\"owasp-chk-pill {statusClass}\" title=\"{HttpUtility.HtmlEncode(chk.Evidence)}\">");
                    sb.AppendLine($"            <span>{icon} <strong>{HttpUtility.HtmlEncode(chk.Category)}</strong>: {HttpUtility.HtmlEncode(chk.Status)}</span>");
                    sb.AppendLine("          </div>");
                }
                sb.AppendLine("        </div>");
            }

            // Security Findings
            if (sec.Findings.Count > 0)
            {
                sb.AppendLine("        <div class=\"sec-findings-box\">");
                sb.AppendLine("          <h4>🔍 Security Findings & Mitigations:</h4>");
                sb.AppendLine("          <div class=\"finding-list\">");
                foreach (var f in sec.Findings)
                {
                    var sevClass = f.Severity.ToLowerInvariant() switch
                    {
                        "critical" => "sev-critical",
                        "high" => "sev-high",
                        "medium" => "sev-medium",
                        _ => "sev-low"
                    };
                    sb.AppendLine("            <div class=\"finding-item\">");
                    sb.AppendLine("              <div class=\"finding-header\">");
                    sb.AppendLine($"                <span class=\"badge sev-badge {sevClass}\">{HttpUtility.HtmlEncode(f.Severity)}</span>");
                    sb.AppendLine($"                <strong>{HttpUtility.HtmlEncode(f.Title)}</strong>");
                    if (!string.IsNullOrWhiteSpace(f.OwaspRef))
                        sb.AppendLine($"                <code class=\"owasp-ref\">{HttpUtility.HtmlEncode(f.OwaspRef)}</code>");
                    sb.AppendLine("              </div>");
                    sb.AppendLine($"              <p class=\"finding-desc\">{HttpUtility.HtmlEncode(f.Description)}</p>");
                    if (!string.IsNullOrWhiteSpace(f.Mitigation))
                        sb.AppendLine($"              <p class=\"finding-mitigation\"><strong>Fix:</strong> {HttpUtility.HtmlEncode(f.Mitigation)}</p>");
                    sb.AppendLine("            </div>");
                }
                sb.AppendLine("          </div>");
                sb.AppendLine("        </div>");
            }
        }
        else
        {
            sb.AppendLine("        <p class=\"empty-text\">Security audit not available.</p>");
        }
        sb.AppendLine("      </section>");

        sb.AppendLine("    </div>");

        // Interactive Multi-Diagram Suite Card
        sb.AppendLine("    <section class=\"card full-card arch-card\" id=\"diagramSection\">");
        sb.AppendLine("      <div class=\"card-header\">");
        sb.AppendLine("        <div class=\"header-left\">");
        sb.AppendLine("          <h2>🏛️ Interactive Architecture Suite</h2>");
        sb.AppendLine($"          <span class=\"badge pattern-badge\">{HttpUtility.HtmlEncode(spec.Architecture.Pattern)}</span>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"diagram-tabs\">");
        sb.AppendLine("          <button class=\"d-tab active\" onclick=\"switchDiagram('component', this)\">🧩 Component & Protocol Architecture</button>");
        sb.AppendLine("          <button class=\"d-tab\" onclick=\"switchDiagram('context', this)\">🏛️ C4 System Context</button>");
        sb.AppendLine("          <button class=\"d-tab\" onclick=\"switchDiagram('dataflow', this)\">⚡ Data & Event Flow</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"diagram-controls\">");
        sb.AppendLine("          <button class=\"ctrl-btn\" onclick=\"zoomDiagram(1.2)\" title=\"Zoom In\">➕</button>");
        sb.AppendLine("          <button class=\"ctrl-btn\" onclick=\"zoomDiagram(0.8)\" title=\"Zoom Out\">➖</button>");
        sb.AppendLine("          <button class=\"ctrl-btn\" onclick=\"resetZoom()\" title=\"Reset Fit\">⛶ Reset</button>");
        sb.AppendLine("          <button class=\"ctrl-btn fullscreen-btn\" onclick=\"toggleFullscreen()\" title=\"Enlarge / Fullscreen\">⤢ Enlarge</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");

        sb.AppendLine($"      <p class=\"arch-summary\">{HttpUtility.HtmlEncode(spec.Architecture.Summary)}</p>");

        // Pan-Zoom Diagram Viewport
        sb.AppendLine("      <div class=\"diagram-viewport-container\" id=\"diagramViewportContainer\">");
        sb.AppendLine("        <div class=\"diagram-viewport\" id=\"diagramViewport\">");

        // Diagram 1: Component Diagram
        sb.AppendLine("          <div id=\"diag-component\" class=\"diagram-pane active\">");
        sb.AppendLine("            <pre class=\"mermaid\">");
        sb.AppendLine(HttpUtility.HtmlEncode(componentDiagram));
        sb.AppendLine("            </pre>");
        sb.AppendLine("          </div>");

        // Diagram 2: Context Diagram
        sb.AppendLine("          <div id=\"diag-context\" class=\"diagram-pane\">");
        sb.AppendLine("            <pre class=\"mermaid\">");
        sb.AppendLine(HttpUtility.HtmlEncode(contextDiagram));
        sb.AppendLine("            </pre>");
        sb.AppendLine("          </div>");

        // Diagram 3: Data Flow Diagram
        sb.AppendLine("          <div id=\"diag-dataflow\" class=\"diagram-pane\">");
        sb.AppendLine("            <pre class=\"mermaid\">");
        sb.AppendLine(HttpUtility.HtmlEncode(dataFlowDiagram));
        sb.AppendLine("            </pre>");
        sb.AppendLine("          </div>");

        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("    </section>");

        // Grid: Components Detail & Tech Stack
        sb.AppendLine("    <div class=\"grid-2\">");

        // Components Breakdown Card
        sb.AppendLine("      <section class=\"card\">");
        sb.AppendLine("        <div class=\"card-header\">");
        sb.AppendLine($"          <h2>🧩 Subsystems & Modules ({spec.Architecture.Components.Count})</h2>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"component-list\">");
        foreach (var comp in spec.Architecture.Components)
        {
            sb.AppendLine("          <div class=\"comp-card\" onclick=\"inspectComponent('" + HttpUtility.HtmlEncode(comp.Name) + "')\">");
            sb.AppendLine("            <div class=\"comp-header\">");
            sb.AppendLine($"              <strong>{HttpUtility.HtmlEncode(comp.Name)}</strong>");
            sb.AppendLine($"              <span class=\"badge comp-badge\">{HttpUtility.HtmlEncode(comp.Type)}</span>");
            sb.AppendLine("            </div>");
            sb.AppendLine($"            <p class=\"comp-desc\">{HttpUtility.HtmlEncode(comp.Description)}</p>");
            if (comp.Responsibilities.Count > 0)
            {
                sb.AppendLine("            <ul class=\"comp-resp\">");
                foreach (var r in comp.Responsibilities)
                {
                    sb.AppendLine($"              <li>• {HttpUtility.HtmlEncode(r)}</li>");
                }
                sb.AppendLine("            </ul>");
            }
            sb.AppendLine("          </div>");
        }
        sb.AppendLine("        </div>");
        sb.AppendLine("      </section>");

        // Tech Stack & DataStores Card
        sb.AppendLine("      <section class=\"card\">");
        sb.AppendLine("        <div class=\"card-header\">");
        sb.AppendLine("          <h2>🛠️ Tech Stack & Infrastructure</h2>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <div class=\"tech-group\">");
        sb.AppendLine("          <h3>Languages & Frameworks</h3>");
        sb.AppendLine("          <div class=\"tag-cloud\">");
        foreach (var item in spec.TechStack.Languages)
            sb.AppendLine($"            <span class=\"tech-tag lang\">{HttpUtility.HtmlEncode(item.Name)} {HttpUtility.HtmlEncode(item.Version ?? "")}</span>");
        foreach (var item in spec.TechStack.Frameworks)
            sb.AppendLine($"            <span class=\"tech-tag framework\">{HttpUtility.HtmlEncode(item.Name)} {HttpUtility.HtmlEncode(item.Version ?? "")}</span>");
        foreach (var item in spec.TechStack.Runtimes)
            sb.AppendLine($"            <span class=\"tech-tag runtime\">{HttpUtility.HtmlEncode(item.Name)} {HttpUtility.HtmlEncode(item.Version ?? "")}</span>");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <div class=\"tech-group\">");
        sb.AppendLine("          <h3>Data Stores & Caches</h3>");
        sb.AppendLine("          <div class=\"tag-cloud\">");
        foreach (var db in spec.DataStores.Databases)
            sb.AppendLine($"            <span class=\"tech-tag db\">🗄️ {HttpUtility.HtmlEncode(db.Name)} ({HttpUtility.HtmlEncode(db.Type)})</span>");
        foreach (var cache in spec.DataStores.Caches)
            sb.AppendLine($"            <span class=\"tech-tag cache\">⚡ {HttpUtility.HtmlEncode(cache.Name)}</span>");
        foreach (var broker in spec.DataStores.MessageBrokers)
            sb.AppendLine($"            <span class=\"tech-tag broker\">📬 {HttpUtility.HtmlEncode(broker.Name)}</span>");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");

        sb.AppendLine("        <div class=\"tech-group\">");
        sb.AppendLine("          <h3>Observability & Telemetry</h3>");
        sb.AppendLine("          <div class=\"tag-cloud\">");
        if (!string.IsNullOrWhiteSpace(spec.Observability.Logging.Framework))
            sb.AppendLine($"            <span class=\"tech-tag obs\">📝 {HttpUtility.HtmlEncode(spec.Observability.Logging.Framework)}</span>");
        foreach (var sink in spec.Observability.Logging.Sinks)
            sb.AppendLine($"            <span class=\"tech-tag obs-sink\">Sink: {HttpUtility.HtmlEncode(sink)}</span>");
        if (!string.IsNullOrWhiteSpace(spec.Observability.Metrics.Exporter))
            sb.AppendLine($"            <span class=\"tech-tag obs-metric\">📊 {HttpUtility.HtmlEncode(spec.Observability.Metrics.Exporter)}</span>");
        sb.AppendLine("          </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </section>");

        sb.AppendLine("    </div>");

        // API Contracts Section
        if (spec.ApiContracts.Endpoints.Count > 0 || spec.ApiContracts.Events.Count > 0)
        {
            sb.AppendLine("    <section class=\"card full-card\">");
            sb.AppendLine("      <div class=\"card-header\">");
            sb.AppendLine($"        <h2>📡 API Contracts & Endpoints ({spec.ApiContracts.Endpoints.Count})</h2>");
            sb.AppendLine("        <input type=\"text\" id=\"apiSearch\" class=\"search-input\" placeholder=\"Filter endpoints (e.g. GET, /api/items)...\" onkeyup=\"filterApiTable()\">");
            sb.AppendLine("      </div>");
            sb.AppendLine("      <div class=\"table-responsive\">");
            sb.AppendLine("        <table class=\"data-table\" id=\"apiTable\">");
            sb.AppendLine("          <thead>");
            sb.AppendLine("            <tr><th>Method</th><th>Endpoint</th><th>Description</th><th>Auth</th><th>Response Type</th></tr>");
            sb.AppendLine("          </thead>");
            sb.AppendLine("          <tbody>");
            foreach (var ep in spec.ApiContracts.Endpoints)
            {
                var methodClass = ep.Method.ToUpperInvariant() switch
                {
                    "GET" => "method-get",
                    "POST" => "method-post",
                    "PUT" => "method-put",
                    "DELETE" => "method-delete",
                    _ => "method-other"
                };
                sb.AppendLine("            <tr>");
                sb.AppendLine($"              <td><span class=\"method-badge {methodClass}\">{HttpUtility.HtmlEncode(ep.Method)}</span></td>");
                sb.AppendLine($"              <td><code>{HttpUtility.HtmlEncode(ep.Path)}</code></td>");
                sb.AppendLine($"              <td>{HttpUtility.HtmlEncode(ep.Description)}</td>");
                sb.AppendLine($"              <td>{(ep.AuthRequired ? "🔒 Yes" : "🔓 No")}</td>");
                sb.AppendLine($"              <td><code>{HttpUtility.HtmlEncode(ep.ResponseType ?? "-")}</code></td>");
                sb.AppendLine("            </tr>");
            }
            sb.AppendLine("          </tbody>");
            sb.AppendLine("        </table>");
            sb.AppendLine("      </div>");
            sb.AppendLine("    </section>");
        }

        // Dependencies & Configuration
        sb.AppendLine("    <div class=\"grid-2\">");

        // External Dependencies
        sb.AppendLine("      <section class=\"card\">");
        sb.AppendLine("        <div class=\"card-header\">");
        sb.AppendLine($"          <h2>🔗 External Dependencies ({spec.Dependencies.ExternalApis.Count})</h2>");
        sb.AppendLine("        </div>");
        if (spec.Dependencies.ExternalApis.Count > 0)
        {
            sb.AppendLine("        <ul class=\"dep-list\">");
            foreach (var ext in spec.Dependencies.ExternalApis)
            {
                sb.AppendLine("          <li class=\"dep-item\">");
                sb.AppendLine($"            <strong>{HttpUtility.HtmlEncode(ext.Name)}</strong>");
                sb.AppendLine($"            <p>{HttpUtility.HtmlEncode(ext.Purpose)}</p>");
                sb.AppendLine($"            <span class=\"badge crit-badge\">Criticality: {HttpUtility.HtmlEncode(ext.Criticality)}</span>");
                sb.AppendLine("          </li>");
            }
            sb.AppendLine("        </ul>");
        }
        else
        {
            sb.AppendLine("        <p class=\"empty-text\">No external API dependencies identified.</p>");
        }
        sb.AppendLine("      </section>");

        // Environment Variables & Config
        sb.AppendLine("      <section class=\"card\">");
        sb.AppendLine("        <div class=\"card-header\">");
        sb.AppendLine($"          <h2>⚙️ Configuration & Environment ({spec.Configuration.EnvironmentVariables.Count})</h2>");
        sb.AppendLine("        </div>");
        if (spec.Configuration.EnvironmentVariables.Count > 0)
        {
            sb.AppendLine("        <div class=\"table-responsive\">");
            sb.AppendLine("          <table class=\"data-table\">");
            sb.AppendLine("            <thead><tr><th>Variable</th><th>Default</th><th>Required</th><th>Description</th></tr></thead>");
            sb.AppendLine("            <tbody>");
            foreach (var env in spec.Configuration.EnvironmentVariables)
            {
                sb.AppendLine("              <tr>");
                sb.AppendLine($"                <td><code>{HttpUtility.HtmlEncode(env.Name)}</code></td>");
                sb.AppendLine($"                <td>{HttpUtility.HtmlEncode(env.DefaultValue ?? "-")}</td>");
                sb.AppendLine($"                <td>{(env.Required ? "✅ Yes" : "No")}</td>");
                sb.AppendLine($"                <td>{HttpUtility.HtmlEncode(env.Description)}</td>");
                sb.AppendLine("              </tr>");
            }
            sb.AppendLine("            </tbody>");
            sb.AppendLine("          </table>");
            sb.AppendLine("        </div>");
        }
        else
        {
            sb.AppendLine("        <p class=\"empty-text\">No environment variables configured.</p>");
        }
        sb.AppendLine("      </section>");

        sb.AppendLine("    </div>");

        // CRD Manifest Raw Viewer Tab
        sb.AppendLine("    <section class=\"card full-card\">");
        sb.AppendLine("      <div class=\"card-header\">");
        sb.AppendLine("        <h2>📜 Kubernetes AtlasResource CRD Manifest (atlas.yaml)</h2>");
        sb.AppendLine("        <button class=\"copy-btn\" onclick=\"copyCrdYaml()\">📋 Copy YAML</button>");
        sb.AppendLine("      </div>");
        sb.AppendLine($"      <pre class=\"yaml-viewer\"><code id=\"crdYamlCode\">{HttpUtility.HtmlEncode(yaml)}</code></pre>");
        sb.AppendLine("    </section>");

        sb.AppendLine("  </main>");

        // Sliding Inspector Drawer
        sb.AppendLine("  <div class=\"inspector-drawer\" id=\"inspectorDrawer\">");
        sb.AppendLine("    <div class=\"drawer-header\">");
        sb.AppendLine("      <h3 id=\"drawerTitle\">Component Details</h3>");
        sb.AppendLine("      <button class=\"close-btn\" onclick=\"closeDrawer()\">✕</button>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"drawer-body\" id=\"drawerBody\">");
        sb.AppendLine("      <p>Click on any component or diagram node to inspect responsibilities and details.</p>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");

        sb.AppendLine("  <footer class=\"footer\">");
        sb.AppendLine($"    <p>Generated by <strong>AtlasResourceCRD CLI</strong> • {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine("  </footer>");

        // Client-Side Scripts (Mermaid, Pan/Zoom, Tabs, Fullscreen)
        sb.AppendLine("  <script>");
        sb.AppendLine(GetClientJs());
        sb.AppendLine("  </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string RenderStars(double stars)
    {
        var full = (int)Math.Floor(stars);
        var half = (stars - full) >= 0.5 ? 1 : 0;
        var empty = 5 - full - half;

        var sb = new StringBuilder();
        for (int i = 0; i < full; i++) sb.Append("★");
        if (half > 0) sb.Append("½");
        for (int i = 0; i < empty; i++) sb.Append("☆");
        return sb.ToString();
    }

    public static void GenerateToFile(AtlasResource resource, string outputPath)
    {
        var html = Generate(resource);
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(outputPath, html);
    }

    private static string GetCssStyles() => """
:root {
  --bg-dark: #0b1120;
  --bg-card: #1e293b;
  --bg-card-hover: #26354a;
  --text-primary: #f8fafc;
  --text-secondary: #94a3b8;
  --border-color: #334155;
  --accent-blue: #38bdf8;
  --accent-cyan: #06b6d4;
  --accent-green: #10b981;
  --accent-purple: #a855f7;
  --accent-amber: #f59e0b;
  --accent-red: #ef4444;
}

* { box-sizing: border-box; margin: 0; padding: 0; }

body {
  background-color: var(--bg-dark);
  color: var(--text-primary);
  font-family: 'Plus Jakarta Sans', -apple-system, BlinkMacSystemFont, sans-serif;
  line-height: 1.6;
  padding-bottom: 3rem;
}

.navbar {
  background: rgba(30, 41, 59, 0.85);
  backdrop-filter: blur(12px);
  border-bottom: 1px solid var(--border-color);
  position: sticky;
  top: 0;
  z-index: 100;
}

.nav-container {
  max-width: 1400px;
  margin: 0 auto;
  padding: 1rem 1.5rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 1rem;
}

.brand { display: flex; align-items: center; gap: 0.75rem; }
.logo-icon {
  font-size: 1.5rem;
  background: linear-gradient(135deg, #38bdf8, #818cf8);
  border-radius: 8px;
  width: 36px;
  height: 36px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.brand-title { font-size: 1.25rem; font-weight: 800; }
.badge { font-size: 0.75rem; font-weight: 700; padding: 0.25rem 0.6rem; border-radius: 9999px; text-transform: uppercase; }
.tier-badge { background: #3b82f620; color: #60a5fa; border: 1px solid #3b82f640; }
.lang-badge { background: #10b98120; color: #34d399; border: 1px solid #10b98140; }
.sec-badge { background: #10b98125; color: #34d399; border: 1px solid #10b98150; font-weight: 700; }
.qual-badge { background: #f59e0b25; color: #fbbf24; border: 1px solid #f59e0b50; font-weight: 700; }
.pattern-badge { background: #8b5cf620; color: #c084fc; border: 1px solid #8b5cf640; }
.crit-badge { background: #f59e0b20; color: #fbbf24; border: 1px solid #f59e0b40; font-size: 0.7rem; }
.comp-badge { background: #06b6d420; color: #22d3ee; border: 1px solid #06b6d440; font-size: 0.7rem; }

.nav-meta { display: flex; gap: 1rem; align-items: center; }
.meta-tag { font-size: 0.85rem; color: var(--text-secondary); }
.meta-tag code { font-family: 'JetBrains Mono', monospace; background: #0f172a; padding: 0.2rem 0.4rem; border-radius: 4px; color: #38bdf8; }

.container { max-width: 1400px; margin: 2rem auto; padding: 0 1.5rem; display: flex; flex-direction: column; gap: 1.5rem; }
.hero-card { background: linear-gradient(135deg, #1e293b, #1e1b4b); border: 1px solid var(--border-color); border-radius: 16px; padding: 2rem; }
.hero-title { font-size: 2rem; font-weight: 800; margin-bottom: 0.5rem; }
.hero-desc { font-size: 1.1rem; color: var(--text-secondary); margin-bottom: 1.5rem; max-width: 900px; }
.hero-details { display: flex; gap: 2rem; flex-wrap: wrap; font-size: 0.95rem; }
.hero-details a { color: var(--accent-blue); text-decoration: none; }
.hero-details a:hover { text-decoration: underline; }

.grid-2 { display: grid; grid-template-columns: 1fr 1fr; gap: 1.5rem; }
@media (max-width: 992px) { .grid-2 { grid-template-columns: 1fr; } }

.card { background: var(--bg-card); border: 1px solid var(--border-color); border-radius: 16px; padding: 1.75rem; }
.card-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1.25rem; flex-wrap: wrap; gap: 0.75rem; }
.header-left { display: flex; align-items: center; gap: 0.75rem; }
.card-header h2 { font-size: 1.25rem; font-weight: 700; }

/* Quality Card */
.stars-rating { font-size: 1.15rem; color: #fbbf24; display: flex; align-items: center; gap: 0.4rem; }
.stars-num { color: #f8fafc; font-size: 1rem; }
.qual-summary { color: var(--text-secondary); font-size: 0.9rem; margin-bottom: 1rem; }
.sig-dimensions-grid { display: flex; flex-direction: column; gap: 0.6rem; }
.sig-dim-item { background: #0f172a; padding: 0.75rem 1rem; border-radius: 8px; border: 1px solid var(--border-color); }
.dim-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.25rem; }
.dim-name { font-weight: 600; font-size: 0.9rem; color: var(--accent-blue); }
.dim-stars { color: #fbbf24; font-size: 0.9rem; letter-spacing: 2px; }
.dim-desc { font-size: 0.8rem; color: var(--text-secondary); }
.tech-debt-box { margin-top: 1rem; background: #2a1b18; border: 1px solid #7c2d12; border-radius: 8px; padding: 0.75rem 1rem; font-size: 0.85rem; }
.tech-debt-box h4 { color: #f87171; margin-bottom: 0.35rem; }
.tech-debt-box ul { list-style: none; display: flex; flex-direction: column; gap: 0.25rem; color: #fca5a5; }

/* Security Card */
.sec-rating-pill { font-size: 0.85rem; font-weight: 800; padding: 0.35rem 0.8rem; border-radius: 9999px; }
.sec-rating-pill.rating-a { background: #10b98125; color: #34d399; border: 1px solid #10b98150; }
.sec-rating-pill.rating-b { background: #f59e0b25; color: #fbbf24; border: 1px solid #f59e0b50; }
.sec-rating-pill.rating-c { background: #ef444425; color: #f87171; border: 1px solid #ef444450; }

.owasp-checks-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0.5rem; margin-bottom: 1rem; }
.owasp-chk-pill { background: #0f172a; border: 1px solid var(--border-color); border-radius: 6px; padding: 0.4rem 0.75rem; font-size: 0.8rem; }
.owasp-chk-pill.chk-compliant { border-color: #065f46; color: #6ee7b7; }
.owasp-chk-pill.chk-partial { border-color: #854d0e; color: #fde047; }
.owasp-chk-pill.chk-noncompliant { border-color: #991b1b; color: #fca5a5; }

.sec-findings-box { background: #0f172a; border: 1px solid var(--border-color); border-radius: 8px; padding: 0.75rem 1rem; font-size: 0.85rem; }
.sec-findings-box h4 { color: var(--accent-blue); margin-bottom: 0.5rem; }
.finding-list { display: flex; flex-direction: column; gap: 0.6rem; max-height: 260px; overflow-y: auto; }
.finding-item { background: #1e293b; padding: 0.6rem 0.8rem; border-radius: 6px; border-left: 3px solid var(--accent-amber); }
.finding-header { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.25rem; flex-wrap: wrap; }
.sev-badge { font-size: 0.7rem; font-weight: 700; padding: 0.15rem 0.4rem; border-radius: 4px; }
.sev-badge.sev-critical { background: #dc2626; color: #fff; }
.sev-badge.sev-high { background: #ea580c; color: #fff; }
.sev-badge.sev-medium { background: #d97706; color: #fff; }
.sev-badge.sev-low { background: #0284c7; color: #fff; }
.owasp-ref { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; background: #0f172a; padding: 0.1rem 0.3rem; border-radius: 4px; color: #38bdf8; }
.finding-desc { font-size: 0.8rem; color: var(--text-secondary); margin-bottom: 0.2rem; }
.finding-mitigation { font-size: 0.8rem; color: #34d399; }

/* Diagram Tabs & Viewport */
.diagram-tabs { display: flex; gap: 0.4rem; background: #0f172a; padding: 0.3rem; border-radius: 10px; border: 1px solid var(--border-color); }
.d-tab { background: transparent; border: none; color: var(--text-secondary); padding: 0.4rem 0.8rem; border-radius: 6px; font-weight: 600; font-size: 0.85rem; cursor: pointer; transition: all 0.2s; }
.d-tab.active { background: #334155; color: #fff; }
.d-tab:hover:not(.active) { color: var(--text-primary); }

.diagram-controls { display: flex; gap: 0.4rem; }
.ctrl-btn { background: #334155; color: #fff; border: 1px solid var(--border-color); padding: 0.4rem 0.75rem; border-radius: 8px; cursor: pointer; font-size: 0.85rem; font-weight: 600; transition: all 0.2s; }
.ctrl-btn:hover { background: #475569; border-color: var(--accent-blue); }
.ctrl-btn.fullscreen-btn { background: #0284c7; border-color: #38bdf8; }
.ctrl-btn.fullscreen-btn:hover { background: #0369a1; }

.arch-summary { color: var(--text-secondary); font-size: 0.95rem; margin-bottom: 1.25rem; }

.diagram-viewport-container {
  background: #060b14;
  border-radius: 14px;
  border: 1px solid var(--border-color);
  overflow: hidden;
  position: relative;
  min-height: 520px;
  cursor: grab;
}

.diagram-viewport-container:active { cursor: grabbing; }

.diagram-viewport {
  width: 100%;
  height: 100%;
  transform-origin: 0 0;
  transition: transform 0.05s ease-out;
  padding: 2rem;
  display: flex;
  justify-content: center;
}

.diagram-pane { display: none; width: 100%; }
.diagram-pane.active { display: block; }
.diagram-pane .mermaid { display: flex; justify-content: center; }

/* Fullscreen Mode */
.card.fullscreen {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  width: 100vw; height: 100vh;
  z-index: 1000;
  border-radius: 0;
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
}

.card.fullscreen .diagram-viewport-container {
  flex: 1;
  min-height: auto;
}

/* Component Cards */
.component-list { display: flex; flex-direction: column; gap: 0.75rem; max-height: 500px; overflow-y: auto; }
.comp-card { background: #0f172a; border: 1px solid var(--border-color); border-radius: 10px; padding: 1rem; cursor: pointer; transition: all 0.2s; }
.comp-card:hover { border-color: var(--accent-blue); transform: translateY(-2px); }
.comp-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.4rem; }
.comp-desc { font-size: 0.85rem; color: var(--text-secondary); margin-bottom: 0.4rem; }
.comp-resp { list-style: none; font-size: 0.8rem; color: #cbd5e1; display: flex; flex-direction: column; gap: 0.2rem; }

/* Tech Tags */
.tech-group { margin-bottom: 1.25rem; }
.tech-group h3 { font-size: 0.85rem; color: var(--text-secondary); text-transform: uppercase; letter-spacing: 0.05em; margin-bottom: 0.5rem; }
.tag-cloud { display: flex; flex-wrap: wrap; gap: 0.5rem; }
.tech-tag { font-size: 0.85rem; padding: 0.35rem 0.75rem; border-radius: 8px; background: #334155; color: var(--text-primary); font-weight: 500; }
.tech-tag.lang { background: #0369a130; color: #7dd3fc; border: 1px solid #0284c750; }
.tech-tag.framework { background: #6366f130; color: #a5b4fc; border: 1px solid #6366f150; }
.tech-tag.db { background: #0d948830; color: #5eead4; border: 1px solid #0d948850; }
.tech-tag.obs { background: #d9770630; color: #fcd34d; border: 1px solid #d9770650; }
.tech-tag.obs-sink { background: #475569; color: #cbd5e1; font-size: 0.75rem; }

.search-input { background: #0f172a; border: 1px solid var(--border-color); color: var(--text-primary); padding: 0.5rem 1rem; border-radius: 8px; font-size: 0.9rem; min-width: 280px; }
.search-input:focus { outline: none; border-color: var(--accent-blue); }

.table-responsive { overflow-x: auto; }
.data-table { width: 100%; border-collapse: collapse; font-size: 0.9rem; }
.data-table th { text-align: left; padding: 0.75rem 1rem; background: #0f172a; color: var(--text-secondary); border-bottom: 1px solid var(--border-color); }
.data-table td { padding: 0.75rem 1rem; border-bottom: 1px solid #33415550; }
.data-table tr:hover { background: #26354a50; }
.data-table code { font-family: 'JetBrains Mono', monospace; font-size: 0.85rem; color: #38bdf8; }

.method-badge { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; font-weight: 700; padding: 0.2rem 0.5rem; border-radius: 4px; }
.method-get { background: #0284c7; color: #fff; }
.method-post { background: #16a34a; color: #fff; }
.method-put { background: #d97706; color: #fff; }
.method-delete { background: #dc2626; color: #fff; }
.method-other { background: #6b7280; color: #fff; }

.dep-list { list-style: none; display: flex; flex-direction: column; gap: 0.75rem; }
.dep-item { background: #0f172a; border: 1px solid var(--border-color); border-radius: 8px; padding: 0.75rem 1rem; }
.dep-item strong { color: var(--accent-blue); font-size: 0.95rem; }
.dep-item p { font-size: 0.85rem; color: var(--text-secondary); margin: 0.25rem 0; }

.copy-btn { background: #334155; color: var(--text-primary); border: 1px solid var(--border-color); padding: 0.4rem 0.8rem; border-radius: 6px; cursor: pointer; font-size: 0.85rem; font-weight: 600; }
.copy-btn:hover { background: #475569; border-color: var(--accent-blue); }

.yaml-viewer { background: #060b14; border: 1px solid var(--border-color); border-radius: 12px; padding: 1.25rem; overflow-x: auto; max-height: 500px; }
.yaml-viewer code { font-family: 'JetBrains Mono', monospace; font-size: 0.85rem; color: #e2e8f0; }

/* Inspector Drawer */
.inspector-drawer {
  position: fixed;
  right: -420px;
  top: 0;
  width: 400px;
  height: 100vh;
  background: #1e293b;
  border-left: 1px solid var(--border-color);
  box-shadow: -8px 0 24px rgba(0,0,0,0.5);
  z-index: 1100;
  transition: right 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  padding: 1.5rem;
  overflow-y: auto;
}

.inspector-drawer.open { right: 0; }
.drawer-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 1rem; border-bottom: 1px solid var(--border-color); padding-bottom: 0.75rem; }
.drawer-header h3 { font-size: 1.15rem; font-weight: 700; color: var(--accent-blue); }
.close-btn { background: transparent; border: none; color: var(--text-secondary); font-size: 1.2rem; cursor: pointer; }
.close-btn:hover { color: #fff; }

.footer { text-align: center; color: var(--text-secondary); font-size: 0.85rem; margin-top: 2rem; }
""";

    private static string GetClientJs() => """
mermaid.initialize({ startOnLoad: true, theme: 'dark', securityLevel: 'loose' });

var scale = 1;
var translateX = 0;
var translateY = 0;
var isPanning = false;
var startX = 0;
var startY = 0;

var container = document.getElementById('diagramViewportContainer');
var viewport = document.getElementById('diagramViewport');

function updateTransform() {
  viewport.style.transform = 'translate(' + translateX + 'px, ' + translateY + 'px) scale(' + scale + ')';
}

function zoomDiagram(factor) {
  scale = Math.min(Math.max(0.3, scale * factor), 4);
  updateTransform();
}

function resetZoom() {
  scale = 1;
  translateX = 0;
  translateY = 0;
  updateTransform();
}

function toggleFullscreen() {
  var section = document.getElementById('diagramSection');
  section.classList.toggle('fullscreen');
  var btn = document.querySelector('.fullscreen-btn');
  if (section.classList.contains('fullscreen')) {
    btn.innerText = '✕ Close';
  } else {
    btn.innerText = '⤢ Enlarge';
    resetZoom();
  }
}

document.addEventListener('keydown', function(e) {
  if (e.key === 'Escape') {
    var section = document.getElementById('diagramSection');
    if (section.classList.contains('fullscreen')) {
      toggleFullscreen();
    }
    closeDrawer();
  }
});

// Pan events
container.addEventListener('mousedown', function(e) {
  isPanning = true;
  startX = e.clientX - translateX;
  startY = e.clientY - translateY;
});

window.addEventListener('mousemove', function(e) {
  if (!isPanning) return;
  translateX = e.clientX - startX;
  translateY = e.clientY - startY;
  updateTransform();
});

window.addEventListener('mouseup', function() {
  isPanning = false;
});

// Mouse wheel zoom
container.addEventListener('wheel', function(e) {
  e.preventDefault();
  var factor = e.deltaY < 0 ? 1.1 : 0.9;
  zoomDiagram(factor);
});

// Tab Switcher
function switchDiagram(type, btn) {
  var tabs = document.querySelectorAll('.d-tab');
  tabs.forEach(function(t) { t.classList.remove('active'); });
  btn.classList.add('active');

  var panes = document.querySelectorAll('.diagram-pane');
  panes.forEach(function(p) { p.classList.remove('active'); });

  var activePane = document.getElementById('diag-' + type);
  if (activePane) {
    activePane.classList.add('active');
  }
  resetZoom();
}

// Search API table
function filterApiTable() {
  var input = document.getElementById('apiSearch').value.toUpperCase();
  var trs = document.getElementById('apiTable').getElementsByTagName('tr');
  for (var i = 1; i < trs.length; i++) {
    var text = trs[i].textContent || trs[i].innerText;
    trs[i].style.display = text.toUpperCase().indexOf(input) > -1 ? '' : 'none';
  }
}

// Copy CRD YAML
function copyCrdYaml() {
  var text = document.getElementById('crdYamlCode').innerText;
  navigator.clipboard.writeText(text).then(function() {
    alert('AtlasResource CRD YAML copied to clipboard!');
  });
}

// Inspector Drawer
function inspectComponent(name) {
  var drawer = document.getElementById('inspectorDrawer');
  var title = document.getElementById('drawerTitle');
  var body = document.getElementById('drawerBody');
  title.innerText = name;
  body.innerHTML = '<p><strong>Selected Component:</strong> ' + name + '</p><p style="margin-top:0.75rem; color:#94a3b8;">Detailed responsibilities and active contracts are listed in the modules panel.</p>';
  drawer.classList.add('open');
}

function closeDrawer() {
  document.getElementById('inspectorDrawer').classList.remove('open');
}
""";
}
