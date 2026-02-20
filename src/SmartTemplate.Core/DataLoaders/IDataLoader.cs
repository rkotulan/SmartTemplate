namespace SmartTemplate.Core.DataLoaders;

public interface IDataLoader
{
    Task<Dictionary<string, object?>> LoadAsync(string source);
    bool CanLoad(string source);
}
