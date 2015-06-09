namespace SurfaceAreaExposure
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.MSBuild;

    public sealed class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: SurfaceAreaExposure [CSProjLocation] [Regex]");
                return;
            }

            FindMatchingSymbols(args[0], args[1], Predicate).Wait();
        }

        private static bool Predicate(ISymbol symbol, string pattern)
        {
            Regex r = new Regex(pattern);
            return (symbol is IPropertySymbol || symbol is IMethodSymbol || symbol is IFieldSymbol) && r.IsMatch(symbol.ToDisplayString());
        }

        public static async Task FindMatchingSymbols(string projectPath, string pattern, Func<ISymbol, string, bool> predicate)
        {
            var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(projectPath);
            var compilation = await project.GetCompilationAsync();

            var trees = compilation.SyntaxTrees;
            foreach (var tree in trees)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                var nodes = semanticModel.SyntaxTree.GetRoot().DescendantNodes();
                foreach (var node in nodes)
                {
                    ISymbol symbol = semanticModel.GetSymbolInfo(node).Symbol;
                    if (predicate(symbol, pattern))
                    {
                        var linespan = node.GetLocation().GetLineSpan();
                        Console.WriteLine(linespan.Path + ":" + linespan.StartLinePosition +": " + node.ToFullString());
                    }
                }
            }
        }

        public static async Task<ImmutableHashSet<ISymbol>> FindMatchingSymbols<T>(string projectPath, Func<ISymbol, bool> predicate) where T : SyntaxNode
        {
            var workspace = MSBuildWorkspace.Create();
            var project = await workspace.OpenProjectAsync(projectPath);
            var compilation = await project.GetCompilationAsync();

            return compilation.SyntaxTrees.Select(syntaxTree => compilation.GetSemanticModel(syntaxTree))
                .SelectMany(semanticModel => semanticModel
                    .SyntaxTree
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<T>()
                    .Select(t => semanticModel.GetSymbolInfo(t).Symbol))
                .Where(predicate)
                .ToImmutableHashSet();
        }
    }
}