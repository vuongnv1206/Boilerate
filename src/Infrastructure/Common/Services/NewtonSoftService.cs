using Boilerate.Application.Common.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Boilerate.Infrastructure.Common.Services;

/// <summary>
/// JSON serializer implementation sử dụng Newtonsoft.Json.
/// </summary>
public class NewtonSoftService : ISerializerService
{
    /// <summary>
    /// Deserialize JSON string thành object.
    /// </summary>
    public T Deserialize<T>(string text)
    {
        return JsonConvert.DeserializeObject<T>(text)!;
    }

    /// <summary>
    /// Serialize object thành JSON string với custom settings.
    /// </summary>
    public string Serialize<T>(T obj)
    {
        return JsonConvert.SerializeObject(obj, new JsonSerializerSettings
        {
            // CamelCase property names (firstName thay vì FirstName)
            ContractResolver = new CamelCasePropertyNamesContractResolver(),

            // Ignore null values (không serialize properties null)
            NullValueHandling = NullValueHandling.Ignore,

            // Enum as string (thay vì number)
            Converters = new List<JsonConverter>
            {
                new StringEnumConverter { NamingStrategy = new CamelCaseNamingStrategy() },
            },
        });
    }

    /// <summary>
    /// Serialize object thành JSON string với type cụ thể.
    /// </summary>
    public string Serialize<T>(T obj, Type type)
    {
        return JsonConvert.SerializeObject(obj, type, new JsonSerializerSettings());
    }
}
