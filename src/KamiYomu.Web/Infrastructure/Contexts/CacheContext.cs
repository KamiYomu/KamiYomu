using MonkeyCache;
using MonkeyCache.LiteDB;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace KamiYomu.Web.Infrastructure.Contexts;

public class CacheContext
{
    public IBarrel Current => Barrel.Current;

    public bool TryGetCached<T>(string key, out T value)
    {
        if (!Barrel.Current.IsExpired(key) && Barrel.Current.Exists(key))
        {
            T? result = Barrel.Current.Get<T>(key, GetCacheSerializationOptions());
            value = result;
            return true;
        }
        value = default;
        return false;
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> valueFactory, TimeSpan? expiration = null)
    {
        // Check if cache exists and is not expired
        if (!Barrel.Current.IsExpired(key) && Barrel.Current.Exists(key))
        {
            T? result = Barrel.Current.Get<T>(key, GetCacheSerializationOptions());
            return result;
        }

        // Calculate the value
        T? value = await valueFactory();

        // Store in cache
        Barrel.Current.Add(key, value, expiration ?? TimeSpan.FromMinutes(30));

        return value;
    }

    public T GetOrSet<T>(string key, Func<T> valueFactory, TimeSpan? expiration = null)
    {
        // Check if cache exists and is not expired
        if (!Barrel.Current.IsExpired(key) && Barrel.Current.Exists(key))
        {
            return Barrel.Current.Get<T>(key, GetCacheSerializationOptions());
        }

        // Calculate the value
        T? value = valueFactory();

        // Store in cache
        Barrel.Current.Add(key, value, expiration ?? TimeSpan.FromMinutes(30));

        return value;
    }

    public string[] GetKeys(Guid crawlerAgentId) => [.. Barrel.Current.GetKeys(CacheState.Active).Where(x => x.StartsWith(crawlerAgentId.ToString(), StringComparison.OrdinalIgnoreCase))];
    public void EmptyAgentKeys(Guid crawlerAgentId) => Barrel.Current.Empty(GetKeys(crawlerAgentId));
    public void EmptyAll() => Barrel.Current.EmptyAll();
    public void Empty(params string[] keys) => Barrel.Current.Empty(keys);
    public void EmptyExpired() => Barrel.Current.EmptyExpired();
    private JsonSerializerOptions GetCacheSerializationOptions()
    {
        JsonSerializerOptions options = new()
        {
            AllowOutOfOrderMetadataProperties = true,
            PropertyNameCaseInsensitive = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    typeInfo =>
                    {
                        Type clrType = typeInfo.Type;
                        foreach (JsonPropertyInfo property in typeInfo.Properties)
                        {
                            if (property.Set == null)
                            {
                                PropertyInfo? propInfo = clrType.GetProperty(property.Name,
                                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                                if (propInfo?.SetMethod?.IsPrivate == true)
                                {
                                    property.Set = (obj, value) => propInfo.SetValue(obj, value);
                                }
                            }
                        }
                    }
                }
            }
        };


        return options;

    }

}
