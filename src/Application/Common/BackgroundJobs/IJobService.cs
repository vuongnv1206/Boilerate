using System.Linq.Expressions;
using Boilerate.Application.Common.Interfaces;

namespace Boilerate.Application.Common.BackgroundJobs;

public interface IJobService : ITransientService
{
    string Enqueue(Expression<Action> methodCall);
    string Enqueue<T>(Expression<Action<T>> methodCall);
    string Enqueue(Expression<Func<Task>> methodCall);
    string Enqueue<T>(Expression<Func<T, Task>> methodCall);

    string Schedule(Expression<Action> methodCall, TimeSpan delay);
    string Schedule<T>(Expression<Action<T>> methodCall, TimeSpan delay);
    string Schedule(Expression<Func<Task>> methodCall, TimeSpan delay);
    string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay);
    string Schedule(Expression<Action> methodCall, DateTimeOffset enqueueAt);
    string Schedule<T>(Expression<Action<T>> methodCall, DateTimeOffset enqueueAt);
    string Schedule(Expression<Func<Task>> methodCall, DateTimeOffset enqueueAt);
    string Schedule<T>(Expression<Func<T, Task>> methodCall, DateTimeOffset enqueueAt);

    void AddOrUpdateRecurringJob(string jobId, Expression<Action> methodCall, string cronExpression, TimeZoneInfo? timeZone = null);
    void AddOrUpdateRecurringJob<T>(string jobId, Expression<Action<T>> methodCall, string cronExpression, TimeZoneInfo? timeZone = null);
    void AddOrUpdateRecurringJob(string jobId, Expression<Func<Task>> methodCall, string cronExpression, TimeZoneInfo? timeZone = null);
    void AddOrUpdateRecurringJob<T>(string jobId, Expression<Func<T, Task>> methodCall, string cronExpression, TimeZoneInfo? timeZone = null);

    void RemoveRecurringJob(string jobId);
    void TriggerRecurringJob(string jobId);
    bool Delete(string jobId);
    bool Requeue(string jobId);

    string ContinueJobWith(string parentJobId, Expression<Action> methodCall);
    string ContinueJobWith<T>(string parentJobId, Expression<Action<T>> methodCall);
    string ContinueJobWith(string parentJobId, Expression<Func<Task>> methodCall);
    string ContinueJobWith<T>(string parentJobId, Expression<Func<T, Task>> methodCall);
}
