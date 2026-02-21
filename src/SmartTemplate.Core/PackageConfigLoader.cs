using SmartTemplate.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SmartTemplate.Core;

/// <summary>
/// Loads a <c>packages.yaml</c> file that defines one or more template packages
/// for use with <c>st run</c>.
/// </summary>
public static class PackageConfigLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static async Task<List<PackageDefinition>> LoadAsync(string configPath)
    {
        var yaml = await File.ReadAllTextAsync(configPath);
        var root = Deserializer.Deserialize<PackageRoot?>(yaml);
        return root?.Packages ?? [];
    }

    private sealed class PackageRoot
    {
        public List<PackageDefinition> Packages { get; set; } = [];
    }
}
