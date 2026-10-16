using Ardalis.Specification;
using Boilerate.Application.Common.Specification;
using Boilerate.Domain.Auditing;
using FluentValidation;

namespace Boilerate.Application.Auditing;

public class AuditLogsExportSpec : Specification<Trail>
{
    public AuditLogsExportSpec(ExportAuditLogsRequest request)
    {
        if (request.StartDate > request.EndDate)
        {
            throw new ValidationException("The export start date must not be after its end date.");
        }

        Query.SearchBy(request);
        Query.OrderByDescending(x => x.DateTime).ThenByDescending(x => x.Id);

        if (request.StartDate.HasValue)
        {
            Query.Where(x => x.DateTime >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            Query.Where(x => x.DateTime <= request.EndDate.Value);
        }

        if (!string.IsNullOrEmpty(request.UserId))
        {
            if (!Guid.TryParse(request.UserId, out var userIdGuid))
            {
                throw new ValidationException("The export user ID must be a valid GUID.");
            }

            Query.Where(x => x.UserId == userIdGuid);
        }

        if (!string.IsNullOrEmpty(request.TableName))
        {
            Query.Where(x => x.TableName == request.TableName);
        }

        // Limit to 10,000 records for export
        Query.Take(10000);
    }
}
