using System.Text;
using Atlas.Core.Models;

namespace Atlas.Server.Services;

/// <summary>
/// Generates comprehensive, vendor-ready Software Requirements Specifications (SRS) & RFP Blueprint documents
/// from Atlas living specifications.
/// </summary>
public sealed class VendorRfpExportService
{
    public string GenerateMarkdownRfp(AtlasResource crd)
    {
        var spec = crd.Spec;
        var meta = crd.Metadata;
        var fn = spec?.FunctionalSpecs;
        var sb = new StringBuilder();

        sb.AppendLine($"# Software Requirements Specification (SRS) & Vendor Rebuild Blueprint");
        sb.AppendLine($"## Application: {meta.Name}");
        sb.AppendLine();
        sb.AppendLine($"> **Document Version**: 1.0 (Synthesized by Atlas Enterprise Hub)");
        sb.AppendLine($"> **Target Tier**: {spec?.ComponentOverview?.Tier ?? "Backend"}");
        sb.AppendLine($"> **Source Repository**: {spec?.ComponentOverview?.RepositoryUrl ?? meta.Annotations.GetValueOrDefault("atlas.io/git-remote", "Internal")}");
        sb.AppendLine($"> **Commit SHA**: {meta.Annotations.GetValueOrDefault("atlas.io/git-commit", "N/A")}");
        sb.AppendLine($"> **Date Generated**: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // 1. Executive Summary & Purpose
        sb.AppendLine("---");
        sb.AppendLine("## 1. Executive Summary & Purpose");
        sb.AppendLine();
        sb.AppendLine(spec?.ComponentOverview?.Description ?? "Enterprise software component specification.");
        sb.AppendLine();

        // 2. Domain Capabilities Breakdown
        sb.AppendLine("---");
        sb.AppendLine("## 2. Domain Capabilities Breakdown");
        sb.AppendLine();
        if (fn?.Capabilities != null && fn.Capabilities.Count > 0)
        {
            foreach (var cap in fn.Capabilities)
            {
                sb.AppendLine($"### Capability: {cap.Name}");
                sb.AppendLine($"- **Description**: {cap.Description}");
                if (!string.IsNullOrWhiteSpace(cap.BusinessOutcome))
                    sb.AppendLine($"- **Target Business Outcome**: {cap.BusinessOutcome}");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("*(No explicit capabilities defined)*");
            sb.AppendLine();
        }

        // 3. Functional Use Cases & Execution Contracts
        sb.AppendLine("---");
        sb.AppendLine("## 3. Detailed Functional Use Cases");
        sb.AppendLine();
        if (fn?.UseCases != null && fn.UseCases.Count > 0)
        {
            foreach (var uc in fn.UseCases)
            {
                sb.AppendLine($"### [{uc.Id}] {uc.Title}");
                sb.AppendLine($"- **Primary Actor**: {uc.PrimaryActor}");
                sb.AppendLine($"- **Business Value**: {uc.BusinessValue}");
                if (!string.IsNullOrWhiteSpace(uc.Trigger))
                    sb.AppendLine($"- **Trigger**: {uc.Trigger}");

                if (uc.Preconditions != null && uc.Preconditions.Count > 0)
                {
                    sb.AppendLine($"- **Preconditions**:");
                    foreach (var pre in uc.Preconditions) sb.AppendLine($"  * {pre}");
                }

                if (uc.InputDataContracts != null && uc.InputDataContracts.Count > 0)
                {
                    sb.AppendLine($"- **Input Data Contracts**:");
                    foreach (var inp in uc.InputDataContracts) sb.AppendLine($"  * `{inp}`");
                }

                if (uc.MainFlow != null && uc.MainFlow.Count > 0)
                {
                    sb.AppendLine($"- **Main Execution Flow**:");
                    for (int i = 0; i < uc.MainFlow.Count; i++) sb.AppendLine($"  {i + 1}. {uc.MainFlow[i]}");
                }

                if (uc.BusinessRules != null && uc.BusinessRules.Count > 0)
                {
                    sb.AppendLine($"- **Business Rules & Invariants**:");
                    foreach (var br in uc.BusinessRules) sb.AppendLine($"  * {br}");
                }

                if (uc.OutputStateChanges != null && uc.OutputStateChanges.Count > 0)
                {
                    sb.AppendLine($"- **Output State Changes & Emitted Events**:");
                    foreach (var outState in uc.OutputStateChanges) sb.AppendLine($"  * {outState}");
                }

                if (!string.IsNullOrWhiteSpace(uc.ArchitecturalAdvice))
                {
                    sb.AppendLine($"> **Architectural Advice**: {uc.ArchitecturalAdvice}");
                }

                sb.AppendLine();
            }
        }

        // 4. Executable Acceptance Scenarios (Gherkin / BDD)
        sb.AppendLine("---");
        sb.AppendLine("## 4. Executable Acceptance Criteria (Gherkin / BDD Suite)");
        sb.AppendLine();
        var allScenarios = fn?.UseCases?.SelectMany(u => u.AcceptanceScenarios ?? new List<BddScenario>()).ToList() ?? new List<BddScenario>();
        if (allScenarios.Count > 0)
        {
            foreach (var bdd in allScenarios)
            {
                sb.AppendLine($"```gherkin");
                sb.AppendLine($"Scenario: {bdd.ScenarioTitle}");
                sb.AppendLine($"  Given {bdd.Given}");
                sb.AppendLine($"  When  {bdd.When}");
                sb.AppendLine($"  Then  {bdd.Then}");
                sb.AppendLine($"```");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("*(Acceptance criteria inferred from use case flows)*");
            sb.AppendLine();
        }

        // 5. Domain State Machines & Lifecycle Models
        sb.AppendLine("---");
        sb.AppendLine("## 5. Domain State Machines & Lifecycle Models");
        sb.AppendLine();
        if (fn?.StateMachines != null && fn.StateMachines.Count > 0)
        {
            foreach (var sm in fn.StateMachines)
            {
                sb.AppendLine($"### State Model: {sm.EntityName}");
                sb.AppendLine($"- **Initial State**: `{sm.InitialState}`");
                sb.AppendLine($"- **All States**: {string.Join(", ", sm.States.Select(s => $"`{s}`"))}");
                sb.AppendLine();

                if (!string.IsNullOrWhiteSpace(sm.MermaidDiagram))
                {
                    sb.AppendLine("```mermaid");
                    sb.AppendLine(sm.MermaidDiagram);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }

                if (sm.Transitions != null && sm.Transitions.Count > 0)
                {
                    sb.AppendLine("| From State | Trigger Event | To State | Guard Condition | Action / Effect |");
                    sb.AppendLine("|---|---|---|---|---|");
                    foreach (var t in sm.Transitions)
                    {
                        sb.AppendLine($"| `{t.FromState}` | **{t.TriggerEvent}** | `{t.ToState}` | {t.GuardCondition ?? "-"} | {t.ActionEffect ?? "-"} |");
                    }
                    sb.AppendLine();
                }
            }
        }
        else
        {
            sb.AppendLine("*(State machine implicitly governed by use case mutations)*");
            sb.AppendLine();
        }

        // 6. Business Rules & Deterministic Calculation Formulas
        sb.AppendLine("---");
        sb.AppendLine("## 6. Business Rules & Calculation Formulas");
        sb.AppendLine();
        if (fn?.BusinessRulesAndFormulas != null && fn.BusinessRulesAndFormulas.Count > 0)
        {
            sb.AppendLine("| Rule ID | Category | Rule Title | Formal Logic / Formula | Validation Constraint | Error Message |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var br in fn.BusinessRulesAndFormulas)
            {
                sb.AppendLine($"| **{br.RuleId}** | {br.Category} | {br.RuleTitle} | `{br.FormalLogicOrFormula}` | {br.ConstraintOrValidation} | *{br.ErrorMessage ?? "-"}* |");
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("*(Deterministic business rules extracted into use case contracts)*");
            sb.AppendLine();
        }

        // 7. Complete Data Dictionary & Schema Constraints
        sb.AppendLine("---");
        sb.AppendLine("## 7. Domain Data Dictionary & Entity Schemas");
        sb.AppendLine();
        if (fn?.DataDictionary != null && fn.DataDictionary.Count > 0)
        {
            foreach (var entity in fn.DataDictionary)
            {
                sb.AppendLine($"### Entity: `{entity.EntityName}`");
                sb.AppendLine($"- **Description**: {entity.Description}");
                sb.AppendLine($"- **Primary Key**: `{entity.PrimaryKey}`");
                sb.AppendLine();
                sb.AppendLine("| Field Name | Data Type | Required | Constraints | Description | Example |");
                sb.AppendLine("|---|---|---|---|---|---|");
                foreach (var f in entity.Fields)
                {
                    sb.AppendLine($"| `{f.Name}` | `{f.DataType}` | {(f.Required ? "✅ Yes" : "No")} | {f.Constraints ?? "-"} | {f.Description} | `{f.ExampleValue ?? "-"}` |");
                }
                sb.AppendLine();
            }
        }

        // 8. Invariants, Concurrency & Idempotency
        sb.AppendLine("---");
        sb.AppendLine("## 8. Invariants, Concurrency & Idempotency Guarantees");
        sb.AppendLine();
        if (fn?.Invariants != null && fn.Invariants.Count > 0)
        {
            sb.AppendLine("| Invariant ID | Rule Description | Enforcement Mechanism | Concurrency / Locking | Severity |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var inv in fn.Invariants)
            {
                sb.AppendLine($"| **{inv.Id}** | {inv.Description} | {inv.EnforcementMechanism} | `{inv.ConcurrencyRequirement}` | **{inv.ViolationSeverity}** |");
            }
            sb.AppendLine();
        }

        // 9. CycloneDX 1.5 Software Bill of Materials (SBOM)
        sb.AppendLine("---");
        sb.AppendLine("## 9. Software Bill of Materials (CycloneDX 1.5 SBOM)");
        sb.AppendLine();
        var sbom = spec?.Dependencies?.Sbom;
        if (sbom?.Components != null && sbom.Components.Count > 0)
        {
            sb.AppendLine($"Total Direct & Transitive Components: **{sbom.Components.Count}**");
            sb.AppendLine();
            sb.AppendLine("| Component Name | Version | Package URL (PURL) | License | Scope |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var c in sbom.Components)
            {
                var lic = c.Licenses?.FirstOrDefault()?.License?.Id ?? c.Licenses?.FirstOrDefault()?.Expression ?? "MIT";
                sb.AppendLine($"| `{c.Name}` | `{c.Version}` | `{c.Purl}` | {lic} | {c.Scope} |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
