using SCM3.FormBuilder.Data;
using Microsoft.AspNetCore.Hosting;
using Telerik.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Enable development mode
builder.Environment.EnvironmentName = Environments.Development;

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddTelerikBlazor();

// Local-disk JSON storage. Folder lives next to the running exe -
// zip up FormBuilderData/ if you want to back it up or hand it off.
var dataRoot = Path.Combine(builder.Environment.ContentRootPath, "FormBuilderData");
builder.Services.AddSingleton<IFormRepository>(new JsonFileFormRepository(dataRoot));

var app = builder.Build();

// Always use static files middleware to serve Blazor framework and other assets
app.UseStaticFiles();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
