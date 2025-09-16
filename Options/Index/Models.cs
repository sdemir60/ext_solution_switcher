using System;
using System.Collections.Generic;

namespace SolutionSwitcher.Index
{
    public sealed class NamespaceHit
    {
        public string Namespace { get; set; } = "";
        public string ProjectPath { get; set; } = "";
        public HashSet<string> SolutionPaths { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class ProjectRecord
    {
        public string ProjectPath { get; set; } = "";
        public string AssemblyName { get; set; } = "";
        public string RootNamespace { get; set; } = "";
        public HashSet<string> DeclaredNamespaces { get; set; } = new(StringComparer.Ordinal);
        public HashSet<string> SolutionPaths { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class IndexSnapshot
    {
        // Namespace → list of projects containing it
        public Dictionary<string, List<ProjectRecord>> NamespaceMap { get; set; } = new(StringComparer.Ordinal);
        public DateTime BuiltAtUtc { get; set; }
    }
}