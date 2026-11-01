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
                var pluginDescriptions = new List<(string Name, int ProblemCount)>();
                var contributedProblems = new List<Problem>();
                foreach (Type type in pluginTypes)
                {
                    var plugin = (IOjGenProblemPlugin)Activator.CreateInstance(type)!;
                    if (plugin is IOjGenProblemPluginMetadata metadata)
                    {
                        if (metadata.ApiVersion != ProblemPluginSdk.ApiVersion)
                            throw new InvalidDataException($"Plugin {plugin.Name} dùng API {metadata.ApiVersion}; OjGen hỗ trợ API {ProblemPluginSdk.ApiVersion}.");
                    }
                    else messages.Add($"Plugin {plugin.Name}: chưa khai báo IOjGenProblemPluginMetadata; đang dùng chế độ tương thích API cũ.");
                    var problems = plugin.CreateProblems()?.ToList()
                        ?? throw new InvalidDataException($"Plugin {plugin.Name} trả về danh sách dạng bài rỗng.");
                    contributedProblems.AddRange(problems);
                    pluginDescriptions.Add((plugin.Name, problems.Count));
                }
                if (!Problems.RegisterMany(contributedProblems, out string? registrationError))
                    throw new InvalidDataException(registrationError ?? "Không đăng ký được dạng bài từ plugin.");
                foreach (var plugin in pluginDescriptions)
                {
                    messages.Add($"Plugin {plugin.Name}: đã nạp {plugin.ProblemCount} dạng bài.");
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
