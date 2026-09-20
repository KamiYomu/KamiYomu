using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using MonkeyCache;
using MonkeyCache.LiteDB;

namespace KamiYomu.Web.Infrastructure.Contexts;

public class CacheContext
{
    // ASSUMPTION: Tests may temporarily override this resolver to substitute a fake IBarrel,
    // since MonkeyCache's Barrel.Current is a process-wide singleton that is lazily created once
    // and never rebuilt for the lifetime of the process, making it impossible to isolate or force
    // failures via Barrel.ApplicationId alone in a shared test process. Production keeps using
    // the real Barrel.Current by default.
    internal static Func<IBarrel> CurrentResolver { get; set; } = () => Barrel.Current;

    public IBarrel Current => CurrentResolver();

    public bool TryGetCached<T>(string key, out T value)
    {
        if (!Current.IsExpired(key) && Current.Exists(key))
        {
            T? result = Current.Get<T>(key, GetCacheSerializationOptions());
            value = result;
            return true;
        }
        value = default;
        return false;
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> valueFactory, TimeSpan? expiration = null)
    {
        // Check if cache exists and is not expired
        if (!Current.IsExpired(key) && Current.Exists(key))
        {
            T? result = Current.Get<T>(key, GetCacheSerializationOptions());
            return result;
        }

        // Calculate the value
        T? value = await valueFactory();

        // Store in cache
        Current.Add(key, value, expiration ?? TimeSpan.FromMinutes(30));

        return value;
    }

    public T GetOrSet<T>(string key, Func<T> valueFactory, TimeSpan? expiration = null)
    {
        // Check if cache exists and is not expired
        if (!Current.IsExpired(key) && Current.Exists(key))
        {
            return Current.Get<T>(key, GetCacheSerializationOptions());
        }

        // Calculate the value
        T? value = valueFactory();

        // Store in cache
        Current.Add(key, value, expiration ?? TimeSpan.FromMinutes(30));

        return value;
    }

    public string[] GetKeys(Guid crawlerAgentId)
    {
        return [.. Current.GetKeys(CacheState.Active).Where(x => x.StartsWith(crawlerAgentId.ToString(), StringComparison.OrdinalIgnoreCase))];
    }

    public void EmptyAgentKeys(Guid crawlerAgentId)
    {
        Current.Empty(GetKeys(crawlerAgentId));
    }

    public void EmptyAll()
    {
        Current.EmptyAll();
    }

    public void Empty(params string[] keys)
    {
        Current.Empty(keys);
    }

    public void EmptyExpired()
    {
        Current.EmptyExpired();
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
