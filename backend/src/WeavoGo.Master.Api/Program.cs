using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using WeavoGo.Master.Api.Common;
using WeavoGo.Master.Api.Infrastructure;
using WeavoGo.Master.Api.Middleware;
using WeavoGo.Master.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- persistence
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserContextConnectionInterceptor>();

builder.Services.AddDbContext<ItemMasterDbContext>((sp, options) =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ItemMaster"),
        sql => sql.EnableRetryOnFailure(3));
    options.AddInterceptors(sp.GetRequiredService<UserContextConnectionInterceptor>());
});

// ------------------------------------------------------------------- services
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAttributeResolver, AttributeResolver>();
builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<IApprovalService, ApprovalService>();

// --------------------------------------------------------- auth (SDS §12.3)
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey))
    throw new InvalidOperationException("Jwt:SigningKey is not configured. Set it in appsettings or the environment.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// One policy per row of the §12.3 security matrix. Business-unit scoping is applied
// inside the services, which can see the item the action targets.
builder.Services.AddAuthorization(options =>
{
    void Policy(string name, params string[] roles) =>
        options.AddPolicy(name, p => p.RequireRole(roles.Append(RoleNames.SystemAdministrator).ToArray()));

    Policy(Policies.CreateDraftItem, RoleNames.CategoryManager);
    Policy(Policies.EditDraftItem, RoleNames.CategoryManager);
    Policy(Policies.SubmitForApproval, RoleNames.CategoryManager);
    Policy(Policies.ApproveStep, RoleNames.CategoryManager, RoleNames.FinanceController,
                                 RoleNames.QcManager, RoleNames.ItSecurityReview);
    Policy(Policies.ViewItem, RoleNames.CategoryManager, RoleNames.FinanceController, RoleNames.QcManager,
                              RoleNames.WarehouseClerk, RoleNames.SalesUser, RoleNames.ReadOnly);
    Policy(Policies.MarkObsolete, RoleNames.CategoryManager, RoleNames.FinanceController);
    Policy(Policies.ViewAuditAndVersions, RoleNames.CategoryManager, RoleNames.FinanceController, RoleNames.QcManager);
    Policy(Policies.ManageBusinessUnitMapping, RoleNames.CategoryManager);
});

// ---------------------------------------------------------------- mvc + docs
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WeavoGo Universal Item Master API",
        Version = "v1",
        Description = "Implements Chapter 10 of the Universal Item Master SDS."
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token returned by POST /api/v1/auth/login."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    var xml = Path.Combine(AppContext.BaseDirectory,
        $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

builder.Services.AddCors(options => options.AddPolicy("angular", p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:4200" })
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Item Master API v1"));
}

app.UseCors("angular");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow })).AllowAnonymous();

app.Run();

/// <summary>Exposed so the integration test project can host the API in-process.</summary>
//public partial class Program { }
