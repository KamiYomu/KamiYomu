using Microsoft.Extensions.Options;
using MonkeyCache;
using MonkeyCache.LiteDB;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace KamiYomu.Web.Infrastructure.Contexts
{
    public class CacheContext
    {
        public IBarrel Current => Barrel.Current;
        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> valueFactory, TimeSpan? expiration = null)
        {
            // Check if cache exists and is not expired
            if (!Barrel.Current.IsExpired(key) && Barrel.Current.Exists(key))
            {
                var result = Barrel.Current.Get<T>(key, GetCacheSerializationOptions());
                return result;
            }

            // Calculate the value
            var value = await valueFactory();

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
            var value = valueFactory();

            // Store in cache
            Barrel.Current.Add(key, value, expiration ?? TimeSpan.FromMinutes(30));

            return value;
        }

        public string[] GetKeys(Guid agentId) => [.. Barrel.Current.GetKeys(CacheState.Active).Where(x => x.StartsWith(agentId.ToString(), StringComparison.OrdinalIgnoreCase))];
        public void EmptyAgentKeys(Guid agentId) => Barrel.Current.Empty(GetKeys(agentId));
        public void EmptyAll() => Barrel.Current.EmptyAll();
        public void Empty(params string[] keys) => Barrel.Current.Empty(keys);
        public void EmptyExpired() => Barrel.Current.EmptyExpired();
        private JsonSerializerOptions GetCacheSerializationOptions()
        {
            var options = new JsonSerializerOptions
            {
                AllowOutOfOrderMetadataProperties = true,
                PropertyNameCaseInsensitive = true,
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers =
                    {
                        typeInfo =>
                        {
                            var clrType = typeInfo.Type;
                            foreach (var property in typeInfo.Properties)
                            {
                                if (property.Set == null)
                                {
                                    var propInfo = clrType.GetProperty(property.Name,
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
}
