using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using AtlasResourceCRD.Core.Models;
using AtlasResourceCRD.Core.Serialization;
using AtlasResourceCRD.Core.Validation;

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
        var cr = spec.CodeReview;
        var risk = spec.RiskSummary;
        var tm = spec.ThreatModel;
        var fn = spec.FunctionalSpecs;
        var yaml = CrdYamlSerializer.SerializeYaml(resource);

        var contextDiagram = SanitizeMermaidDiagram(!string.IsNullOrWhiteSpace(arch.ContextDiagram) ? arch.ContextDiagram : arch.MermaidDiagram);
        var componentDiagram = SanitizeMermaidDiagram(!string.IsNullOrWhiteSpace(arch.ComponentDiagram) ? arch.ComponentDiagram : arch.MermaidDiagram);
        var dataFlowDiagram = SanitizeMermaidDiagram(!string.IsNullOrWhiteSpace(arch.DataFlowDiagram) ? arch.DataFlowDiagram : arch.MermaidDiagram);

        // Prepare JSON payload for 360-degree interactive architectural repository drilldown
        var catalogJson = CrdYamlSerializer.SerializeJson(resource);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"  <title>{HttpUtility.HtmlEncode(meta.Name)} - Atlas Architecture & Living Catalog</title>");
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
        if (risk != null && !string.IsNullOrWhiteSpace(risk.OverallRiskLevel))
        {
            var riskBadgeClass = risk.OverallRiskLevel.ToLowerInvariant() switch
            {
                "critical" => "risk-badge-crit",
                "high" => "risk-badge-high",
                "moderate" => "risk-badge-mod",
                _ => "risk-badge-low"
            };
            sb.AppendLine($"        <span class=\"badge {riskBadgeClass}\">🚨 Risk: {HttpUtility.HtmlEncode(risk.OverallRiskLevel)} ({HttpUtility.HtmlEncode(risk.ProductionReadiness)})</span>");
        }
        if (sec != null && !string.IsNullOrWhiteSpace(sec.OverallRating))
        {
            sb.AppendLine($"        <span class=\"badge sec-badge\">🛡️ OWASP: {HttpUtility.HtmlEncode(sec.OverallRating)}</span>");
        }
        if (qual != null && qual.SigStars > 0)
        {
            sb.AppendLine($"        <span class=\"badge qual-badge\">⭐ SIG: {qual.SigStars.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}★</span>");
        }
        if (cr != null && !string.IsNullOrWhiteSpace(cr.ReviewGrade))
        {
            sb.AppendLine($"        <span class=\"badge cr-badge\">🔍 Review: {HttpUtility.HtmlEncode(cr.ReviewGrade)}</span>");
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

        // 1. Executive Risk & Blast Radius Assessment Card
        if (risk != null && (!string.IsNullOrWhiteSpace(risk.ExecutiveSummary) || risk.TopRisks.Count > 0))
        {
            var readinessClass = risk.ProductionReadiness?.ToLowerInvariant() switch
            {
                "approved" => "readiness-approved",
                "conditional" => "readiness-conditional",
                "blocked" => "readiness-blocked",
                _ => "readiness-conditional"
            };
            var riskLevelClass = risk.OverallRiskLevel?.ToLowerInvariant() switch
            {
                "critical" => "risk-crit",
                "high" => "risk-high",
                "moderate" => "risk-mod",
                _ => "risk-low"
            };

            sb.AppendLine("    <section class=\"card full-card risk-assessment-card\">");
            sb.AppendLine("      <div class=\"card-header\">");
            sb.AppendLine("        <div class=\"header-left\">");
            sb.AppendLine("          <h2>🚨 Executive Risk & Blast Radius Assessment</h2>");
            sb.AppendLine($"          <span class=\"readiness-pill {readinessClass}\">Production Readiness: {HttpUtility.HtmlEncode(risk.ProductionReadiness ?? "Conditional")}</span>");
            sb.AppendLine($"          <span class=\"risk-pill {riskLevelClass}\">Overall Risk: {HttpUtility.HtmlEncode(risk.OverallRiskLevel ?? "Moderate")}</span>");
            sb.AppendLine("        </div>");
            sb.AppendLine("      </div>");

            if (!string.IsNullOrWhiteSpace(risk.ExecutiveSummary))
            {
                sb.AppendLine($"      <p class=\"risk-exec-summary\">{HttpUtility.HtmlEncode(risk.ExecutiveSummary)}</p>");
            }

            sb.AppendLine("      <div class=\"grid-2\" style=\"margin-bottom: 1.25rem;\">");

            // Blast Radius Box
            sb.AppendLine("        <div class=\"blast-radius-box\">");
            sb.AppendLine("          <h4>💥 Blast Radius & Cascade Containment</h4>");
            sb.AppendLine($"          <p>{HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(risk.BlastRadiusEvaluation) ? "Local failure containment evaluated; external cloud outages fall back to local offline caching." : risk.BlastRadiusEvaluation)}</p>");
            sb.AppendLine("        </div>");

            // Restricted Environment Box
            sb.AppendLine("        <div class=\"restricted-env-box\">");
            sb.AppendLine("          <h4>🔒 Restricted & Air-Gapped Environment Compliance</h4>");
            sb.AppendLine($"          <p>{HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(risk.RestrictedEnvironmentCompliance) ? "System supports air-gapped execution when cloud integrations are disabled; requires mTLS and key authentication." : risk.RestrictedEnvironmentCompliance)}</p>");
            sb.AppendLine("        </div>");

            sb.AppendLine("      </div>");

            // Top Risks Table
            if (risk.TopRisks.Count > 0)
            {
                sb.AppendLine("      <div class=\"card-header\" style=\"margin-top: 0.5rem;\">");
                sb.AppendLine($"        <h3>Key Architectural Risks & Mitigations ({risk.TopRisks.Count})</h3>");
                sb.AppendLine("      </div>");
                sb.AppendLine("      <div class=\"table-responsive\">");
                sb.AppendLine("        <table class=\"data-table\">");
                sb.AppendLine("          <thead>");
                sb.AppendLine("            <tr><th>Level</th><th>Risk Title & Impact</th><th>Likelihood</th><th>Trigger Scenario</th><th>Mandatory Mitigation</th></tr>");
                sb.AppendLine("          </thead>");
                sb.AppendLine("          <tbody>");
                foreach (var r in risk.TopRisks)
                {
                    var rClass = r.RiskLevel.ToLowerInvariant() switch
                    {
                        "critical" => "sev-critical",
                        "high" => "sev-high",
                        "medium" => "sev-medium",
                        _ => "sev-low"
                    };
                    sb.AppendLine("            <tr>");
                    sb.AppendLine($"              <td><span class=\"badge sev-badge {rClass}\">{HttpUtility.HtmlEncode(r.RiskLevel)}</span></td>");
                    sb.AppendLine($"              <td><strong>{HttpUtility.HtmlEncode(r.RiskTitle)}</strong><br><small style=\"color:var(--text-secondary);\">{HttpUtility.HtmlEncode(r.Impact)}</small></td>");
                    sb.AppendLine($"              <td><code>{HttpUtility.HtmlEncode(r.Likelihood)}</code></td>");
                    sb.AppendLine($"              <td><span style=\"font-size:0.8rem; color:#cbd5e1;\">{HttpUtility.HtmlEncode(r.TriggerScenario)}</span></td>");
                    sb.AppendLine($"              <td style=\"color:#34d399; font-size:0.85rem;\">{HttpUtility.HtmlEncode(r.RequiredMitigation)}</td>");
                    sb.AppendLine("            </tr>");
                }
                sb.AppendLine("          </tbody>");
                sb.AppendLine("        </table>");
                sb.AppendLine("      </div>");
            }

            sb.AppendLine("    </section>");
        }

        // 2. STRIDE Threat Model Card
        if (tm != null && (tm.Threats.Count > 0 || tm.TrustBoundaries.Count > 0))
        {
            sb.AppendLine("    <section class=\"card full-card threat-model-card\">");
            sb.AppendLine("      <div class=\"card-header\">");
            sb.AppendLine("        <div class=\"header-left\">");
            sb.AppendLine("          <h2>🛡️ STRIDE Threat Model & Attack Surface</h2>");
            sb.AppendLine($"          <span class=\"badge pattern-badge\">Methodology: {HttpUtility.HtmlEncode(tm.Methodology ?? "STRIDE")}</span>");
            sb.AppendLine("        </div>");
            sb.AppendLine("        <input type=\"text\" id=\"tmSearch\" class=\"search-input\" placeholder=\"Filter threats (e.g. Spoofing, Tampering, Port)...\" onkeyup=\"filterThreatTable()\">");
            sb.AppendLine("      </div>");

            if (!string.IsNullOrWhiteSpace(tm.AttackSurfaceSummary))
            {
                sb.AppendLine($"      <p class=\"tm-summary\">{HttpUtility.HtmlEncode(tm.AttackSurfaceSummary)}</p>");
            }

            // Trust Boundaries
            if (tm.TrustBoundaries.Count > 0)
            {
                sb.AppendLine("      <div class=\"trust-boundaries-container\">");
                sb.AppendLine("        <h4>🌐 Trust Boundaries Identified:</h4>");
                sb.AppendLine("        <div class=\"tb-grid\">");
                foreach (var tb in tm.TrustBoundaries)
                {
                    sb.AppendLine("          <div class=\"tb-card\">");
                    sb.AppendLine($"            <strong>{HttpUtility.HtmlEncode(tb.Name)}</strong>");
                    sb.AppendLine($"            <p>{HttpUtility.HtmlEncode(tb.Description)}</p>");
                    if (tb.AssetsInside.Count > 0)
                    {
                        sb.AppendLine($"            <small>Assets: {string.Join(", ", tb.AssetsInside.Select(a => HttpUtility.HtmlEncode(a)))}</small>");
                    }
                    sb.AppendLine("          </div>");
                }
                sb.AppendLine("        </div>");
                sb.AppendLine("      </div>");
            }

            // Threat Vectors Table
            if (tm.Threats.Count > 0)
            {
                sb.AppendLine("      <div class=\"table-responsive\" style=\"margin-top: 1rem;\">");
                sb.AppendLine("        <table class=\"data-table\" id=\"threatTable\">");
                sb.AppendLine("          <thead>");
                sb.AppendLine("            <tr><th>ID</th><th>STRIDE Category</th><th>Target Asset</th><th>Threat Scenario</th><th>Severity</th><th>Mitigation Control</th><th>Residual</th></tr>");
                sb.AppendLine("          </thead>");
                sb.AppendLine("          <tbody>");
                foreach (var t in tm.Threats)
                {
                    var strideClass = t.StrideCategory.ToLowerInvariant() switch
                    {
                        "spoofing" => "stride-spoof",
                        "tampering" => "stride-tamp",
                        "repudiation" => "stride-rep",
                        "informationdisclosure" or "information disclosure" => "stride-info",
                        "denialofservice" or "denial of service" => "stride-dos",
                        _ => "stride-eop"
                    };
                    var sevClass = t.Severity.ToLowerInvariant() switch
                    {
                        "critical" => "sev-critical",
                        "high" => "sev-high",
                        "medium" => "sev-medium",
                        _ => "sev-low"
                    };
                    var resClass = t.ResidualRisk.ToLowerInvariant() switch
                    {
                        "high" => "res-high",
                        "medium" => "res-med",
                        _ => "res-low"
                    };

                    sb.AppendLine("            <tr>");
                    sb.AppendLine($"              <td><code>{HttpUtility.HtmlEncode(t.Id)}</code></td>");
                    sb.AppendLine($"              <td><span class=\"stride-badge {strideClass}\">{HttpUtility.HtmlEncode(t.StrideCategory)}</span></td>");
                    sb.AppendLine($"              <td><strong>{HttpUtility.HtmlEncode(t.TargetAsset)}</strong></td>");
                    sb.AppendLine($"              <td>{HttpUtility.HtmlEncode(t.ThreatScenario)}</td>");
                    sb.AppendLine($"              <td><span class=\"badge sev-badge {sevClass}\">{HttpUtility.HtmlEncode(t.Severity)}</span></td>");
                    sb.AppendLine($"              <td style=\"color:#34d399; font-size:0.85rem;\">{HttpUtility.HtmlEncode(t.MitigationControl)}</td>");
                    sb.AppendLine($"              <td><span class=\"res-pill {resClass}\">{HttpUtility.HtmlEncode(t.ResidualRisk)}</span></td>");
                    sb.AppendLine("            </tr>");
                }
                sb.AppendLine("          </tbody>");
                sb.AppendLine("        </table>");
                sb.AppendLine("      </div>");
            }

            sb.AppendLine("    </section>");
        }

        // 3. Interactive Multi-Diagram Suite Card with C4 Model Palette & Export Tools
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
        sb.AppendLine("          <button class=\"ctrl-btn\" onclick=\"exportDiagramSvg()\" title=\"Export Vector SVG\">💾 SVG</button>");
        sb.AppendLine("          <button class=\"ctrl-btn\" onclick=\"exportDiagramPng()\" title=\"Export PNG Image\">📷 PNG</button>");
        sb.AppendLine("          <button class=\"ctrl-btn fullscreen-btn\" onclick=\"toggleFullscreen()\" title=\"Enlarge / Fullscreen\">⤢ Enlarge</button>");
        sb.AppendLine("        </div>");
        sb.AppendLine("      </div>");

        sb.AppendLine($"      <p class=\"arch-summary\">{HttpUtility.HtmlEncode(spec.Architecture.Summary)}</p>");

        // C4 Official Color Legend Bar
        sb.AppendLine("      <div class=\"c4-legend-bar\">");
        sb.AppendLine("        <span class=\"legend-title\">C4 Model Legend:</span>");
        sb.AppendLine("        <span class=\"legend-item c4-person\"><span class=\"legend-swatch\"></span> 👤 Person / User</span>");
        sb.AppendLine("        <span class=\"legend-item c4-system\"><span class=\"legend-swatch\"></span> 🏢 Software System</span>");
        sb.AppendLine("        <span class=\"legend-item c4-container\"><span class=\"legend-swatch\"></span> 📦 Container / Gateway</span>");
        sb.AppendLine("        <span class=\"legend-item c4-component\"><span class=\"legend-swatch\"></span> 🧩 Component / Service</span>");
        sb.AppendLine("        <span class=\"legend-item c4-external\"><span class=\"legend-swatch\"></span> 🌐 External System / Hardware</span>");
        sb.AppendLine("        <span class=\"legend-item c4-db\"><span class=\"legend-swatch\"></span> 🗄️ Database / Store</span>");
        sb.AppendLine("        <span class=\"legend-hint\">💡 Click any diagram node to open the 360° Architecture Inspector</span>");
        sb.AppendLine("      </div>");

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

        // 4. Living Documentation & Functional Specifications Accordion Card
        if (fn != null && (fn.UseCases.Count > 0 || fn.Capabilities.Count > 0))
        {
            sb.AppendLine("    <section class=\"card full-card living-doc-card\" id=\"livingDocsSection\">");
            sb.AppendLine("      <div class=\"card-header\">");
            sb.AppendLine("        <div class=\"header-left\">");
            sb.AppendLine("          <h2>📖 Living Documentation & Functional Specifications</h2>");
            sb.AppendLine($"          <span class=\"badge qual-badge\">{fn.UseCases.Count} Business Use-Cases</span>");
            sb.AppendLine("        </div>");
            sb.AppendLine("        <input type=\"text\" id=\"ucSearch\" class=\"search-input\" placeholder=\"Filter use-cases (e.g. Actor, Climate, Solar, MQTT)...\" onkeyup=\"filterUseCases()\">");
            sb.AppendLine("      </div>");

            // Capabilities Cloud
            if (fn.Capabilities.Count > 0)
            {
                sb.AppendLine("      <div class=\"capabilities-grid\">");
                foreach (var cap in fn.Capabilities)
                {
                    sb.AppendLine("        <div class=\"cap-pill-card\">");
                    sb.AppendLine($"          <h4>✨ {HttpUtility.HtmlEncode(cap.Name)}</h4>");
                    sb.AppendLine($"          <p>{HttpUtility.HtmlEncode(cap.Description)}</p>");
                    if (!string.IsNullOrWhiteSpace(cap.BusinessOutcome))
                    {
                        sb.AppendLine($"          <small><strong>Outcome:</strong> {HttpUtility.HtmlEncode(cap.BusinessOutcome)}</small>");
                    }
                    sb.AppendLine("        </div>");
                }
                sb.AppendLine("      </div>");
            }

            // Use Case Accordions
            sb.AppendLine("      <div class=\"use-case-accordion-container\" id=\"useCaseContainer\">");
            foreach (var uc in fn.UseCases)
            {
                sb.AppendLine("        <div class=\"uc-card\" data-actor=\"" + HttpUtility.HtmlEncode(uc.PrimaryActor) + "\" data-title=\"" + HttpUtility.HtmlEncode(uc.Title) + "\">");
                sb.AppendLine("          <div class=\"uc-header\" onclick=\"toggleUseCase(this)\">");
                sb.AppendLine("            <div class=\"uc-header-left\">");
                sb.AppendLine($"              <span class=\"uc-id-badge\">{HttpUtility.HtmlEncode(uc.Id)}</span>");
                sb.AppendLine($"              <strong class=\"uc-title\">{HttpUtility.HtmlEncode(uc.Title)}</strong>");
                sb.AppendLine($"              <span class=\"badge tier-badge\">Actor: {HttpUtility.HtmlEncode(uc.PrimaryActor)}</span>");
                if (!string.IsNullOrWhiteSpace(uc.Capability))
                {
                    sb.AppendLine($"              <span class=\"badge pattern-badge\">{HttpUtility.HtmlEncode(uc.Capability)}</span>");
                }
                sb.AppendLine("            </div>");
                sb.AppendLine("            <span class=\"accordion-icon\">▼</span>");
                sb.AppendLine("          </div>");
                sb.AppendLine("          <div class=\"uc-body\">");
                if (!string.IsNullOrWhiteSpace(uc.BusinessValue))
                {
                    sb.AppendLine($"            <div class=\"uc-callout\"><strong>Business Value:</strong> {HttpUtility.HtmlEncode(uc.BusinessValue)}</div>");
                }
                if (!string.IsNullOrWhiteSpace(uc.Trigger))
                {
                    sb.AppendLine($"            <p class=\"uc-field\"><strong>⚡ Trigger:</strong> {HttpUtility.HtmlEncode(uc.Trigger)}</p>");
                }
                if (uc.Preconditions.Count > 0)
                {
                    sb.AppendLine("            <div class=\"uc-section-block\">");
                    sb.AppendLine("              <strong>Prerequisites & Preconditions:</strong>");
                    sb.AppendLine("              <ul>");
                    foreach (var pre in uc.Preconditions) sb.AppendLine($"                <li>• {HttpUtility.HtmlEncode(pre)}</li>");
                    sb.AppendLine("              </ul>");
                    sb.AppendLine("            </div>");
                }
                if (uc.MainFlow.Count > 0)
                {
                    sb.AppendLine("            <div class=\"uc-section-block\">");
                    sb.AppendLine("              <strong>Execution Workflow / Main Flow:</strong>");
                    sb.AppendLine("              <ol class=\"uc-flow-list\">");
                    foreach (var step in uc.MainFlow) sb.AppendLine($"                <li>{HttpUtility.HtmlEncode(step)}</li>");
                    sb.AppendLine("              </ol>");
                    sb.AppendLine("            </div>");
                }
                if (uc.BusinessRules.Count > 0)
                {
                    sb.AppendLine("            <div class=\"uc-section-block\">");
                    sb.AppendLine("              <strong>Business Invariants & Policies:</strong>");
                    sb.AppendLine("              <div class=\"rules-tag-list\">");
                    foreach (var r in uc.BusinessRules) sb.AppendLine($"                <span class=\"rule-tag\">📏 {HttpUtility.HtmlEncode(r)}</span>");
                    sb.AppendLine("              </div>");
                    sb.AppendLine("            </div>");
                }
                if (uc.AcceptanceScenarios.Count > 0)
                {
                    sb.AppendLine("            <div class=\"uc-section-block\">");
                    sb.AppendLine("              <strong>Acceptance Criteria (BDD Given-When-Then):</strong>");
                    sb.AppendLine("              <div class=\"bdd-list\">");
                    foreach (var bdd in uc.AcceptanceScenarios)
                    {
                        sb.AppendLine("                <div class=\"bdd-card\">");
                        sb.AppendLine($"                  <h5>🧪 Scenario: {HttpUtility.HtmlEncode(bdd.ScenarioTitle)}</h5>");
                        sb.AppendLine($"                  <p class=\"bdd-line\"><span class=\"bdd-kw\">Given</span> {HttpUtility.HtmlEncode(bdd.Given)}</p>");
                        sb.AppendLine($"                  <p class=\"bdd-line\"><span class=\"bdd-kw\">When</span> {HttpUtility.HtmlEncode(bdd.When)}</p>");
                        sb.AppendLine($"                  <p class=\"bdd-line\"><span class=\"bdd-kw\">Then</span> {HttpUtility.HtmlEncode(bdd.Then)}</p>");
                        sb.AppendLine("                </div>");
                    }
                    sb.AppendLine("              </div>");
                    sb.AppendLine("            </div>");
                }
                if (uc.AssociatedComponents.Count > 0 || uc.AssociatedApis.Count > 0)
                {
                    sb.AppendLine("            <div class=\"uc-footer-links\">");
                    if (uc.AssociatedComponents.Count > 0)
                    {
                        sb.AppendLine($"              <span><strong>Components:</strong> {string.Join(", ", uc.AssociatedComponents.Select(c => $"<a href=\"javascript:void(0)\" onclick=\"inspectComponent('{HttpUtility.HtmlEncode(c)}')\">{HttpUtility.HtmlEncode(c)}</a>"))}</span>");
                    }
                    if (uc.AssociatedApis.Count > 0)
                    {
                        sb.AppendLine($"              <span><strong>APIs:</strong> {string.Join(", ", uc.AssociatedApis.Select(a => $"<code>{HttpUtility.HtmlEncode(a)}</code>"))}</span>");
                    }
                    sb.AppendLine("            </div>");
                }
                sb.AppendLine("          </div>");
                sb.AppendLine("        </div>");
            }
            sb.AppendLine("      </div>");
            sb.AppendLine("    </section>");
        }

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

        // Automated Code Review & Architectural Insights Card
        if (cr != null && (!string.IsNullOrWhiteSpace(cr.Summary) || cr.Findings.Count > 0 || cr.Strengths.Count > 0))
        {
            var crGradeClass = cr.ReviewGrade.StartsWith("A") ? "grade-a" : (cr.ReviewGrade.StartsWith("B") ? "grade-b" : "grade-c");
            sb.AppendLine("    <section class=\"card full-card code-review-card\">");
            sb.AppendLine("      <div class=\"card-header\">");
            sb.AppendLine("        <div class=\"header-left\">");
            sb.AppendLine("          <h2>🔍 Automated Code Review & Architectural Insights</h2>");
            sb.AppendLine($"          <span class=\"cr-grade-pill {crGradeClass}\">Grade: {HttpUtility.HtmlEncode(cr.ReviewGrade)} ({cr.ReviewScore}/100)</span>");
            sb.AppendLine("        </div>");
            sb.AppendLine("      </div>");

            if (!string.IsNullOrWhiteSpace(cr.Summary))
            {
                sb.AppendLine($"      <p class=\"cr-summary\">{HttpUtility.HtmlEncode(cr.Summary)}</p>");
            }

            sb.AppendLine("      <div class=\"grid-2\" style=\"margin-bottom: 1.25rem;\">");

            if (cr.Strengths.Count > 0)
            {
                sb.AppendLine("        <div class=\"cr-strengths-box\">");
                sb.AppendLine("          <h4>✨ Architectural Strengths & Modern Idioms:</h4>");
                sb.AppendLine("          <ul>");
                foreach (var st in cr.Strengths)
                {
                    sb.AppendLine($"            <li>• {HttpUtility.HtmlEncode(st)}</li>");
                }
                sb.AppendLine("          </ul>");
                sb.AppendLine("        </div>");
            }

            if (cr.CodeSmells.Count > 0)
            {
                sb.AppendLine("        <div class=\"cr-smells-box\">");
                sb.AppendLine("          <h4>⚠️ Anti-Patterns & Code Smells Detected:</h4>");
                sb.AppendLine("          <div class=\"cr-smells-list\">");
                foreach (var smell in cr.CodeSmells)
                {
                    sb.AppendLine("            <div class=\"cr-smell-item\">");
                    sb.AppendLine($"              <div class=\"smell-title\"><strong>{HttpUtility.HtmlEncode(smell.SmellType)}</strong> <code class=\"smell-target\">{HttpUtility.HtmlEncode(smell.AffectedComponentOrFile)}</code></div>");
                    sb.AppendLine($"              <p class=\"smell-desc\">{HttpUtility.HtmlEncode(smell.Description)}</p>");
                    sb.AppendLine("            </div>");
                }
                sb.AppendLine("          </div>");
                sb.AppendLine("        </div>");
            }

            sb.AppendLine("      </div>");

            if (cr.Findings.Count > 0)
            {
                sb.AppendLine("      <div class=\"card-header\" style=\"margin-top: 0.5rem;\">");
                sb.AppendLine($"        <h3>Detailed Review Findings ({cr.Findings.Count})</h3>");
                sb.AppendLine("        <input type=\"text\" id=\"crSearch\" class=\"search-input\" placeholder=\"Filter code review findings (e.g. Performance, Controller)...\" onkeyup=\"filterCrTable()\">");
                sb.AppendLine("      </div>");
                sb.AppendLine("      <div class=\"table-responsive\">");
                sb.AppendLine("        <table class=\"data-table\" id=\"crTable\">");
                sb.AppendLine("          <thead>");
                sb.AppendLine("            <tr><th>Severity</th><th>Category</th><th>Target File / Symbol</th><th>Observation</th><th>Recommended Action</th></tr>");
                sb.AppendLine("          </thead>");
                sb.AppendLine("          <tbody>");
                foreach (var f in cr.Findings)
                {
                    var sevClass = f.Severity.ToLowerInvariant() switch
                    {
                        "critical" => "sev-critical",
                        "major" => "sev-high",
                        "minor" => "sev-medium",
                        _ => "sev-low"
                    };
                    sb.AppendLine("            <tr>");
                    sb.AppendLine($"              <td><span class=\"badge sev-badge {sevClass}\">{HttpUtility.HtmlEncode(f.Severity)}</span></td>");
                    sb.AppendLine($"              <td><span class=\"tech-tag\" style=\"font-size:0.75rem;\">{HttpUtility.HtmlEncode(f.Category)}</span></td>");
                    sb.AppendLine($"              <td><code>{HttpUtility.HtmlEncode(f.File)}</code>{(string.IsNullOrWhiteSpace(f.Symbol) ? "" : $"<br><small style=\"color:var(--accent-blue);\">{HttpUtility.HtmlEncode(f.Symbol)}</small>")}</td>");
                    sb.AppendLine($"              <td><strong>{HttpUtility.HtmlEncode(f.Title)}</strong><br><span style=\"font-size:0.8rem; color:var(--text-secondary);\">{HttpUtility.HtmlEncode(f.Description)}</span></td>");
                    sb.AppendLine($"              <td style=\"color:#34d399; font-size:0.85rem;\">{HttpUtility.HtmlEncode(f.Recommendation)}</td>");
                    sb.AppendLine("            </tr>");
                }
                sb.AppendLine("          </tbody>");
                sb.AppendLine("        </table>");
                sb.AppendLine("      </div>");
            }

            sb.AppendLine("    </section>");
        }

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

        // 360-Degree Architecture Repository Drawer
        sb.AppendLine("  <div class=\"inspector-drawer\" id=\"inspectorDrawer\">");
        sb.AppendLine("    <div class=\"drawer-header\">");
        sb.AppendLine("      <div class=\"drawer-title-group\">");
        sb.AppendLine("        <span class=\"badge comp-badge\" id=\"drawerTypeBadge\">Component</span>");
        sb.AppendLine("        <h3 id=\"drawerTitle\">Component Details</h3>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <button class=\"close-btn\" onclick=\"closeDrawer()\">✕</button>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <div class=\"drawer-body\" id=\"drawerBody\">");
        sb.AppendLine("      <p>Click on any component card or diagram node to inspect responsibilities, mapped business use cases, and contracts.</p>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </div>");

        sb.AppendLine("  <footer class=\"footer\">");
        sb.AppendLine($"    <p>Generated by <strong>AtlasResourceCRD CLI</strong> • {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");
        sb.AppendLine("  </footer>");

        // Client-Side Scripts & Embedded Architecture Data Store
        sb.AppendLine("  <script>");
        sb.AppendLine($"window.__ATLAS_CATALOG__ = {catalogJson};");
        sb.AppendLine(GetClientJs());
        sb.AppendLine("  </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static string SanitizeMermaidDiagram(string? diagram)
    {
        return MermaidValidator.Sanitize(diagram);
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

.brand { display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; }
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
.cr-badge { background: #6366f125; color: #a5b4fc; border: 1px solid #6366f150; font-weight: 700; }
.risk-badge-crit { background: #dc262625; color: #f87171; border: 1px solid #dc262650; font-weight: 700; }
.risk-badge-high { background: #ea580c25; color: #fb923c; border: 1px solid #ea580c50; font-weight: 700; }
.risk-badge-mod { background: #d9770625; color: #fbbf24; border: 1px solid #d9770650; font-weight: 700; }
.risk-badge-low { background: #10b98125; color: #34d399; border: 1px solid #10b98150; font-weight: 700; }
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
.header-left { display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; }
.card-header h2 { font-size: 1.25rem; font-weight: 700; }

/* C4 Legend Bar */
.c4-legend-bar {
  background: #0f172a;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  padding: 0.6rem 1rem;
  margin-bottom: 1rem;
  display: flex;
  align-items: center;
  gap: 1rem;
  flex-wrap: wrap;
  font-size: 0.8rem;
}
.legend-title { font-weight: 700; color: var(--text-secondary); }
.legend-item { display: inline-flex; align-items: center; gap: 0.35rem; font-weight: 600; }
.legend-swatch { width: 12px; height: 12px; border-radius: 3px; display: inline-block; }
.c4-person .legend-swatch { background: #08427b; border: 1px solid #073b6e; }
.c4-system .legend-swatch { background: #1168bd; border: 1px solid #0b4884; }
.c4-container .legend-swatch { background: #2366a0; border: 1px solid #174670; }
.c4-component .legend-swatch { background: #438dd5; border: 1px solid #2b6ba8; }
.c4-external .legend-swatch { background: #686868; border: 1px solid #4a4a4a; }
.c4-db .legend-swatch { background: #08427b; border: 1px solid #073b6e; }
.legend-hint { margin-left: auto; color: var(--accent-blue); font-size: 0.75rem; }

/* Risk Assessment Card */
.readiness-pill { font-size: 0.85rem; font-weight: 800; padding: 0.35rem 0.8rem; border-radius: 9999px; }
.readiness-approved { background: #065f4630; color: #34d399; border: 1px solid #065f46; }
.readiness-conditional { background: #854d0e30; color: #fde047; border: 1px solid #854d0e; }
.readiness-blocked { background: #991b1b30; color: #f87171; border: 1px solid #991b1b; }
.risk-pill { font-size: 0.85rem; font-weight: 800; padding: 0.35rem 0.8rem; border-radius: 9999px; }
.risk-crit { background: #dc262625; color: #f87171; border: 1px solid #dc262650; }
.risk-high { background: #ea580c25; color: #fb923c; border: 1px solid #ea580c50; }
.risk-mod { background: #d9770625; color: #fbbf24; border: 1px solid #d9770650; }
.risk-low { background: #10b98125; color: #34d399; border: 1px solid #10b98150; }
.risk-exec-summary { color: var(--text-secondary); font-size: 0.95rem; margin-bottom: 1.25rem; border-left: 3px solid #f59e0b; padding-left: 0.75rem; }
.blast-radius-box { background: #7f1d1d20; border: 1px solid #991b1b; border-radius: 10px; padding: 1rem; }
.blast-radius-box h4 { color: #f87171; margin-bottom: 0.4rem; font-size: 0.9rem; }
.blast-radius-box p { color: #fca5a5; font-size: 0.85rem; }
.restricted-env-box { background: #1e1b4b; border: 1px solid #4338ca; border-radius: 10px; padding: 1rem; }
.restricted-env-box h4 { color: #a5b4fc; margin-bottom: 0.4rem; font-size: 0.9rem; }
.restricted-env-box p { color: #cbd5e1; font-size: 0.85rem; }

/* Threat Model Card */
.tm-summary { color: var(--text-secondary); font-size: 0.95rem; margin-bottom: 1rem; }
.trust-boundaries-container { background: #0f172a; border: 1px solid var(--border-color); border-radius: 10px; padding: 1rem; margin-bottom: 1rem; }
.trust-boundaries-container h4 { color: var(--accent-blue); margin-bottom: 0.6rem; font-size: 0.9rem; }
.tb-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 0.75rem; }
.tb-card { background: #1e293b; padding: 0.6rem 0.8rem; border-radius: 6px; border: 1px solid var(--border-color); }
.tb-card strong { color: #38bdf8; font-size: 0.85rem; }
.tb-card p { font-size: 0.8rem; color: var(--text-secondary); margin: 0.2rem 0; }
.tb-card small { color: #94a3b8; font-size: 0.75rem; }
.stride-badge { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; font-weight: 700; padding: 0.2rem 0.5rem; border-radius: 4px; }
.stride-spoof { background: #6366f125; color: #a5b4fc; border: 1px solid #6366f150; }
.stride-tamp { background: #ef444425; color: #f87171; border: 1px solid #ef444450; }
.stride-rep { background: #f59e0b25; color: #fbbf24; border: 1px solid #f59e0b50; }
.stride-info { background: #06b6d425; color: #67e8f9; border: 1px solid #06b6d450; }
.stride-dos { background: #dc262625; color: #fca5a5; border: 1px solid #dc262650; }
.stride-eop { background: #a855f725; color: #d8b4fe; border: 1px solid #a855f750; }
.res-pill { font-size: 0.75rem; font-weight: 700; padding: 0.2rem 0.5rem; border-radius: 4px; }
.res-low { background: #065f4630; color: #34d399; }
.res-med { background: #854d0e30; color: #fde047; }
.res-high { background: #991b1b30; color: #f87171; }

/* Living Documentation Accordion Matrix */
.living-doc-card { border-top: 3px solid #10b981; }
.capabilities-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 0.75rem; margin-bottom: 1.5rem; }
.cap-pill-card { background: #0f172a; border: 1px solid var(--border-color); border-radius: 10px; padding: 0.9rem; }
.cap-pill-card h4 { color: #34d399; font-size: 0.95rem; margin-bottom: 0.3rem; }
.cap-pill-card p { font-size: 0.85rem; color: var(--text-secondary); margin-bottom: 0.3rem; }
.cap-pill-card small { color: #94a3b8; font-size: 0.8rem; }

.use-case-accordion-container { display: flex; flex-direction: column; gap: 0.75rem; }
.uc-card { background: #0f172a; border: 1px solid var(--border-color); border-radius: 10px; overflow: hidden; transition: border-color 0.2s; }
.uc-card:hover { border-color: var(--accent-blue); }
.uc-header { display: flex; justify-content: space-between; align-items: center; padding: 0.9rem 1.25rem; cursor: pointer; user-select: none; background: #131d31; }
.uc-header-left { display: flex; align-items: center; gap: 0.75rem; flex-wrap: wrap; }
.uc-id-badge { font-family: 'JetBrains Mono', monospace; font-size: 0.8rem; font-weight: 800; background: #3b82f630; color: #60a5fa; padding: 0.2rem 0.5rem; border-radius: 4px; }
.uc-title { font-size: 1rem; color: var(--text-primary); }
.accordion-icon { font-size: 0.8rem; color: var(--text-secondary); transition: transform 0.2s; }
.uc-card.open .accordion-icon { transform: rotate(180deg); }
.uc-body { display: none; padding: 1.25rem; border-top: 1px solid var(--border-color); background: #0b1120; }
.uc-card.open .uc-body { display: block; }
.uc-callout { background: #1e293b; border-left: 3px solid #38bdf8; padding: 0.6rem 0.8rem; border-radius: 4px; font-size: 0.9rem; margin-bottom: 0.75rem; }
.uc-field { font-size: 0.85rem; color: #cbd5e1; margin-bottom: 0.5rem; }
.uc-section-block { margin-top: 0.85rem; font-size: 0.85rem; }
.uc-section-block strong { color: var(--accent-cyan); display: block; margin-bottom: 0.35rem; }
.uc-section-block ul { list-style: none; display: flex; flex-direction: column; gap: 0.2rem; color: var(--text-secondary); }
.uc-flow-list { padding-left: 1.25rem; display: flex; flex-direction: column; gap: 0.3rem; color: #e2e8f0; }
.rules-tag-list { display: flex; flex-wrap: wrap; gap: 0.4rem; }
.rule-tag { background: #1e293b; border: 1px solid #475569; padding: 0.25rem 0.6rem; border-radius: 6px; font-size: 0.8rem; color: #cbd5e1; }
.bdd-list { display: flex; flex-direction: column; gap: 0.6rem; margin-top: 0.4rem; }
.bdd-card { background: #111c30; border: 1px solid #1e3a5f; border-radius: 8px; padding: 0.75rem 1rem; }
.bdd-card h5 { color: #38bdf8; margin-bottom: 0.4rem; font-size: 0.85rem; }
.bdd-line { font-family: 'JetBrains Mono', monospace; font-size: 0.8rem; color: #cbd5e1; margin: 0.15rem 0; }
.bdd-kw { color: #f59e0b; font-weight: 700; }
.uc-footer-links { margin-top: 1rem; border-top: 1px solid #1e293b; padding-top: 0.75rem; display: flex; gap: 1.5rem; flex-wrap: wrap; font-size: 0.8rem; }
.uc-footer-links a { color: var(--accent-blue); text-decoration: none; }
.uc-footer-links a:hover { text-decoration: underline; }

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

/* Code Review Card */
.cr-grade-pill { font-size: 0.85rem; font-weight: 800; padding: 0.35rem 0.8rem; border-radius: 9999px; }
.cr-grade-pill.grade-a { background: #6366f125; color: #a5b4fc; border: 1px solid #6366f150; }
.cr-grade-pill.grade-b { background: #06b6d425; color: #67e8f9; border: 1px solid #06b6d450; }
.cr-grade-pill.grade-c { background: #f59e0b25; color: #fbbf24; border: 1px solid #f59e0b50; }
.cr-summary { color: var(--text-secondary); font-size: 0.95rem; margin-bottom: 1.25rem; }
.cr-strengths-box { background: #064e3b20; border: 1px solid #065f46; border-radius: 10px; padding: 1rem; }
.cr-strengths-box h4 { color: #34d399; margin-bottom: 0.5rem; font-size: 0.9rem; }
.cr-strengths-box ul { list-style: none; display: flex; flex-direction: column; gap: 0.35rem; color: #a7f3d0; font-size: 0.85rem; }
.cr-smells-box { background: #78350f20; border: 1px solid #854d0e; border-radius: 10px; padding: 1rem; }
.cr-smells-box h4 { color: #fbbf24; margin-bottom: 0.5rem; font-size: 0.9rem; }
.cr-smells-list { display: flex; flex-direction: column; gap: 0.5rem; max-height: 220px; overflow-y: auto; }
.cr-smell-item { background: #0f172a; padding: 0.5rem 0.75rem; border-radius: 6px; border-left: 3px solid #f59e0b; }
.smell-title { font-size: 0.85rem; margin-bottom: 0.2rem; display: flex; justify-content: space-between; align-items: center; }
.smell-target { font-family: 'JetBrains Mono', monospace; font-size: 0.75rem; background: #1e293b; padding: 0.1rem 0.3rem; border-radius: 4px; color: #38bdf8; }
.smell-desc { font-size: 0.8rem; color: var(--text-secondary); }

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
  min-height: 540px;
  cursor: grab;
}

.diagram-viewport-container:active { cursor: grabbing; }

.diagram-viewport {
  width: 100%;
  height: 100%;
  min-height: 500px;
  transform-origin: 0 0;
  transition: transform 0.05s ease-out;
  padding: 2rem;
  position: relative;
}

.diagram-pane {
  display: block;
  visibility: hidden;
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  opacity: 0;
  pointer-events: none;
  transition: opacity 0.15s ease-in-out;
}

.diagram-pane.active {
  position: relative;
  visibility: visible;
  opacity: 1;
  pointer-events: auto;
}

.diagram-pane .mermaid {
  display: flex;
  justify-content: center;
  width: 100%;
}

.diagram-pane .mermaid svg {
  max-width: 100% !important;
  height: auto !important;
  cursor: pointer;
}

/* Node Spotlight effect on hover */
.diagram-pane .mermaid svg g.node {
  transition: opacity 0.2s, transform 0.2s;
}

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

/* 360-Degree Architecture Repository Inspector Drawer */
.inspector-drawer {
  position: fixed;
  right: -480px;
  top: 0;
  width: 460px;
  height: 100vh;
  background: #1e293b;
  border-left: 1px solid var(--border-color);
  box-shadow: -8px 0 32px rgba(0,0,0,0.6);
  z-index: 1100;
  transition: right 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  padding: 1.5rem;
  overflow-y: auto;
}

.inspector-drawer.open { right: 0; }
.drawer-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 1rem; border-bottom: 1px solid var(--border-color); padding-bottom: 0.75rem; }
.drawer-title-group { display: flex; flex-direction: column; gap: 0.35rem; }
.drawer-header h3 { font-size: 1.25rem; font-weight: 800; color: #f8fafc; }
.close-btn { background: transparent; border: none; color: var(--text-secondary); font-size: 1.3rem; cursor: pointer; padding: 0.2rem; }
.close-btn:hover { color: #fff; }

.drawer-section { margin-top: 1.25rem; }
.drawer-section h4 { font-size: 0.85rem; text-transform: uppercase; letter-spacing: 0.05em; color: var(--accent-blue); margin-bottom: 0.5rem; border-bottom: 1px solid #33415550; padding-bottom: 0.25rem; }
.drawer-badge-row { display: flex; gap: 0.5rem; flex-wrap: wrap; margin-bottom: 0.75rem; }
.drawer-list { list-style: none; display: flex; flex-direction: column; gap: 0.35rem; font-size: 0.85rem; color: #cbd5e1; }
.drawer-link { color: var(--accent-cyan); text-decoration: none; }
.drawer-link:hover { text-decoration: underline; }

.footer { text-align: center; color: var(--text-secondary); font-size: 0.85rem; margin-top: 2rem; }
""";

    private static string GetClientJs() => """
mermaid.initialize({
  startOnLoad: false,
  theme: 'base',
  themeVariables: {
    primaryColor: '#1168BD',
    primaryTextColor: '#FFFFFF',
    primaryBorderColor: '#0B4884',
    lineColor: '#38BDF8',
    secondaryColor: '#2366A0',
    tertiaryColor: '#438DD5',
    fontSize: '14px',
    fontFamily: 'Plus Jakarta Sans, sans-serif'
  },
  securityLevel: 'loose',
  flowchart: { useMaxWidth: false, htmlLabels: true, curve: 'basis' }
});

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
  scale = Math.min(Math.max(0.2, scale * factor), 5);
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

var renderedPanes = {};

async function renderPane(paneId) {
  if (renderedPanes[paneId]) return;
  var pane = document.getElementById(paneId);
  if (!pane) return;
  var pre = pane.querySelector('pre.mermaid');
  if (!pre) return;
  try {
    await mermaid.run({ nodes: [pre] });
    renderedPanes[paneId] = true;
    attachNodeClickListeners(pane);
  } catch (err) {
    console.error('Mermaid render error for ' + paneId, err);
  }
}

function attachNodeClickListeners(pane) {
  var nodes = pane.querySelectorAll('svg g.node');
  nodes.forEach(function(node) {
    node.style.cursor = 'pointer';
    node.addEventListener('click', function(e) {
      e.stopPropagation();
      var label = node.textContent || '';
      var clean = label.replace(/\(.*?\)/g, '').trim();
      inspectComponent(clean);
    });
  });
}

// Tab Switcher
async function switchDiagram(type, btn) {
  var tabs = document.querySelectorAll('.d-tab');
  tabs.forEach(function(t) { t.classList.remove('active'); });
  btn.classList.add('active');

  var panes = document.querySelectorAll('.diagram-pane');
  panes.forEach(function(p) { p.classList.remove('active'); });

  var activePane = document.getElementById('diag-' + type);
  if (activePane) {
    activePane.classList.add('active');
    await renderPane('diag-' + type);
  }
  resetZoom();
}

// SVG Export
function exportDiagramSvg() {
  var activePane = document.querySelector('.diagram-pane.active');
  if (!activePane) return;
  var svg = activePane.querySelector('svg');
  if (!svg) { alert('No active diagram rendered yet.'); return; }
  var svgData = new XMLSerializer().serializeToString(svg);
  var blob = new Blob([svgData], { type: 'image/svg+xml;charset=utf-8' });
  var url = URL.createObjectURL(blob);
  var a = document.createElement('a');
  a.href = url;
  a.download = 'architecture_diagram.svg';
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
}

// PNG Export
function exportDiagramPng() {
  var activePane = document.querySelector('.diagram-pane.active');
  if (!activePane) return;
  var svg = activePane.querySelector('svg');
  if (!svg) { alert('No active diagram rendered yet.'); return; }
  var svgData = new XMLSerializer().serializeToString(svg);
  var canvas = document.createElement('canvas');
  var ctx = canvas.getContext('2d');
  var img = new Image();
  var blob = new Blob([svgData], { type: 'image/svg+xml;charset=utf-8' });
  var url = URL.createObjectURL(blob);

  img.onload = function() {
    canvas.width = img.width * 2;
    canvas.height = img.height * 2;
    ctx.fillStyle = '#0b1120';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
    var pngUrl = canvas.toDataURL('image/png');
    var a = document.createElement('a');
    a.href = pngUrl;
    a.download = 'architecture_diagram.png';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  };
  img.src = url;
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

// Search Code Review table
function filterCrTable() {
  var input = document.getElementById('crSearch').value.toUpperCase();
  var trs = document.getElementById('crTable').getElementsByTagName('tr');
  for (var i = 1; i < trs.length; i++) {
    var text = trs[i].textContent || trs[i].innerText;
    trs[i].style.display = text.toUpperCase().indexOf(input) > -1 ? '' : 'none';
  }
}

// Search Threat Model table
function filterThreatTable() {
  var input = document.getElementById('tmSearch').value.toUpperCase();
  var trs = document.getElementById('threatTable').getElementsByTagName('tr');
  for (var i = 1; i < trs.length; i++) {
    var text = trs[i].textContent || trs[i].innerText;
    trs[i].style.display = text.toUpperCase().indexOf(input) > -1 ? '' : 'none';
  }
}

// Search Living Docs Use Cases
function filterUseCases() {
  var input = document.getElementById('ucSearch').value.toUpperCase();
  var cards = document.querySelectorAll('.uc-card');
  cards.forEach(function(card) {
    var text = card.textContent || card.innerText;
    card.style.display = text.toUpperCase().indexOf(input) > -1 ? '' : 'none';
  });
}

function toggleUseCase(header) {
  var card = header.closest('.uc-card');
  card.classList.toggle('open');
}

// Copy CRD YAML
function copyCrdYaml() {
  var text = document.getElementById('crdYamlCode').innerText;
  navigator.clipboard.writeText(text).then(function() {
    alert('AtlasResource CRD YAML copied to clipboard!');
  });
}

// 360-Degree Architecture Repository Inspector
function inspectComponent(name) {
  var catalog = window.__ATLAS_CATALOG__ || {};
  var spec = catalog.spec || {};
  var comps = (spec.architecture && spec.architecture.components) || [];
  var useCases = (spec.functionalSpecs && spec.functionalSpecs.useCases) || [];
  var endpoints = (spec.apiContracts && spec.apiContracts.endpoints) || [];
  var findings = (spec.codeReview && spec.codeReview.findings) || [];

  var comp = comps.find(function(c) {
    return c.name && (c.name.toLowerCase() === name.toLowerCase() || name.toLowerCase().includes(c.name.toLowerCase()));
  });

  var drawer = document.getElementById('inspectorDrawer');
  var title = document.getElementById('drawerTitle');
  var badge = document.getElementById('drawerTypeBadge');
  var body = document.getElementById('drawerBody');

  title.innerText = comp ? comp.name : name;
  badge.innerText = comp ? comp.type : 'Component';

  var html = '';
  if (comp) {
    html += '<p style="color:var(--text-secondary); margin-bottom:1rem;">' + (comp.description || 'No description available.') + '</p>';
    
    if (comp.responsibilities && comp.responsibilities.length > 0) {
      html += '<div class="drawer-section"><h4>Core Responsibilities</h4><ul class="drawer-list">';
      comp.responsibilities.forEach(function(r) { html += '<li>• ' + r + '</li>'; });
      html += '</ul></div>';
    }
  } else {
    html += '<p style="color:var(--text-secondary); margin-bottom:1rem;">Architectural subsystem node: <strong>' + name + '</strong></p>';
  }

  // Mapped Use Cases
  var mappedUcs = useCases.filter(function(u) {
    return (u.associatedComponents && u.associatedComponents.some(function(c) { return c.toLowerCase().includes(name.toLowerCase()); })) ||
           (u.title && u.title.toLowerCase().includes(name.toLowerCase()));
  });

  if (mappedUcs.length > 0) {
    html += '<div class="drawer-section"><h4>Mapped Business Use-Cases (' + mappedUcs.length + ')</h4><ul class="drawer-list">';
    mappedUcs.forEach(function(u) {
      html += '<li><a class="drawer-link" href="#livingDocsSection" onclick="openAndScrollToUseCase(\'' + u.id + '\')"><strong>[' + u.id + ']</strong> ' + u.title + '</a></li>';
    });
    html += '</ul></div>';
  }

  // Associated Endpoints
  var mappedEps = endpoints.filter(function(e) {
    return (e.path && e.path.toLowerCase().includes(name.toLowerCase())) ||
           (e.description && e.description.toLowerCase().includes(name.toLowerCase()));
  });

  if (mappedEps.length > 0) {
    html += '<div class="drawer-section"><h4>Active API Endpoints (' + mappedEps.length + ')</h4><ul class="drawer-list">';
    mappedEps.forEach(function(e) {
      html += '<li><code>' + e.method + ' ' + e.path + '</code><br><small style="color:var(--text-secondary);">' + e.description + '</small></li>';
    });
    html += '</ul></div>';
  }

  // Code Review Findings for this component
  var mappedFindings = findings.filter(function(f) {
    return (f.file && f.file.toLowerCase().includes(name.toLowerCase())) ||
           (f.symbol && f.symbol.toLowerCase().includes(name.toLowerCase()));
  });

  if (mappedFindings.length > 0) {
    html += '<div class="drawer-section"><h4>Review Findings (' + mappedFindings.length + ')</h4><ul class="drawer-list">';
    mappedFindings.forEach(function(f) {
      html += '<li><strong>' + f.title + '</strong> (' + f.severity + ')<br><small style="color:#f87171;">' + f.recommendation + '</small></li>';
    });
    html += '</ul></div>';
  }

  body.innerHTML = html;
  drawer.classList.add('open');
}

function openAndScrollToUseCase(ucId) {
  closeDrawer();
  var card = document.querySelector('.uc-card[data-title*="' + ucId + '"]') || document.querySelector('.uc-id-badge:contains("' + ucId + '")');
  if (card) {
    card.classList.add('open');
    card.scrollIntoView({ behavior: 'smooth', block: 'center' });
  }
}

function closeDrawer() {
  document.getElementById('inspectorDrawer').classList.remove('open');
}

// Initial render
if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', function() {
    renderPane('diag-component');
  });
} else {
  renderPane('diag-component');
}
""";
}
