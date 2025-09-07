using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using SummarizerApi.Repositories;
using SummarizerApi.Services;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

var modeValue = builder.Configuration["Authentication:Mode"];
if (!Enum.TryParse(modeValue, ignoreCase: true, out AuthenticationMode authMode))
{
    authMode = AuthenticationMode.Production;
}

// AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
// sets the expectation that incoming requests will include a bearer JWT in the Authorization HTTP header.

if (authMode == AuthenticationMode.Development)
{
    var secret = builder.Configuration["Authentication:Jwt:Secret"]; // From secrets.json, not appsettings.development.json
    var issuer = builder.Configuration["Authentication:Jwt:Issuer"];
    var audience = builder.Configuration["Authentication:Jwt:Audience"];

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = false, // Don't insist on unexpired tokens during development.
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
            };

            options.Events = new JwtBearerEvents
            {
                // When this event is fired, the supplied JWT has been parsed and a 
                // ClaimsPrincipal (with claims populated) is assigned to TokenValidatedcontext.Principal.

                // ClaimsPrincipal represents the entire user context, with potentially 
                // multiple authN contexts, represented by multiple ClaimsIdentity.
                // ClaimsPrincipal (1) --> ClaimsIdentity (0..n)

                // Parse any "act" claim (c.f. actor delegation) and assign to 
                // HttpContext.User.Identity.Actor
                OnTokenValidated = context =>
                {
                    var principal = context.Principal;
                    var actClaim = principal.FindFirst("act");

                    if (actClaim != null)
                    {
                        var actorClaims = JsonSerializer.Deserialize<Dictionary<string, object>>(actClaim.Value);
                        var actorIdentity = new ClaimsIdentity("Actor");

                        foreach (var kvp in actorClaims)
                        {
                            if (kvp.Value is JsonElement element && element.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in element.EnumerateArray())
                                {
                                    actorIdentity.AddClaim(new Claim(kvp.Key, item.GetString()));
                                }
                            }
                            else
                            {
                                actorIdentity.AddClaim(new Claim(kvp.Key, kvp.Value.ToString()));
                            }
                        }

                        ((ClaimsIdentity)principal.Identity).Actor = actorIdentity;
                    }

                    return Task.CompletedTask;
                }
            };
        });
}
else
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
}

builder.Services.AddAuthorization();
builder.Services.AddSingleton<IMongoClient>(new MongoClient("mongodb://localhost:27017"));
builder.Services.AddScoped<IChunkRepository, MongoChunkRepository>();
builder.Services.AddSingleton<ILLMService, OllamaLLMService>();
builder.Services.AddSingleton<IVectorMathService, VectorMathService>();
builder.Services.AddSingleton<ISemanticSearchService, SemanticSearchService>();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication(); // Validates credentials and sets HttpContext.User
app.UseAuthorization(); // Evaluates policies/roles based on HttpContext.User
app.MapControllers();
app.Run();

public enum AuthenticationMode
{
    Production,
    Development,
    Test
}