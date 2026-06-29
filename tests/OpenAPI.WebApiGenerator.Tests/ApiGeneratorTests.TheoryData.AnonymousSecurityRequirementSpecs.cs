using Xunit;

namespace OpenAPI.WebApiGenerator.Tests;

public partial class ApiGeneratorTests
{
    public static TheoryData<string, string> AnonymousSecurityRequirementSpecs => new()
    {
        {
            "OpenAPI 3.1",
            """
            {
              "openapi": "3.1.0",
              "info": { "title": "foo", "version": "1.0" },
              "paths": {
                "/foo": {
                  "get": {
                    "operationId": "GetFoo",
                    "responses": {
                      "200": { "description": "Success" }
                    },
                    "security": [{}, { "secret_key": [] }]
                  }
                }
              },
              "components": {
                "securitySchemes": {
                  "secret_key": {
                    "type": "apiKey",
                    "in": "header",
                    "name": "X-API-Key"
                  }
                }
              }
            }
            """
        }
    };
}