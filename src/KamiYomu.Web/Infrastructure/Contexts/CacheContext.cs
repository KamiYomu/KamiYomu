using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using MonkeyCache;
using MonkeyCache.LiteDB;

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

    public string[] GetKeys(Guid crawlerAgentId)
    {
        return [.. Barrel.Current.GetKeys(CacheState.Active).Where(x => x.StartsWith(crawlerAgentId.ToString(), StringComparison.OrdinalIgnoreCase))];
    }

    public void EmptyAgentKeys(Guid crawlerAgentId)
    {
        Barrel.Current.Empty(GetKeys(crawlerAgentId));
    }

    public void EmptyAll()
    {
        Barrel.Current.EmptyAll();
    }

    public void Empty(params string[] keys)
    {
        Barrel.Current.Empty(keys);
    }

    public void EmptyExpired()
    {
        Barrel.Current.EmptyExpired();
    }

    private JsonSerializerOptions GetCacheSerializationOptions()
    {
        return new JsonSerializerOptions
        {
            AllowOutOfOrderMetadataProperties = true,
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { PrivateSetterModifier }
            }
        };
    }

    private static void PrivateSetterModifier(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            if (property.Set == null)
            {
                // Look for the real property via reflection
                PropertyInfo? propInfo = typeInfo.Type.GetProperty(property.Name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (propInfo != null)
                {
                    // Assign the setter even if it is private
                    property.Set = propInfo.SetValue;
                }
            }
        }
    }

}
