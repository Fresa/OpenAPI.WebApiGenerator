using Xunit;

namespace OpenAPI.WebApiGenerator.Tests;

public partial class ApiGeneratorTests
{
    public static TheoryData<string, string> ApiKeySecuritySchemeWithMultipleParameterTypesSpecs => new()
    {
        {
            "OpenAPI 3.1 - Multiple operations with different parameter types",
            """
            {
              "openapi": "3.1.0",
              "info": { "title": "foo", "version": "1.0" },
              "paths": {
                "/foo": {
                  "get": {
                    "operationId": "GetFoo",
                    "parameters": [
                      {
                        "name": "X-API-Key",
                        "in": "header",
                        "schema": { "type": "string" },
                        "required": true
                      }
                    ],
                    "responses": {
                      "200": { "description": "Success" }
                    },
                    "security": [{ "secret_key": [] }]
                  },
                  "post": {
                    "operationId": "CreateFoo",
                    "parameters": [
                      {
                        "name": "X-API-Key",
                        "in": "header",
                        "schema": { "type": "integer" },
                        "required": true
                      }
                    ],
                    "responses": {
                      "201": { "description": "Created" }
                    },
                    "security": [{ "secret_key": [] }]
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
