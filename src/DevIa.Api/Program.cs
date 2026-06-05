using System.Text;
using DevIa.Api.Auth;
using DevIa.Api.Repositories;
using DevIa.Api.Reviews;
using DevIa.Api.Webhooks;
using DevIa.Application.Identity;
using DevIa.Application.PullRequests;
using DevIa.Application.Reviews;
using DevIa.Domain.Enums;
using DevIa.Infrastructure;
using DevIa.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<PullRequestWebhookHandler>();
builder.Services.AddScoped<RecordVerdict>();
builder.Services.AddScoped<AuthenticateWithGitHub>();
builder.Services.AddScoped<ProcessInstallation>();
builder.Services.AddScoped<SetRepositoryActive>();

// Serialize enums as strings across the API (status, severity, etc.).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

var authOptions = builder.Configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = authOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = authOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.SigningKey)),
            ValidateLifetime = true,
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization(options =>
    options.AddPolicy("CanReview", policy =>
        policy.RequireRole(nameof(MembershipRole.Reviewer), nameof(MembershipRole.Admin))));

// The dashboard runs on a separate origin (e.g. http://localhost:3000), so the browser needs CORS.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000"];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGitHubWebhook();
app.MapVerdictEndpoints();
app.MapReviewEndpoints();
app.MapAuthEndpoints();
app.MapRepositoryEndpoints();

app.Run();

// Exposed for WebApplicationFactory-based integration tests (added in B2).
public partial class Program;
