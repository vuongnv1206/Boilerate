using System.Linq.Expressions;
using Boilerate.Application.Common.BackgroundJobs;
using Hangfire;

namespace Boilerate.Infrastructure.BackgroundJobs;

public class HangfireService : IJobService
{
    public string Enqueue(Expression<Action> methodCall) => BackgroundJob.Enqueue(methodCall);
    public string Enqueue<T>(Expression<Action<T>> methodCall) => BackgroundJob.Enqueue(methodCall);
    public string Enqueue(Expression<Func<Task>> methodCall) => BackgroundJob.Enqueue(methodCall);
    public string Enqueue<T>(Expression<Func<T, Task>> methodCall) => BackgroundJob.Enqueue(methodCall);

    public string Schedule(Expression<Action> methodCall, TimeSpan delay) => BackgroundJob.Schedule(methodCall, delay);
    public string Schedule<T>(Expression<Action<T>> methodCall, TimeSpan delay) => BackgroundJob.Schedule(methodCall, delay);
    public string Schedule(Expression<Func<Task>> methodCall, TimeSpan delay) => BackgroundJob.Schedule(methodCall, delay);
    public string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay) => BackgroundJob.Schedule(methodCall, delay);
    public string Schedule(Expression<Action> methodCall, DateTimeOffset enqueueAt) => BackgroundJob.Schedule(methodCall, enqueueAt);
    public string Schedule<T>(Expression<Action<T>> methodCall, DateTimeOffset enqueueAt) => BackgroundJob.Schedule(methodCall, enqueueAt);
    public string Schedule(Expression<Func<Task>> methodCall, DateTimeOffset enqueueAt) => BackgroundJob.Schedule(methodCall, enqueueAt);
    public string Schedule<T>(Expression<Func<T, Task>> methodCall, DateTimeOffset enqueueAt) => BackgroundJob.Schedule(methodCall, enqueueAt);

    public void AddOrUpdateRecurringJob(string jobId, Expression<Action> methodCall, string cronExpression, TimeZoneInfo? timeZone = null) => RecurringJob.AddOrUpdate(jobId, methodCall, cronExpression, new RecurringJobOptions { TimeZone = timeZone ?? TimeZoneInfo.Utc });
    public void AddOrUpdateRecurringJob<T>(string jobId, Expression<Action<T>> methodCall, string cronExpression, TimeZoneInfo? timeZone = null) => RecurringJob.AddOrUpdate(jobId, methodCall, cronExpression, new RecurringJobOptions { TimeZone = timeZone ?? TimeZoneInfo.Utc });
    public void AddOrUpdateRecurringJob(string jobId, Expression<Func<Task>> methodCall, string cronExpression, TimeZoneInfo? timeZone = null) => RecurringJob.AddOrUpdate(jobId, methodCall, cronExpression, new RecurringJobOptions { TimeZone = timeZone ?? TimeZoneInfo.Utc });
    public void AddOrUpdateRecurringJob<T>(string jobId, Expression<Func<T, Task>> methodCall, string cronExpression, TimeZoneInfo? timeZone = null) => RecurringJob.AddOrUpdate(jobId, methodCall, cronExpression, new RecurringJobOptions { TimeZone = timeZone ?? TimeZoneInfo.Utc });

    public void RemoveRecurringJob(string jobId) => RecurringJob.RemoveIfExists(jobId);
    public void TriggerRecurringJob(string jobId) => RecurringJob.TriggerJob(jobId);
    public bool Delete(string jobId) => BackgroundJob.Delete(jobId);
    public bool Requeue(string jobId) => BackgroundJob.Requeue(jobId);

    public string ContinueJobWith(string parentJobId, Expression<Action> methodCall) => BackgroundJob.ContinueJobWith(parentJobId, methodCall);
    public string ContinueJobWith<T>(string parentJobId, Expression<Action<T>> methodCall) => BackgroundJob.ContinueJobWith(parentJobId, methodCall);
    public string ContinueJobWith(string parentJobId, Expression<Func<Task>> methodCall) => BackgroundJob.ContinueJobWith(parentJobId, methodCall);
    public string ContinueJobWith<T>(string parentJobId, Expression<Func<T, Task>> methodCall) => BackgroundJob.ContinueJobWith(parentJobId, methodCall);
}
