using Asp.Versioning;
using Asp.Versioning.Builder;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1);
    options.ReportApiVersions = true;
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi("v1");
builder.Services.AddOpenApi("v2");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    string[] versions = ["v1", "v2"];
    
    app.MapScalarApiReference(
        endpointPrefix: "/docs",
        configureOptions: options => {
            options
                .AddDocuments(versions)
                .WithTitle("This is my Awesome API")
                .WithTheme(ScalarTheme.DeepSpace)
                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                .AddHeaderContent(
                    "<header class=\"custom-header scalar-app\">" + 
                    "<span>My logo here</span>" +
                    "</header>"
                )
                .WithCustomCss($":root {{\n    --scalar-custom-header-height: 50px;\n}}\n.custom-header {{\n    height: var(--scalar-custom-header-height);\n    background-color: var(--scalar-background-1);\n    box-shadow: inset 0 -1px 0 var(--scalar-border-color);\n    color: var(--scalar-color-1);\n    font-size: var(--scalar-font-size-2);\n    padding: 0 18px;\n    position: sticky;\n    justify-content: space-between;\n    top: 0;\n    z-index: 100;\n}}\n.custom-header,\n.custom-header nav {{\n    display: flex;\n    align-items: center;\n    gap: 18px;\n}}\n.custom-header a:hover {{\n    color: var(--scalar-color-2);\n}}");

        });
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

ApiVersionSet versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1))
    .HasApiVersion(new ApiVersion(2))
    .ReportApiVersions()
    .Build();

RouteGroupBuilder group = app.MapGroup("v{version:ApiVersion}")
    .WithApiVersionSet(versionSet);

group.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast")
    .MapToApiVersion(1);

group.MapGet("/weatherforecastv2", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast2")
    .MapToApiVersion(2);

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}