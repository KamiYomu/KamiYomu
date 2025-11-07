using System.Reflection;
using System.Runtime.Loader;

namespace KamiYomu.Web.Entities
{
    public class CrawlerAgentLoadContext : AssemblyLoadContext
    {
        private readonly string _assemblyNameWithoutDll;
        private readonly AssemblyDependencyResolver _resolver;

        public CrawlerAgentLoadContext(string assemblyPath) : base(isCollectible: true)
        {
            _assemblyNameWithoutDll = Path.GetFileNameWithoutExtension(assemblyPath);
            _resolver = new AssemblyDependencyResolver(assemblyPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // 1. Try default resolver
            var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (resolvedPath != null && File.Exists(resolvedPath))
                return LoadFromAssemblyPath(resolvedPath);

            // 2. Try bin folder
            var binPath = Path.Combine(AppContext.BaseDirectory, "bin", $"{assemblyName.Name}.dll");
            if (File.Exists(binPath))
                return LoadFromAssemblyPath(binPath);

            // 3. Try obj folder
            var objPath = Path.Combine(AppContext.BaseDirectory, "obj", $"{assemblyName.Name}.dll");
            if (File.Exists(objPath))
                return LoadFromAssemblyPath(objPath);

            // 4. Try agent/{assemblyNameWithoutDll} folder
            var agentPath = Path.Combine(Settings.SpecialFolders.AgentsDir, _assemblyNameWithoutDll, $"{assemblyName.Name}.dll");
            if (File.Exists(agentPath))
                return LoadFromAssemblyPath(agentPath);

            return null; // fallback to default context
        }
    }


}
