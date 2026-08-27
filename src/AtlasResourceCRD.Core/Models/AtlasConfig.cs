using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace AtlasResourceCRD.Core.Models;

/// <summary>
/// Project-level configuration loaded optionally from .atlas.yaml
/// </summary>
public sealed class AtlasConfig
{
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

    [YamlMember(Alias = "namespace")]
    public string? Namespace { get; set; }

    [YamlMember(Alias = "tier")]
    public string? Tier { get; set; }

    [YamlMember(Alias = "owner")]
    public string? Owner { get; set; }

    [YamlMember(Alias = "ignoreGlobs")]
    public List<string> IgnoreGlobs { get; set; } = new();

    [YamlMember(Alias = "includeExtensions")]
    public List<string> IncludeExtensions { get; set; } = new();

    [YamlMember(Alias = "labels")]
    public Dictionary<string, string> Labels { get; set; } = new();

    [YamlMember(Alias = "annotations")]
    public Dictionary<string, string> Annotations { get; set; } = new();

    [YamlMember(Alias = "model")]
    public string? Model { get; set; }

    [YamlMember(Alias = "thinkingBudget")]
    public int? ThinkingBudget { get; set; }

    [YamlMember(Alias = "maxFileSizeKb")]
    public int MaxFileSizeKb { get; set; } = 256;

    [YamlMember(Alias = "maxFiles")]
    public int MaxFiles { get; set; } = int.MaxValue;
}
