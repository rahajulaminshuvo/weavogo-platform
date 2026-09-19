using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PlatformServices.Identity.Api.Endpoints;
using PlatformServices.Identity.Application.Features.Authentication.GenerateToken;
using PlatformServices.Identity.Infrastructure;
using PlatformServices.Identity.Infrastructure.Persistence;
using PlatformServices.Identity.Infrastructure.Security;
using Weavo.BuildingBlocks.Application.Behaviors;
using Weavo.BuildingBlocks.Messaging;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Configuration validation - fail fast
// ---------------------------------------------------------------------------
// TODO(Phase 2): the symmetric secret disappears when credential verification
// federates to Entra ID per ADR-007; only JwtTokenGenerator changes.
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException(
        $"Missing configuration section '{JwtSettings.SectionName}'.");

// HS256 needs at least 256 bits of key. A shorter secret throws deep inside the
// token handler on first issuance; checking here fails the deployment instead.
if (string.IsNullOrWhiteSpace(jwtSettings.Secret)
    || Encoding.UTF8.GetByteCount(jwtSettings.Secret) < 32)
{
    throw new InvalidOperationException(
        "JwtSettings:Secret must be at least 32 UTF-8 bytes for HS256. "
        + "Supply it via user-secrets or the platform vault, never appsettings.json.");
}

// ---------------------------------------------------------------------------
// 2. CQRS - MediatR with the shared pipeline behaviours
// ---------------------------------------------------------------------------
// Handlers live in the Application assembly, located by a known type so a
// rename cannot silently unregister them.
var applicationAssembly = typeof(GenerateTokenCommand).Assembly;

builder.Services.AddMediatR(configuration =>
{
    configuration.RegisterServicesFromAssembly(applicationAssembly);

    // Order matters: logging outermost so its timing covers validation;
    // validation before the handler so a rejected request never reaches it.
    configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
    configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

builder.Services.AddValidatorsFromAssembly(applicationAssembly);

// ---------------------------------------------------------------------------
// 3. Infrastructure and messaging
// ---------------------------------------------------------------------------
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddWeavoMessaging(builder.Configuration, applicationAssembly);

// Needed by the tenant-context resolution path and future gRPC token propagation.
builder.Services.AddHttpContextAccessor();

// ---------------------------------------------------------------------------
// 4. Authentication - Identity validates the tokens it issues
// ---------------------------------------------------------------------------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.Secret)),

            // No grace period on expiry: tightens the replay window for a stolen
            // token, at the cost of requiring synchronised clocks.
            ClockSkew = TimeSpan.Zero,
        };
    });

// Fail closed: every endpoint requires authentication unless it opts out, so a
// newly added route is protected by omission rather than exposed by it.
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// ---------------------------------------------------------------------------
// 5. Cross-cutting
// ---------------------------------------------------------------------------
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdentityDbContext>(name: "identity-db", tags: ["ready"]);

// TODO(Observability): register OpenTelemetry + Serilog once
// Weavo.BuildingBlocks.Observability lands. Do not invent a per-service setup.

// ---------------------------------------------------------------------------
// 6. OpenAPI
// ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WeavoGo Identity",
        Version = "v1",
        Description = "Platform identity service: JWT issuance and user management.",
    });

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
// Pipeline
// ---------------------------------------------------------------------------
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "WeavoGo Identity v1");
        options.DocumentTitle = "WeavoGo Identity";
    });
}

app.UseHttpsRedirection();

// Authentication precedes authorization: the second decides using the principal
// the first established.
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();

// Liveness answers "is the process up"; readiness answers "can it serve".
// Probing dependencies on liveness would restart a healthy pod during a
// transient database blip.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
}).AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
}).AllowAnonymous();

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Exposed so integration tests can drive this host through
/// <c>WebApplicationFactory&lt;Program&gt;</c>.
/// </summary>
public partial class Program;
