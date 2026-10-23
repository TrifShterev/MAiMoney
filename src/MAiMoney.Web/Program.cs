using System.Text.Json.Serialization;
using MAiMoney.Services;
using MAiMoney.Services.Data;
using MAiMoney.Web.Auth;
using MAiMoney.Web.Components;
using MAiMoney.Web.Infrastructure;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers(options => options.Filters.Add<ApiExceptionFilter>())
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddMAiMoneyServices(builder.Configuration);
builder.Services.AddMAiMoneyAuthentication();
builder.Services.AddSingleton<MoneyFormatter>();

// In the container the keys live on a volume, so sign-in cookies survive restarts.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrEmpty(keysPath))
{
    builder.Services.AddDataProtection()
        .SetApplicationName("MAiMoney")
        .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

var app = builder.Build();

await app.Services.GetRequiredService<MongoDbContext>().EnsureIndexesAsync();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// API callers get plain status codes; only pages get the friendly "not found" page.
app.UseWhen(
    context => !context.Request.IsApiRequest(),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
