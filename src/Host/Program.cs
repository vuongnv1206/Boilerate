using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Boilerate.Application;
using Boilerate.Host.Configurations;
using Boilerate.Infrastructure;
using Boilerate.Infrastructure.Common;
using Boilerate.Infrastructure.Logging;
using Serilog;

StaticLogger.EnsureInitialized();
Log.Information("Server Booting Up...");

try
{
    var operatorFlags = new[] { "--bootstrap-admin", "--register-capabilities", "--grant-admin-capabilities", "--preflight", "--seed-dev" };
    if (args.Contains("--seed-dev") && args.Any(argument => argument != "--seed-dev" && operatorFlags.Contains(argument)))
        throw new InvalidOperationException("Run --seed-dev separately from foundation bootstrap/preflight commands.");
    var builder = WebApplication.CreateBuilder(args.Where(a => !operatorFlags.Contains(a)).ToArray());

    builder.AddConfigurations().RegisterSerilog();

    builder.Services.AddControllers().ConfigureApplicationPartManager(parts =>
        parts.FeatureProviders.Add(new CatalogControllerFeatureProvider(builder.Configuration.GetValue<bool>("Modules:Catalog:Enabled"), builder.Configuration.GetValue<bool>("Modules:Notifications:Enabled"))));
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddHttpClient();
    builder.Services.AddHttpClient("OAuth").RemoveAllLoggers();
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = 429;
        options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("SecuritySettings:AuthenticationRequestsPerMinute", 10),
                Window = TimeSpan.FromMinutes(1), QueueLimit = 0
            }));
    });
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "Boilerate API",
            Version = "v1"
        });

        // Add JWT Bearer Security Definition
        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\""
        });

        options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                new string[] {}
            }
        });

        // Load XML Comments for Descriptions
        var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.xml");
        foreach (var xmlFile in xmlFiles)
        {
            options.IncludeXmlComments(xmlFile);
        }
    });



    var app = builder.Build();

    if (args.Contains("--seed-dev"))
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(app.Lifetime.ApplicationStopping);
        ConsoleCancelEventHandler cancelSeed = (_, cancellationEvent) =>
        {
            cancellationEvent.Cancel = true;
            cancellation.Cancel();
        };
        Console.CancelKeyPress += cancelSeed;
        try
        {
            using var scope = app.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<Boilerate.Infrastructure.Persistence.Initialization.CustomSeederRunner>()
                .RunAsync(cancellation.Token);
        }
        finally
        {
            Console.CancelKeyPress -= cancelSeed;
        }
        return;
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    if (args.Contains("--bootstrap-admin") || args.Contains("--register-capabilities") || args.Contains("--grant-admin-capabilities") || args.Contains("--preflight"))
    {
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<Boilerate.Infrastructure.Persistence.Initialization.FoundationBootstrap>().RunAsync(args);
        return;
    }

    app.UseInfrastructure(builder.Configuration);
    app.MapEndpoints();

    Log.Information("Application Starting...");
    await app.RunAsync();
}
catch (Exception ex) when (!ex.GetType().Name.Equals("HostAbortedException", StringComparison.Ordinal))
{
    StaticLogger.EnsureInitialized();
    Log.Fatal(ex, "Unhandled exception occurred during application startup");
    Environment.ExitCode = 1;
}
finally
{
    StaticLogger.EnsureInitialized();
    Log.Information("Server Shutting down...");
    await Log.CloseAndFlushAsync();
}

public partial class Program { }
