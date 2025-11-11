using Example.Api;

var builder = WebApplication.CreateBuilder(args);
builder.AddOperations();
var app = builder.Build();
app.MapOperations();
app.Run();

public abstract partial class Program;