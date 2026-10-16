namespace Boilerate.Application.Common.Interfaces;

/// <summary>
/// Interface để serialize/deserialize objects.
/// Dùng cho caching, logging, messaging, etc.
/// </summary>
public interface ISerializerService : ITransientService
{
    /// <summary>
    /// Serialize object thành JSON string.
    /// </summary>
    string Serialize<T>(T obj);

    /// <summary>
    /// Serialize object thành JSON string với type cụ thể.
    /// </summary>
    string Serialize<T>(T obj, Type type);

    /// <summary>
    /// Deserialize JSON string thành object.
    /// </summary>
    T Deserialize<T>(string text);
}
