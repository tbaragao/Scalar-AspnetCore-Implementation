using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"])),
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"]
        };
    });

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
string[] apiVersions = ["v1", "v2"];

foreach (var apiVersion in apiVersions)
{
    builder.Services.AddOpenApi(apiVersion, options =>
    {
        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    });
}


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    
    app.MapScalarApiReference(
        endpointPrefix: "/docs",
        configureOptions: options => {
            options
                .AddDocuments(apiVersions.Select(apiversion => new ScalarDocument(apiversion, $"Api {apiversion}")))
                .WithTitle("This is my Awesome API")
                .WithClientButton(false)
                .WithDownloadButton(false)
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

app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();

ApiVersionSet versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1))
    .HasApiVersion(new ApiVersion(2))
    .ReportApiVersions()
    .Build();

RouteGroupBuilder group = app.MapGroup("v{version:ApiVersion}")
    .WithApiVersionSet(versionSet);

group.MapGet("/weatherforecast", () =>
    {
        return "V1 endpoint result";
    })
    .WithName("GetWeatherForecast")
    .MapToApiVersion(1);

group.MapGet("/weatherforecast", () =>
    {
        return "V2 endpoint result";
    })
    .WithName("GetWeatherForecast2")
    .RequireAuthorization()
    .MapToApiVersion(2);

group.MapPost(
    pattern: "/login",
    handler: ([FromBody]User user, IConfiguration configuration) =>
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email)
            ]),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = credentials,
            Issuer = configuration["Jwt:Issuer"],
            Audience = configuration["Jwt:Audience"],
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var encryptedToken = tokenHandler.WriteToken(token);

        return new { access_token = encryptedToken };
    }
).MapToApiVersion(1);

app.Run();

public sealed record User(string Email, string Password);

internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private readonly IAuthenticationSchemeProvider _authenticationSchemeProvider;
    
    public BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider)
    {
        _authenticationSchemeProvider = authenticationSchemeProvider;
    }
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var authenticationSchemes = await _authenticationSchemeProvider.GetAllSchemesAsync();

        var securitySchemes = new Dictionary<string, OpenApiSecurityScheme>();

        if (authenticationSchemes.Any(authScheme => authScheme.Name == "Bearer"))
        {
            securitySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                In = ParameterLocation.Header,
                BearerFormat = "JWT",
                Description = "Autenticação via Bearer Token"
            };
        }

        if (authenticationSchemes.Any(authScheme => authScheme.Name == "api_key"))
        {
            securitySchemes["api_key"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                Name = "X-Api-Key",
                In = ParameterLocation.Header,
                Description = "Chave de API necessária para autenticação"
            };
        }

        if (securitySchemes.Count > 0)
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes = securitySchemes;

            foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations))
            {
                var securityRequirement = new OpenApiSecurityRequirement();
                
                foreach (var scheme in securitySchemes.Keys)
                {
                    securityRequirement[new OpenApiSecurityScheme { Reference = new OpenApiReference { Id = scheme, Type = ReferenceType.SecurityScheme } }] = Array.Empty<string>();
                }
                operation.Value.Security.Add(securityRequirement);
            }
        }
    }
}