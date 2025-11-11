using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;

namespace OpenAPI.WebApiGenerator.Extensions;

internal static class AdditionalTextExtensions
{
    internal static MemoryStream AsStream(this AdditionalText text)
    {
        var content = text.GetText();
        var stream = new MemoryStream();
        if (content is null)
        {
            return stream;
        }

        using (var writer = new StreamWriter(stream, Encoding.UTF8, 1024, true))
        {
            content.Write(writer);    
        }
        
        stream.Position = 0;
        return stream;
    }
}