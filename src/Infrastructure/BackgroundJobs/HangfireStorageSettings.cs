namespace Boilerate.Infrastructure.BackgroundJobs;

public class HangfireStorageSettings
{
    public bool Enabled { get; set; }
    public string StorageProvider { get; set; } = "SqlServer";
    public string? ConnectionString { get; set; }
    public bool EnableDashboard { get; set; } = true;
    public string DashboardPath { get; set; } = "/hangfire";
    public string DashboardTitle { get; set; } = "Boilerate Jobs";
    public int WorkerCount { get; set; } = 20;
    public int JobRetentionDays { get; set; } = 7;
    public bool EnableAutomaticRetry { get; set; } = true;
    public int MaxRetryAttempts { get; set; } = 3;
}
