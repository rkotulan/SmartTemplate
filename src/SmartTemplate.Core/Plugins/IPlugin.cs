namespace SmartTemplate.Core.Plugins;

public interface IPlugin
{
    string Name { get; }
    Task<Dictionary<string, object?>> EnrichAsync(
        Dictionary<string, object?> data,
        CancellationToken cancellationToken = default);
}
