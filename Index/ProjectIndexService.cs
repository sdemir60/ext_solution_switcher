using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace SolutionSwitcher.Index
{
    public static class ProjectIndexService
    {
        private static readonly object _gate = new();
        private static bool _initialized;
        private static AsyncPackage _package;
        private static Options.SolutionSwitcherOptions _options;
        private static FileSystemWatcher _fsw;
        private static CancellationTokenSource _cts = new();
        private static IndexSnapshot _snapshot = new();

        private static string CachePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SolutionSwitcher", "index.json");

        public static void Initialize(AsyncPackage package, Options.SolutionSwitcherOptions options)
        {
            lock (_gate)
            {
                if (_initialized) return;
                _initialized = true;
            }

            _package = package;
            _options = options;

            Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
            LoadCacheIfAny();

            if (_options.RescanOnStartup) _ = ForceRescanAsync();

            SetupWatcher();
        }

        public static async Task ForceRescanAsync()
        {
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var root = _options.RootDirectory;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return;

            var scanner = new DirectoryScanner(root, _options.MaxParallelism);
            var projects = await scanner.ScanAsync(token).ConfigureAwait(false);

            var map = new Dictionary<string, List<ProjectRecord>>(StringComparer.Ordinal);
            foreach (var proj in projects)
            {
                foreach (var ns in proj.DeclaredNamespaces.Concat(new[] { proj.RootNamespace, proj.AssemblyName })
                                                         .Where(s => !string.IsNullOrWhiteSpace(s)))
                {
                    // İndekse tüm prefix'leri koy: OSYS, OSYS.Sales, OSYS.Sales.Service
                    var parts = ns.Split('.');
                    for (int i = 1; i <= parts.Length; i++)
                    {
                        var prefix = string.Join(".", parts.Take(i));
                        if (!map.TryGetValue(prefix, out var list))
                        {
                            list = new List<ProjectRecord>();
                            map[prefix] = list;
                        }
                        if (!list.Any(p => StringComparer.OrdinalIgnoreCase.Equals(p.ProjectPath, proj.ProjectPath)))
                            list.Add(proj);
                    }
                }
            }

            _snapshot = new IndexSnapshot { NamespaceMap = map, BuiltAtUtc = DateTime.UtcNow };
            SaveCache();
        }

        public static IReadOnlyList<ProjectRecord> Query(string @namespace)
        {
            if (string.IsNullOrWhiteSpace(@namespace)) return Array.Empty<ProjectRecord>();

            // En uzun prefix'e öncelik ver
            var parts = @namespace.Split('.');
            for (int i = parts.Length; i >= 1; i--)
            {
                var prefix = string.Join(".", parts.Take(i));
                if (_snapshot.NamespaceMap.TryGetValue(prefix, out var list) && list.Count > 0)
                    return list;
            }
            return Array.Empty<ProjectRecord>();
        }

        private static void SetupWatcher()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_options.RootDirectory) || !Directory.Exists(_options.RootDirectory))
                    return;

                _fsw = new FileSystemWatcher(_options.RootDirectory)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
                };

                void onChange(object s, FileSystemEventArgs e)
                {
                    // Yalın debounce
                    _ = DebouncedRescanAsync();
                }

                _fsw.Created += onChange;
                _fsw.Changed += onChange;
                _fsw.Deleted += onChange;
                _fsw.Renamed += (s, e) => onChange(s, e);
                _fsw.EnableRaisingEvents = true;
            }
            catch { /* yut - düşük öncelik */ }
        }

        static DateTime _lastTrigger = DateTime.MinValue;
        private static async Task DebouncedRescanAsync()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastTrigger).TotalSeconds < 10) return;
            _lastTrigger = now;
            await ForceRescanAsync();
        }

        private static void SaveCache()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_snapshot, Formatting.Indented);
                File.WriteAllText(CachePath, json);
            }
            catch { /* önemli değil */ }
        }

        private static void LoadCacheIfAny()
        {
            try
            {
                if (File.Exists(CachePath))
                {
                    var json = File.ReadAllText(CachePath);
                    var snap = JsonConvert.DeserializeObject<IndexSnapshot>(json);
                    if (snap != null) _snapshot = snap;
                }
            }
            catch { /* önemli değil */ }
        }
    }
}