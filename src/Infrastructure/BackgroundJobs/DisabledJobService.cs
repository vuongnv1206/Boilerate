using System.Linq.Expressions;
using Boilerate.Application.Common.BackgroundJobs;
using Boilerate.Application.Common.Exceptions;

namespace Boilerate.Infrastructure.BackgroundJobs;

internal sealed class DisabledJobService : IJobService
{
    private static Exception Disabled() => new ConflictException("Background jobs are disabled. Configure and enable Hangfire first.");
    public string Enqueue(Expression<Action> call) => throw Disabled();
    public string Enqueue<T>(Expression<Action<T>> call) => throw Disabled();
    public string Enqueue(Expression<Func<Task>> call) => throw Disabled();
    public string Enqueue<T>(Expression<Func<T, Task>> call) => throw Disabled();
    public string Schedule(Expression<Action> call, TimeSpan delay) => throw Disabled();
    public string Schedule<T>(Expression<Action<T>> call, TimeSpan delay) => throw Disabled();
    public string Schedule(Expression<Func<Task>> call, TimeSpan delay) => throw Disabled();
    public string Schedule<T>(Expression<Func<T, Task>> call, TimeSpan delay) => throw Disabled();
    public string Schedule(Expression<Action> call, DateTimeOffset at) => throw Disabled();
    public string Schedule<T>(Expression<Action<T>> call, DateTimeOffset at) => throw Disabled();
    public string Schedule(Expression<Func<Task>> call, DateTimeOffset at) => throw Disabled();
    public string Schedule<T>(Expression<Func<T, Task>> call, DateTimeOffset at) => throw Disabled();
    public void AddOrUpdateRecurringJob(string id, Expression<Action> call, string cron, TimeZoneInfo? zone = null) => throw Disabled();
    public void AddOrUpdateRecurringJob<T>(string id, Expression<Action<T>> call, string cron, TimeZoneInfo? zone = null) => throw Disabled();
    public void AddOrUpdateRecurringJob(string id, Expression<Func<Task>> call, string cron, TimeZoneInfo? zone = null) => throw Disabled();
    public void AddOrUpdateRecurringJob<T>(string id, Expression<Func<T, Task>> call, string cron, TimeZoneInfo? zone = null) => throw Disabled();
    public void RemoveRecurringJob(string id) => throw Disabled();
    public void TriggerRecurringJob(string id) => throw Disabled();
    public bool Delete(string id) => throw Disabled();
    public bool Requeue(string id) => throw Disabled();
    public string ContinueJobWith(string id, Expression<Action> call) => throw Disabled();
    public string ContinueJobWith<T>(string id, Expression<Action<T>> call) => throw Disabled();
    public string ContinueJobWith(string id, Expression<Func<Task>> call) => throw Disabled();
    public string ContinueJobWith<T>(string id, Expression<Func<T, Task>> call) => throw Disabled();
}
