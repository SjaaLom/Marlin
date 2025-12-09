using Microsoft.AspNetCore.ResponseCompression;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddApplicationPart(typeof(Configurator.Server.Controllers.SchemaController).Assembly);
builder.Services.AddRazorPages();
// builder.Services.AddOpenApi();

builder.Services.AddScoped<Configurator.Server.Services.ISchemaService, Configurator.Server.Services.SchemaService>();
builder.Services.AddScoped<Configurator.Server.Services.IConfigurationService, Configurator.Server.Services.ConfigurationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    // app.MapOpenApi();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();
app.MapControllers();
app.MapGet("/api/test", () => "API is working");
app.MapFallbackToFile("index.html");

app.Run();
