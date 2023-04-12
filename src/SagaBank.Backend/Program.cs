var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => $"Hello World! It's {DateTimeOffset.Now}");

app.Run();
