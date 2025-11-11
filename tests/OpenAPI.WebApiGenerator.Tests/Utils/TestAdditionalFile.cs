using System.IO;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace OpenAPI.WebApiGenerator.Tests.Utils;

public class TestAdditionalFile(string path) : AdditionalText
{
    public override SourceText GetText(CancellationToken cancellationToken = new()) => SourceText.From(File.OpenRead(Path));

    public override string Path { get; } = path;
}