using Microsoft.Build.Construction;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace SolutionSwitcher.Index
{
    internal sealed class DirectoryScanner
    {
        private readonly string _root;
        private readonly int _maxDegree;

        public DirectoryScanner(string root, int maxDegree)
        {
            _root = root;
            _maxDegree = Math.Max(2, maxDegree);
        }

        public async Task<List<ProjectRecord>> ScanAsync(CancellationToken token)
        {
            var projects = new ConcurrentDictionary<string, ProjectRecord>(StringComparer.OrdinalIgnoreCase);

            var slnFiles = Directory.EnumerateFiles(_root, "*.sln", SearchOption.AllDirectories)
                                    .Where(p => !p.Contains(@"\.git\") && !p.Contains(@"\bin\") && !p.Contains(@"\obj\"))
                                    .ToList();

            using (var semaphore = new SemaphoreSlim(_maxDegree, _maxDegree))
            {
                var tasks = slnFiles.Select(async slnPath =>
                {
                    await semaphore.WaitAsync(token);
                    try
                    {
                        try
                        {
                            var sln = SolutionFile.Parse(slnPath);
                            foreach (var p in sln.ProjectsInOrder)
                            {
                                if (p.ProjectType != SolutionProjectType.KnownToBeMSBuildFormat) continue;
                                if (!p.AbsolutePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) continue;
                                var projPath = p.AbsolutePath;

                                var rec = projects.GetOrAdd(projPath, _ => new ProjectRecord
                                {
                                    ProjectPath = projPath,
                                    AssemblyName = ReadMsBuildProperty(projPath, "AssemblyName") ?? Path.GetFileNameWithoutExtension(projPath),
                                    RootNamespace = ReadMsBuildProperty(projPath, "RootNamespace") ?? ""
                                });

                                rec.SolutionPaths.Add(slnPath);

                                // Namespace çıkarımı (Roslyn ile hızlı bakış)
                                foreach (var file in EnumerateCSharpFiles(projPath))
                                {
                                    token.ThrowIfCancellationRequested();
                                    try
                                    {
                                        var text = File.ReadAllText(file);
                                        var tree = CSharpSyntaxTree.ParseText(text);
                                        var root = tree.GetCompilationUnitRoot();

                                        foreach (var nsd in root.DescendantNodes().OfType<NamespaceDeclarationSyntax>())
                                        {
                                            var name = nsd.Name.ToString().Trim();
                                            if (!string.IsNullOrWhiteSpace(name))
                                                rec.DeclaredNamespaces.Add(name);
                                        }
                                        foreach (var fsd in root.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>())
                                        {
                                            var name = fsd.Name.ToString().Trim();
                                            if (!string.IsNullOrWhiteSpace(name))
                                                rec.DeclaredNamespaces.Add(name);
                                        }
                                    }
                                    catch { /* tek dosya hatası önemli değil */ }
                                }
                            }
                        }
                        catch { /* sln parse hatası - geç */ }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }

            return projects.Values.ToList();
        }

        private static IEnumerable<string> EnumerateCSharpFiles(string csprojPath)
        {
            var dir = Path.GetDirectoryName(csprojPath);
            return Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
                            .Where(p => !p.Contains(@"\bin\") && !p.Contains(@"\obj\") && !p.Contains(@"\.git\"));
        }

        private static string ReadMsBuildProperty(string csprojPath, string prop)
        {
            try
            {
                var project = ProjectRootElement.Open(csprojPath);
                // İlk PropertyGroup → son değer kazanır
                foreach (var pg in project.PropertyGroups.Reverse())
                {
                    var v = pg.Properties.FirstOrDefault(p => string.Equals(p.Name, prop, StringComparison.OrdinalIgnoreCase))?.Value;
                    if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
                }
            }
            catch { }
            return null;
        }
    }
}
