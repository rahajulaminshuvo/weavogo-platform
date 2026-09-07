using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using ItemMaster.Api.Controllers;
using ItemMaster.Api.Middleware;
using ItemMaster.Api.Security;
using ItemMaster.Application.Abstractions;
using ItemMaster.Application.Items.Commands;
using ItemMaster.Infrastructure;
using ItemMaster.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Weavo.BuildingBlocks.Application.Behaviors;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Authentication (JWT Bearer)
// ---------------------------------------------------------------------------
var jwtSettings = builder.Configuration.GetSection("JwtSettings");

// The signing key is a credential. A hardcoded fallback that ships to
// production lets anyone who has read the source mint valid tokens, so outside
// Development a missing key fails start-up instead.
var configuredSecret = jwtSettings["Secret"];

if (string.IsNullOrWhiteSpace(configuredSecret))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "JwtSettings:Secret is not configured. Supply it through user-secrets, "
            + "an environment variable, or the platform's secret store.");
    }

    configuredSecret = "SuperSecretKeyForDevelopmentPhase12345!";
}

var secretKey = Encoding.UTF8.GetBytes(configuredSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Metadata over plain HTTP is only tolerable on a developer machine.
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"] ?? "ItemMaster.Api",
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"] ?? "ItemMaster.Clients",
        ValidateLifetime = true,
        // No grace period on expiry. Tightens the replay window for a stolen
        // token at the cost of requiring synchronised clocks.
        ClockSkew = TimeSpan.Zero,
    };
});

// ---------------------------------------------------------------------------
// 2. Authorization -- authenticated by default, permissions per endpoint
// ---------------------------------------------------------------------------
// The fallback policy makes every endpoint require authentication unless it
// opts out with [AllowAnonymous]. Failing closed means a newly added endpoint
// is protected by default rather than exposed by omission.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy(
        AuthorizationPolicies.CanManageItems,
        policy => policy.RequireClaim("permission", "item_master:write"))
    .AddPolicy(
        AuthorizationPolicies.CanReadItems,
        policy => policy.RequireClaim("permission", "item_master:read"));

// ---------------------------------------------------------------------------
// 3. Error handling and MVC
// ---------------------------------------------------------------------------
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

// Required by HttpContextCurrentUserProvider to read the caller's claims.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserProvider, HttpContextCurrentUserProvider>();

// ---------------------------------------------------------------------------
// 4. CQRS -- MediatR and the shared pipeline behaviours
// ---------------------------------------------------------------------------
// Handlers live in the Application assembly, located via a known type rather
// than a magic string, so a rename cannot silently unregister them. Scanning
// the API assembly instead would find none of them.
var applicationAssembly = typeof(CreateItemCommand).Assembly;

builder.Services.AddMediatR(configuration =>
{
    configuration.RegisterServicesFromAssembly(applicationAssembly);

    // Order is significant. Logging sits outermost so its measured duration
    // covers validation too; validation runs before the handler, so a rejected
    // request never reaches business logic or opens a transaction.
    configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
    configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

// Validators live beside the commands they guard, in the Application assembly.
// Scanning the API assembly instead would find none of them.
builder.Services.AddValidatorsFromAssembly(applicationAssembly);

// ---------------------------------------------------------------------------
// 5. Infrastructure -- EF Core, repositories and the Outbox dispatcher
// ---------------------------------------------------------------------------
builder.Services.AddItemMasterInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ItemMasterDbContext>(name: "itemmaster-db", tags: ["ready"]);

// ---------------------------------------------------------------------------
// 6. OpenAPI
// ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WeavoGo ItemMaster API",
        Version = "v1",
        Description = "Master data service for catalogue items.",
    });

    // Lets the Swagger UI send a bearer token, which every endpoint now needs.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the JWT access token (without the 'Bearer ' prefix).",
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Database migration and seeding
// ---------------------------------------------------------------------------
// Runs before the pipeline starts serving, so no request can arrive against an
// unmigrated schema. A scope is required: the seeder and its DbContext are
// scoped, and app.Services is the root provider.
//
// Development only. In production, run migrations as a separate deployment step
// -- several replicas booting at once would otherwise race to migrate the same
// database, and a schema change would be triggered by a pod restart rather than
// by an intentional release.
if (app.Environment.IsDevelopment())
{
    await using (var scope = app.Services.CreateAsyncScope())
    {
        var seeder = scope.ServiceProvider
            .GetRequiredService<ItemMasterDbContextSeeder>();

        await seeder.SeedAsync().ConfigureAwait(false);
    }
}

// ---------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ItemMaster API v1");
        options.DocumentTitle = "WeavoGo ItemMaster API";
    });
}








app.UseHttpsRedirection();

// Authentication must precede authorization: the second decides using the
// principal the first established.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Liveness: the process is up. Readiness: its dependencies answer too.
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks
    .HealthCheckOptions
{
    Predicate = _ => false,
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks
    .HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
}).AllowAnonymous();

// ---------------------------------------------------------------------------
// Development-only token endpoint
// ---------------------------------------------------------------------------
// Every endpoint requires a JWT, and no Identity service exists yet, so without
// this there is no way to exercise the API from Swagger. Guarded by
// IsDevelopment(): the route is never mapped in any other environment, so it
// cannot be reached even if the assembly is deployed. Delete it once
// PlatformServices/Identity issues real tokens.
if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/v1/auth/dev-token", () =>
    {
        var claims = new[]
        {
            // NameIdentifier, not "sub": Microsoft's JWT handler rewrites "sub"
            // to the NameIdentifier claim type during validation, so a handler
            // reading it back finds NameIdentifier either way.
            // HttpContextCurrentUserProvider parses this as the acting user id.
            new Claim(ClaimTypes.NameIdentifier, "1001"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("permission", "item_master:read"),
            new Claim("permission", "item_master:write"),
        };

        // Reuses the exact key, issuer and audience the validator was
        // configured with above. Re-reading configuration here would let the
        // minted token and the validation parameters drift apart.
        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"] ?? "ItemMaster.Api",
            audience: jwtSettings["Audience"] ?? "ItemMaster.Clients",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(secretKey),
                SecurityAlgorithms.HmacSha256));

        return Results.Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
        });
    })
    .AllowAnonymous()
    .WithTags("Development Authentication")
    .WithSummary("Mints a JWT with full item-master permissions for local testing.")
    .WithDescription(
        "Development only. Copy the token into Swagger's Authorize dialog.");
}

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Exposed so integration tests can drive this host through
/// <c>WebApplicationFactory&lt;Program&gt;</c>. A top-level-statement Program is
/// internal by default, which the factory cannot reach.
/// </summary>
public partial class Program;
