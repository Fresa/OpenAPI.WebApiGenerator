using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace OpenAPI.WebApiGenerator;

internal sealed class SourceCode(string fileName, string code)
{
    // Rider (and possibly other IDEs) aren't dealing with directories properly,
    // where source generated code is not displayed or has file hint names truncated,
    // so we deal with this by normalizing file names to not have any directory hierarchy
    // Example:
    // this/is/a/deep/hierarchy/file.cs
    // becomes:
    // this.is.a.deep.hierarchy.file.cs
    // https://youtrack.jetbrains.com/issue/RIDER-130837
    private readonly string _fileName = fileName
        .Replace('/', '.')
        .Replace('\\', '.');

    internal void AddTo(SourceProductionContext context)
    {
        context.AddSource(_fileName, ParseCSharpCode(code));
    }
    
    private static SourceText ParseCSharpCode(string code, bool normalize = true)
    {
        var compilationUnit = SyntaxFactory
            .ParseCompilationUnit(code, options: new CSharpParseOptions());
        if (normalize)
        {
            compilationUnit = compilationUnit.NormalizeWhitespace();
        }
        return compilationUnit.WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)
            .GetText(Encoding.UTF8);
    }
}