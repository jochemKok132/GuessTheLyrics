using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using System.Security.Claims;
using GuessTheLyrics.API.Handlers;
using GuessTheLyrics.API.Options;
using GuessTheLyrics.API.Security;
using GuessTheLyrics.Application.Interfaces;
using GuessTheLyrics.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace GuessTheLyrics.API;

public static class Dependencies
{
    public const string FrontendCorsPolicy = "Frontend";
    public const int RequestsPerMinute = 60;
    public const int MatchRequestsPerMinute = 240;

    public static IServiceCollection RegisterAPI(this IServiceCollection services, IConfiguration configuration)
    {
        services.RegisterControllers();
        services.RegisterForwardedHeaders();
        services.RegisterExceptionHandling();
        services.RegisterCors(configuration);
        services.RegisterAuthentication(configuration);
        services.RegisterRateLimiter();
        services.RegisterSwagger();
        return services;
    }

    private static void RegisterControllers(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions))
            // Invalid JSON, wrong types or a missing body: same ApiResponse envelope as every other error.
            .ConfigureApiBehaviorOptions(options =>
            {
                options.InvalidModelStateResponseFactory = ApiErrorResponses.InvalidModelState;
                // No automatic ProblemDetails bodies for 4xx results (e.g. 415): UseApiStatusCodePages fills them in.
                options.SuppressMapClientErrors = true;
            });

        // Used by WriteAsJsonAsync in the exception handler and rate limiter, so error bodies match.
        services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));
    }

    private static void ConfigureJson(JsonSerializerOptions options)
    {
        options.Converters.Add(new JsonStringEnumConverter());
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    }

    private static void RegisterForwardedHeaders(this IServiceCollection services)
    {
        // In production the API sits behind the host nginx (container port bound to 127.0.0.1 only),
        // so the real client IP and scheme come from X-Forwarded-For / X-Forwarded-Proto.
        // Without this, the rate limiter would see every player as the same proxy IP.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });
    }

    private static void RegisterExceptionHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
    }

    private static void RegisterCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<FrontendOptions>()
            .Bind(configuration.GetSection(FrontendOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var origins = configuration.GetSection(FrontendOptions.SectionName).Get<FrontendOptions>()?.AllowedOrigins ?? [];

        services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy =>
            policy.WithOrigins(origins)
                  .AllowAnyHeader()
                  .WithMethods("GET", "POST", "PUT")));
    }

    private static void RegisterAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ITokenService, TokenService>();

        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                  ?? throw new InvalidOperationException($"Configuration section '{JwtOptions.SectionName}' is missing.");

        if (string.IsNullOrWhiteSpace(jwt.Key))
            throw new InvalidOperationException("Jwt:Key is missing. Set it in appsettings.Development.json or with the JWT__Key environment variable.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = TokenService.ValidationParameters(jwt);

                // Never an empty body: auth failures use the same envelope as every other error.
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;

                        var message = context.AuthenticateFailure is SecurityTokenExpiredException
                            ? "Your session has expired. Log in again."
                            : context.AuthenticateFailure is not null
                                ? "Your login is not valid any more. Log in again."
                                : "You need to be logged in.";

                        await context.Response.WriteAsJsonAsync(
                            ApiResponse<object>.Fail(message, ErrorCodes.Unauthorized, TraceIds.Get(context.HttpContext)));
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        await context.Response.WriteAsJsonAsync(
                            ApiResponse<object>.Fail("You are not allowed to do this.", ErrorCodes.Forbidden, TraceIds.Get(context.HttpContext)));
                    }
                };
            });

        services.AddAuthorization();
    }

    private static void RegisterRateLimiter(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Partitioned per logged-in player, falling back to the IP address for anonymous requests.
            // Multiplayer polls about once a second, so /api/matches has its own, bigger budget.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var who = context.User.FindFirstValue(AppClaims.UserId)
                          ?? context.Connection.RemoteIpAddress?.ToString()
                          ?? "unknown";
                var isMatch = context.Request.Path.StartsWithSegments("/api/matches");

                return RateLimitPartition.GetFixedWindowLimiter(
                    isMatch ? $"matches:{who}" : who,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = isMatch ? MatchRequestsPerMinute : RequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = (int)HttpStatusCode.TooManyRequests;

                var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? (int)Math.Ceiling(retryAfter.TotalSeconds)
                    : 60;
                response.Headers.RetryAfter = seconds.ToString();

                await response.WriteAsJsonAsync(
                    ApiResponse<object>.Fail($"Too many requests. Wait {seconds} seconds and try again.",
                        ErrorCodes.RateLimited, TraceIds.Get(context.HttpContext)),
                    cancellationToken);
            };
        });
    }

    private static void RegisterSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste the token from /api/auth/login."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });
    }
}
