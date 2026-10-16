namespace Boilerate.Host.Configurations;

internal static class Startup
{
    internal static WebApplicationBuilder AddConfigurations(
        this WebApplicationBuilder builder)
    {
        const string configurationsDirectory = "Configurations";
        var env = builder.Environment;
   
        builder.Configuration
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/database.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/database.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/logger.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/logger.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/security.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/security.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/cache.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/cache.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/mail.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/mail.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/hangfire.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/hangfire.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/auth.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/auth.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/storage.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"{configurationsDirectory}/storage.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
        if (env.IsDevelopment())
            builder.Configuration.AddUserSecrets<Program>(optional: true);
        builder.Configuration.AddEnvironmentVariables();
     
        return builder;
    }
}
