using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Northline.Api.Authorization;
using Northline.Api.Common;
using Northline.Api.Data;
using Northline.Api.Data.Seed;
using Northline.Api.Entities;
using Northline.Api.Services;
using Northline.Api.Validation;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------- Options ----------
builder.Services.AddOptions<DispatchOptions>().Bind(config.GetSection(DispatchOptions.SectionName)).ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<DispatchOptions>, DispatchOptionsValidator>();
builder.Services.AddOptions<JwtOptions>().Bind(config.GetSection(JwtOptions.SectionName))
    .Validate(o => o.SigningKey.Length >= 32, "Jwt:SigningKey must be configured and at least 32 characters long.")
    .ValidateOnStart();
builder.Services.Configure<SeedOptions>(config.GetSection(SeedOptions.SectionName));
builder.Services.Configure<DatabaseOptions>(config.GetSection(DatabaseOptions.SectionName));

// ---------- Data ----------
var connectionString = config.GetConnectionString("Northline");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:Northline is not configured.");
}
builder.Services.AddDbContext<NorthlineDbContext>(options => options
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention());

// ---------- Application services ----------
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<LicenceRules>();
builder.Services.AddSingleton<DispatchRuleEvaluator>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDispatchService, DispatchService>();
builder.Services.AddScoped<IAssignmentQueryService, AssignmentQueryService>();
builder.Services.AddScoped<IOrderQueryService, OrderQueryService>();
builder.Services.AddScoped<IResourceQueryService, ResourceQueryService>();
builder.Services.AddScoped<ISummaryService, SummaryService>();
builder.Services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

// ---------- Authentication & authorization ----------
var jwt = config.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        NameClaimType = Claims.Name,
        RoleClaimType = Claims.Role,
        ClockSkew = TimeSpan.FromSeconds(30),
    };
    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            await ProblemResponses.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized,
                "Unauthorized", "A valid bearer token is required. Sign in via POST /api/auth/login.");
        },
        OnForbidden = context => ProblemResponses.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden,
            ErrorCodes.Forbidden, "Forbidden", "Your role does not have permission to perform this action."),
    };
});

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Policies.DispatchRead, p => p.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Dispatcher)))
    .AddPolicy(Policies.DispatchWrite, p => p.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Dispatcher)))
    .AddPolicy(Policies.AdminOnly, p => p.RequireRole(nameof(UserRole.Admin)))
    .AddPolicy(Policies.DriverOnly, p => p.RequireRole(nameof(UserRole.Driver)));

// ---------- MVC ----------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = context =>
    {
        var violations = context.ModelState
            .Where(kv => kv.Value is { Errors.Count: > 0 })
            .SelectMany(kv => kv.Value!.Errors.Select(e => new RuleViolation("REQUEST", ErrorCodes.ValidationFailed,
                string.IsNullOrEmpty(kv.Key) ? e.ErrorMessage : $"{kv.Key.TrimStart('$', '.')}: {e.ErrorMessage}")))
            .ToList();
        var problem = ProblemResponses.Create(context.HttpContext, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed,
            "Request validation failed", "One or more request fields are missing or invalid. See 'violations'.", violations);
        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    });

builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Northline Freight TMS API",
        Version = "v1",
        Description = "Dispatch Management API for the Northline Freight & Logistics training TMS. " +
                      "Sign in with POST /api/auth/login, then click Authorize and paste the token.",
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT from POST /api/auth/login",
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml))
    {
        c.IncludeXmlComments(xml);
    }
});

var app = builder.Build();

// ---------- Database bootstrap / CLI ----------
// `dotnet run -- --reset-db` drops all data, re-seeds and exits (used by scripts/reset-db.*).
var resetOnly = args.Contains("--reset-db");
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
    var dbOptions = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    if (dbOptions.MigrateOnStartup || resetOnly)
    {
        await seeder.MigrateAsync(CancellationToken.None);
    }
    if (resetOnly)
    {
        var result = await seeder.ResetAsync(CancellationToken.None);
        Console.WriteLine($"Database reset: {result.Orders} orders, {result.Drivers} drivers, {result.Vehicles} vehicles, " +
                          $"{result.Assignments} assignments, {result.Users} users.");
        return;
    }
    if (dbOptions.SeedOnStartup)
    {
        await seeder.SeedIfEmptyAsync(CancellationToken.None);
    }
}

// ---------- Pipeline ----------
app.UseExceptionHandler();
app.UseStatusCodePages(async ctx =>
{
    // Unmatched routes etc. still get a problem response.
    var http = ctx.HttpContext;
    if (!http.Response.HasStarted && http.Response.ContentLength is null && string.IsNullOrEmpty(http.Response.ContentType))
    {
        var status = http.Response.StatusCode;
        await ProblemResponses.WriteAsync(http, status, status == 404 ? ErrorCodes.NotFound : $"HTTP_{status}",
            ReasonPhrases.GetReasonPhrase(status), $"The request to {http.Request.Path} could not be completed.");
    }
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Northline TMS API v1");
    c.DocumentTitle = "Northline TMS API";
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
