using KamiYomu.Web.AppOptions;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Runtime.Loader;

namespace KamiYomu.Web.Entities
{
    public class CrawlerAgentLoadContext : AssemblyLoadContext
    {
        private readonly string _baseDir;
        private readonly AssemblyDependencyResolver _resolver;

        public CrawlerAgentLoadContext(string assemblyPath) : base(isCollectible: true)
        {
            _baseDir = Path.GetDirectoryName(assemblyPath)!;
            _resolver = new AssemblyDependencyResolver(assemblyPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {

            // 1. Try application bin path first
            if (string.Equals(assemblyName.Name, "KamiYomu.CrawlerAgents.Core"))
                return null;


            // 2. Try default resolver
            var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (resolvedPath != null && File.Exists(resolvedPath))
                return LoadFromAssemblyPath(resolvedPath);

            // 3. Try same folder as invoker
            var localPath = Path.Combine(_baseDir, $"{assemblyName.Name}.dll");
            if (File.Exists(localPath))
                return LoadFromAssemblyPath(localPath);

            // 4. Try bin folder relative to invoker base
            var binPath = Path.Combine(_baseDir, "bin", $"{assemblyName.Name}.dll");
            if (File.Exists(binPath))
                return LoadFromAssemblyPath(binPath);

            // 5. Try obj folder relative to invoker base
            var objPath = Path.Combine(_baseDir, "obj", $"{assemblyName.Name}.dll");
            if (File.Exists(objPath))
                return LoadFromAssemblyPath(objPath);

            // 6. Try agent folder
            var specialFolderOptions = Defaults.ServiceLocator.Instance.GetRequiredService<IOptions<SpecialFolderOptions>>();

            var agentPath = Path.Combine(specialFolderOptions.Value.AgentsDir, Path.GetFileName(_baseDir), $"{assemblyName.Name}.dll");
            if (File.Exists(agentPath))
                return LoadFromAssemblyPath(agentPath);

            return null;
        }
    }


}
