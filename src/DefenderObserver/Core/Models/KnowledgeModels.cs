using System.Text.Json.Serialization;

namespace DefenderObserver.Core.Models;

public sealed class ProviderCatalog
{
    [JsonPropertyName("providerName")]
    public string ProviderName { get; set; } = string.Empty;

    [JsonPropertyName("providerGuid")]
    public string ProviderGuid { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("keywords")]
    public List<KeywordMetadata> Keywords { get; set; } = [];

    [JsonPropertyName("tasks")]
    public List<TaskMetadata> Tasks { get; set; } = [];

    [JsonPropertyName("valueMaps")]
    public Dictionary<string, Dictionary<string, string>> ValueMaps { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("events")]
    public List<EventMetadata> Events { get; set; } = [];
}

public sealed class KeywordMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("mask")]
    public string Mask { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public sealed class TaskMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("manifestTask")]
    public string ManifestTask { get; set; } = string.Empty;

    [JsonPropertyName("guid")]
    public string Guid { get; set; } = string.Empty;
}

public sealed class EventMetadata
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("task")]
    public string Task { get; set; } = string.Empty;

    [JsonPropertyName("opcode")]
    public string Opcode { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public List<FieldMetadata> Fields { get; set; } = [];
}

public sealed class FieldMetadata
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}
