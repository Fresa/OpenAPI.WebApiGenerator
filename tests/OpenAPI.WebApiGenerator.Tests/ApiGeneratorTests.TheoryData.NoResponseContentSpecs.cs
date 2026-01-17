using Xunit;

namespace OpenAPI.WebApiGenerator.Tests;

public partial class ApiGeneratorTests
{
    public static TheoryData<string, string> NoResponseContentSpecs => new()
    {
        {
            "Swagger 2.0",
            """
            {
              "swagger": "2.0",
              "info": { "title": "foo", "version": "1.0" },
              "paths": {
                "/foo": {
                  "delete": {
                    "operationId": "Delete",
                    "responses": {
                      "202": { "description": "Success" }
                    }
                  }
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
                  "delete": {
                    "operationId": "Delete",
                    "responses": {
                      "202": { "description": "Success" }
                    }
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
                  "delete": {
                    "operationId": "Delete",
                    "responses": {
                      "202": { "description": "Success" }
                    }
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
                  "delete": {
                    "operationId": "Delete",
                    "responses": {
                      "202": { "description": "Success" }
                    }
                  }
                }
              }
            }
            """
        }
    };
}