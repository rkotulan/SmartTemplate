namespace SmartTemplate.Core.Plugins;

public interface IPlugin
{
    string Name { get; }

    /// <summary>
    /// Called once after the plugin is loaded, before any <see cref="EnrichAsync"/> call.
    /// Override to open connections, load configuration, etc.
    /// Default implementation does nothing.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Enriches the merged data dictionary with additional variables.
    /// Keys returned overwrite existing keys of the same name.
    /// </summary>
    Task<Dictionary<string, object?>> EnrichAsync(
        Dictionary<string, object?> data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after all templates have been rendered.
    /// Override to close connections, flush buffers, etc.
    /// Default implementation does nothing.
    /// </summary>
    Task DisposeAsync() => Task.CompletedTask;
}
