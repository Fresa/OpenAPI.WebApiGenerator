using Xunit;

namespace OpenAPI.WebApiGenerator.Tests;

public partial class ApiGeneratorTests
{
    public static TheoryData<string, string> OpenApiSpecsWithOperations => new()
    {
        {
            "Swagger 2.0",
            """
            {
              "swagger": "2.0",
              "info": { "title": "foo", "version": "1.0" },
              "paths": {
                "/foo": {
                  "put": {
                    "operationId": "Service_SetProperties",
                    "parameters": [
                      {
                        "name": "body",
                        "in": "body",
                        "required": true,
                        "schema": {
                          "type": "object",
                          "properties": {
                            "HourMetrics": { "type": "string" }
                          }
                        }
                      }
                    ],
                    "responses": {
                      "202": { "description": "Success (Accepted)" }
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
                  "put": {
                    "operationId": "Service_SetProperties",
                    "requestBody": {
                      "required": true,
                      "content": {
                        "application/json": {
                          "schema": {
                            "type": "object",
                            "properties": {
                              "HourMetrics": { "type": "string" }
                            }
                          }
                        }
                      }
                    },
                    "responses": {
                      "202": { "description": "Success (Accepted)" }
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
                  "put": {
                    "operationId": "Service_SetProperties",
                    "requestBody": {
                      "required": true,
                      "content": {
                        "application/json": {
                          "schema": {
                            "type": "object",
                            "properties": {
                              "HourMetrics": { "type": "string" }
                            }
                          }
                        }
                      }
                    },
                    "responses": {
                      "202": { "description": "Success (Accepted)" }
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
                  "put": {
                    "operationId": "Service_SetProperties",
                    "requestBody": {
                      "required": true,
                      "content": {
                        "application/json": {
                          "schema": {
                            "type": "object",
                            "properties": {
                              "HourMetrics": { "type": "string" }
                            }
                          }
                        }
                      }
                    },
                    "responses": {
                      "202": { "description": "Success (Accepted)" }
                    }
                  }
                }
              }
            }
            """
        }
    };
}