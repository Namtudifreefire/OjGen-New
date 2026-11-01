using System.Reflection;
using System.Runtime.Loader;

namespace OjGen.Core;

public static class ProblemPluginLoader
{
    private sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly string _directory;
        private readonly AssemblyDependencyResolver? _resolver;

        public PluginLoadContext(string mainAssemblyPath) : base(isCollectible: false)
        {
            _directory = Path.GetDirectoryName(mainAssemblyPath)!;
            try { _resolver = new AssemblyDependencyResolver(mainAssemblyPath); }
            catch (Exception) { _resolver = null; }
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (string.Equals(assemblyName.Name, typeof(Problem).Assembly.GetName().Name, StringComparison.OrdinalIgnoreCase))
                return typeof(Problem).Assembly;
            string? path = _resolver?.ResolveAssemblyToPath(assemblyName);
            if (path == null && !string.IsNullOrWhiteSpace(assemblyName.Name))
            {
                string local = Path.Combine(_directory, assemblyName.Name + ".dll");
                if (File.Exists(local)) path = local;
            }
            return path != null && File.Exists(path) ? LoadFromAssemblyPath(Path.GetFullPath(path)) : null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? path = _resolver?.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (path == null)
            {
                string local = Path.Combine(_directory, unmanagedDllName);
                if (File.Exists(local)) path = local;
            }
            return path != null && File.Exists(path) ? LoadUnmanagedDllFromPath(Path.GetFullPath(path)) : IntPtr.Zero;
        }
    }

    public static IReadOnlyList<string> LoadFrom(string path)
    {
        var messages = new List<string>();
        if (string.IsNullOrWhiteSpace(path)) return messages;
        string full = Path.GetFullPath(path);
        string[] assemblies;
        try
        {
            assemblies = File.Exists(full) ? new[] { full } : Directory.Exists(full)
                ? Directory.EnumerateFiles(full, "*.dll", SearchOption.TopDirectoryOnly).OrderBy(file => file, StringComparer.OrdinalIgnoreCase).ToArray()
                : Array.Empty<string>();
        }
        catch (Exception ex)
        {
            messages.Add($"Không đọc được thư mục plugin {full}: {ex.Message}");
            return messages;
        }
        if (assemblies.Length == 0) { messages.Add("Không tìm thấy plugin DLL: " + full); return messages; }
        int loadedPlugins = 0;
        foreach (string assemblyPath in assemblies)
        {
            try
            {
                var loadContext = new PluginLoadContext(Path.GetFullPath(assemblyPath));
                Assembly assembly = loadContext.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
                Type[] types = assembly.GetTypes();
                var pluginTypes = types.Where(type => !type.IsAbstract && !type.IsInterface &&
                    typeof(IOjGenProblemPlugin).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) != null).ToList();
                if (pluginTypes.Count == 0)
                {
                    if (File.Exists(full)) messages.Add(Path.GetFileName(assemblyPath) + ": không có IOjGenProblemPlugin public.");
                    continue;
                }
                foreach (Type type in pluginTypes)
                {
                    var plugin = (IOjGenProblemPlugin)Activator.CreateInstance(type)!;
                    int added = 0;
                    foreach (Problem problem in plugin.CreateProblems())
                    {
                        if (!Problems.Register(problem)) throw new InvalidDataException("ID dạng bài rỗng hoặc trùng: " + problem?.Id);
                        added++;
                    }
                    messages.Add($"Plugin {plugin.Name}: đã nạp {added} dạng bài.");
                    loadedPlugins++;
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                string detail = string.Join("; ", ex.LoaderExceptions.Where(item => item != null).Select(item => item!.Message).Take(3));
                messages.Add($"Không nạp được {Path.GetFileName(assemblyPath)}: {detail}");
            }
            catch (Exception ex) { messages.Add($"Không nạp được {Path.GetFileName(assemblyPath)}: {ex.Message}"); }
        }
        if (Directory.Exists(full) && loadedPlugins == 0 && messages.Count == 0)
            messages.Add("Thư mục không chứa plugin OjGen nào.");
        return messages;
    }
}
