using Xunit;

namespace OpenAPI.WebApiGenerator.Tests;

public partial class ApiGeneratorTests
{
    public static TheoryData<string, string> ResponseContentMediaTypeSpecs => new()
    {
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
                      "200": {
                        "description": "Success",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "name": { "type": "string" } } }
                          },
                          "application/xml": {
                            "schema": { "type": "object", "properties": { "name": { "type": "string" } } }
                          },
                          "text/*": {
                            "schema": { "type": "string" }
                          },
                          "text/plain; charset=utf-8": {
                            "schema": { "type": "string" }
                          },
                          "*/*": {
                            "schema": { "type": "string" }
                          }
                        }
                      }
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
                  "get": {
                    "operationId": "GetFoo",
                    "responses": {
                      "200": {
                        "description": "Success",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "name": { "type": "string" } } }
                          },
                          "application/xml": {
                            "schema": { "type": "object", "properties": { "name": { "type": "string" } } }
                          },
                          "text/*": {
                            "schema": { "type": "string" }
                          },
                          "text/plain; charset=utf-8": {
                            "schema": { "type": "string" }
                          },
                          "*/*": {
                            "schema": { "type": "string" }
                          }
                        }
                      }
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
                  "get": {
                    "operationId": "GetFoo",
                    "responses": {
                      "200": {
                        "description": "Success",
                        "content": {
                          "application/json": {
                            "schema": { "type": "object", "properties": { "name": { "type": "string" } } }
                          },
                          "application/xml": {
                            "schema": { "type": "object", "properties": { "name": { "type": "string" } } }
                          },
                          "text/*": {
                            "schema": { "type": "string" }
                          },
                          "text/plain; charset=utf-8": {
                            "schema": { "type": "string" }
                          },
                          "*/*": {
                            "schema": { "type": "string" }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """
        }
    };
}