using System.IO;
using System.Text.Json;
using AtlasResourceCRD.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AtlasResourceCRD.Core.Serialization;

public static class CrdYamlSerializer
{
    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
        .Build();

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string SerializeYaml(AtlasResource resource)
    {
        return YamlSerializer.Serialize(resource);
    }

    public static void SerializeYamlToFile(AtlasResource resource, string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var yaml = SerializeYaml(resource);
        File.WriteAllText(filePath, yaml);
    }

    public static AtlasResource DeserializeYaml(string yamlContent)
    {
        return YamlDeserializer.Deserialize<AtlasResource>(yamlContent);
    }

    public static string SerializeJson(AtlasResource resource)
    {
        return JsonSerializer.Serialize(resource, JsonOptions);
    }

    public static string SerializeJson<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, JsonOptions);
    }

    public static AtlasResource DeserializeJson(string jsonContent)
    {
        return JsonSerializer.Deserialize<AtlasResource>(jsonContent, JsonOptions)
               ?? throw new JsonException("Failed to deserialize AtlasResource JSON.");
    }
}
