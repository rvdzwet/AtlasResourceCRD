using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Atlas.Core.Validation;

public sealed class MermaidValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string SanitizedDiagram { get; set; } = string.Empty;
}

public static class MermaidValidator
{
    private static readonly string[] ValidHeaders =
    {
        "flowchart td", "flowchart lr", "flowchart tb", "flowchart bt", "flowchart rl",
        "graph td", "graph lr", "graph tb", "graph bt", "graph rl",
        "sequencediagram", "c4context", "c4component", "c4container", "c4deployment",
        "erdiagram", "classdiagram", "statediagram", "statediagram-v2", "gantt", "pie"
    };

    public static string Sanitize(string? diagram)
    {
        if (string.IsNullOrWhiteSpace(diagram))
        {
            return "flowchart TD\n  Empty[No Diagram Data Available]";
        }

        var text = diagram.Trim();

        // 1. Strip markdown fences if present
        if (text.StartsWith("```mermaid", StringComparison.OrdinalIgnoreCase))
        {
            text = text[10..].TrimStart('\r', '\n');
        }
        else if (text.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            text = text[3..].TrimStart('\r', '\n');
        }

        if (text.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^3].TrimEnd();
        }

        text = text.Trim();

        // 2. Ensure valid diagram header
        var firstLine = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim().ToLowerInvariant() ?? "";
        var hasValidHeader = ValidHeaders.Any(h => firstLine.StartsWith(h));

        if (!hasValidHeader)
        {
            text = "flowchart TD\n" + text;
        }

        // 3. Replace raw arrow symbols inside node labels to prevent Mermaid lexer syntax errors
        text = Regex.Replace(text, @"\[""(.*?)-->(.*?)""\]", "[\"$1→$2\"]");
        text = Regex.Replace(text, @"\[""(.*?)->(.*?)""\]", "[\"$1→$2\"]");
        text = Regex.Replace(text, @"\[""(.*?)<--(.*?)""\]", "[\"$1←$2\"]");
        text = Regex.Replace(text, @"\[""(.*?)<-(.*?)""\]", "[\"$1←$2\"]");

        // 4. Auto-balance unclosed subgraphs
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
        var subgraphCount = 0;
        var endCount = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("subgraph", StringComparison.OrdinalIgnoreCase))
            {
                subgraphCount++;
            }
            else if (trimmed.Equals("end", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("end ", StringComparison.OrdinalIgnoreCase))
            {
                endCount++;
            }
        }

        if (subgraphCount > endCount)
        {
            var sb = new StringBuilder(text);
            for (int i = 0; i < (subgraphCount - endCount); i++)
            {
                sb.AppendLine("\nend");
            }
            text = sb.ToString();
        }

        return text;
    }

    public static MermaidValidationResult Validate(string? diagram)
    {
        var result = new MermaidValidationResult();
        var sanitized = Sanitize(diagram);
        result.SanitizedDiagram = sanitized;

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            result.Errors.Add("Diagram content is empty.");
            return result;
        }

        var lines = sanitized.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            result.Errors.Add("Diagram contains no valid lines.");
            return result;
        }

        // 1. Validate Header
        var header = lines[0].Trim().ToLowerInvariant();
        var hasValidHeader = ValidHeaders.Any(h => header.StartsWith(h));
        if (!hasValidHeader)
        {
            result.Errors.Add($"Diagram missing valid Mermaid header. Found: '{lines[0]}'");
        }

        // 2. Validate Subgraph Balance
        var subgraphCount = 0;
        var endCount = 0;
        var openSquareBrackets = 0;
        var openParens = 0;
        var openCurly = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var lineNum = i + 1;

            // Skip comments and empty lines
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("%%")) continue;

            if (line.StartsWith("subgraph", StringComparison.OrdinalIgnoreCase))
            {
                subgraphCount++;
                // Check subgraph format: subgraph ID ["Title"] or subgraph Title
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    result.Warnings.Add($"Line {lineNum}: Subgraph definition '{line}' may be missing an identifier.");
                }
            }
            else if (line.Equals("end", StringComparison.OrdinalIgnoreCase) || line.StartsWith("end ", StringComparison.OrdinalIgnoreCase))
            {
                endCount++;
                if (endCount > subgraphCount)
                {
                    result.Errors.Add($"Line {lineNum}: Extraneous 'end' statement without matching 'subgraph'.");
                }
            }

            // Check bracket balance across quotes
            var inQuotes = false;
            for (int c = 0; c < line.Length; c++)
            {
                var ch = line[c];
                if (ch == '"' && (c == 0 || line[c - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                }

                if (!inQuotes)
                {
                    if (ch == '[') openSquareBrackets++;
                    else if (ch == ']') openSquareBrackets--;
                    else if (ch == '(') openParens++;
                    else if (ch == ')') openParens--;
                    else if (ch == '{') openCurly++;
                    else if (ch == '}') openCurly--;
                }
            }

            if (inQuotes)
            {
                result.Errors.Add($"Line {lineNum}: Unclosed double quote string in: '{line}'");
            }
        }

        if (subgraphCount != endCount)
        {
            result.Errors.Add($"Unbalanced subgraphs: {subgraphCount} 'subgraph' blocks vs {endCount} 'end' tags.");
        }

        if (openSquareBrackets != 0)
        {
            result.Errors.Add($"Unbalanced square brackets '[': difference is {openSquareBrackets}.");
        }

        if (openParens != 0)
        {
            result.Errors.Add($"Unbalanced parentheses '(': difference is {openParens}.");
        }

        if (openCurly != 0)
        {
            result.Errors.Add($"Unbalanced curly braces '{{': difference is {openCurly}.");
        }

        return result;
    }

    public static string GenerateFallbackDiagram(string title, IEnumerable<string>? componentNames = null, string orientation = "TD")
    {
        var sb = new StringBuilder();
        sb.AppendLine($"flowchart {orientation}");
        sb.AppendLine($"  subgraph Sys [\"{title}\"]");

        var comps = componentNames?.Where(c => !string.IsNullOrWhiteSpace(c)).ToList() ?? new List<string>();
        if (comps.Count == 0)
        {
            sb.AppendLine("    Node1[\"Primary System\"]");
            sb.AppendLine("    Node2[\"Core Components\"]");
            sb.AppendLine("    Node1 -->|\"Processes\"| Node2");
        }
        else
        {
            for (int i = 0; i < comps.Count; i++)
            {
                var id = $"Node_{i + 1}";
                sb.AppendLine($"    {id}[\"{comps[i]}\"]");
                if (i > 0)
                {
                    var prevId = $"Node_{i}";
                    sb.AppendLine($"    {prevId} -->|\"Interacts with\"| {id}");
                }
            }
        }

        sb.AppendLine("  end");
        return sb.ToString();
    }
}
