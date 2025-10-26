using System.Reflection;

namespace KamiYomu.Web.Extensions
{
    public static class AgentExtension
    {
        public static IEnumerable<Type> FindImplementations(Assembly assembly, Type interfaceType)
        {
            if (!interfaceType.IsInterface)
                throw new ArgumentException("Provided type must be an interface", nameof(interfaceType));

            return assembly.GetTypes()
                .Where(t => interfaceType.IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);
        }
    }
}
