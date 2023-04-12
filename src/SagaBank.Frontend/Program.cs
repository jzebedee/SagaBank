var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

{
    builder.Services.AddHttpClient<BackendClient>(client =>
    {
        client.BaseAddress = builder.Configuration.GetServiceUri("sagabank-backend");
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();

public class BackendClient
{
    private readonly HttpClient _client;

    public BackendClient(HttpClient client)
    {
        _client = client;
    }

    public async Task<string> GetIndex() => await _client.GetStringAsync("/");
}