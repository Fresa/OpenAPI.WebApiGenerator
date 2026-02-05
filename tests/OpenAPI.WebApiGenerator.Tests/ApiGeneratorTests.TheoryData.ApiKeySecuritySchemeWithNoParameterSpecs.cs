using Xunit;

namespace OpenAPI.WebApiGenerator.Tests;

public partial class ApiGeneratorTests
{
    public static TheoryData<string, string> ApiKeySecuritySchemeWithNoParameterSpecs => new()
    {
        {
            "Swagger 2.0",
            """
            {
              "swagger": "2.0",
              "info": { "title": "foo", "version": "1.0" },
              "paths": {
                "/foo": {
                  "get": {
                    "operationId": "GetFoo",
                    "responses": {
                      "200": { "description": "Success" }
                    },
                    "security": [{ "secret_key": [] }]
                  }
                }
              },
              "securityDefinitions": {
                "secret_key": {
                  "type": "apiKey",
                  "in": "header",
                  "name": "X-API-Key"
                }
              }
            }
            """
        },
        {
            "OpenAPI 3.0",
            """
            {
              "openapi": "3.0.3",
              "info": { "title": "foo", "version": "1.0" },
              "paths": {
                "/foo": {
                  "get": {
                    "operationId": "GetFoo",
                    "responses": {
                      "200": { "description": "Success" }
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
        },
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
        },
        {
            "OpenAPI 3.2",
            """
            {
              "openapi": "3.2.0",
              "info": { "title": "foo", "version": "1.0" },
              "paths": {
                "/foo": {
                  "get": {
                    "operationId": "GetFoo",
                    "responses": {
                      "200": { "description": "Success" }
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
