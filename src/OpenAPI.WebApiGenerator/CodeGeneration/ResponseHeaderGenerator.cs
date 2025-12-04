using Corvus.Json.CodeGeneration;
using Microsoft.OpenApi;

namespace OpenAPI.WebApiGenerator.CodeGeneration;

internal sealed class ResponseHeaderGenerator(IOpenApiHeader header, TypeDeclaration typeDeclaration)
{
    private readonly IOpenApiHeader _header = header;
    private readonly TypeDeclaration _typeDeclaration = typeDeclaration;
}