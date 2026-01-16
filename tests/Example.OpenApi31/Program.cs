using Example.OpenApi31;

var builder = WebApplication.CreateBuilder(args);
builder.AddOperations(builder.Configuration.Get<WebApiConfiguration>());
var app = builder.Build();
app.MapOperations();
app.Run();

public abstract partial class Program;