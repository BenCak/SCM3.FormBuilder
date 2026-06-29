using SCM3.FormBuilder.Data;
using Microsoft.AspNetCore.Hosting;
using SCM3.FormBuilder.UI.Designer;
using SCM3.FormBuilder.Web;

var builder = WebApplication.CreateBuilder(args);

// Explicitly set to Development via environment variable
builder.Services.Configure<HostOptions>(options =>
{
    options.ServicesStartConcurrently = false;
});

builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(options =>
{
    options.DetailedErrors = true;
    options.DisconnectedCircuitMaxRetained = 100;
});
builder.Services.AddTelerikBlazor();

// Local-disk JSON storage
var dataRoot = Path.Combine(builder.Environment.ContentRootPath, "FormBuilderData");
builder.Services.AddSingleton<IFormRepository>(new JsonFileFormRepository(dataRoot));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapRazorPages();
app.MapFallbackToPage("/_Host");

app.Run();
