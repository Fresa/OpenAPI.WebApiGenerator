using OpenAPI.WebApiGenerator.Json;

namespace OpenAPI.WebApiGenerator.OpenApi.Visitor;

internal interface IVisitor
{
    internal JsonPointer Pointer { get; }
}