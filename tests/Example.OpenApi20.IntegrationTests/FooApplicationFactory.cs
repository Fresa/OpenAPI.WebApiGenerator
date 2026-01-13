using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Example.Api.IntegrationTests;

[UsedImplicitly]
public class FooApplicationFactory : WebApplicationFactory<Program>;