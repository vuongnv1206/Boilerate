using Boilerate.Application.Auditing;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Models;
using Boilerate.Domain.Auditing;
using Boilerate.Infrastructure.Persistence.Context;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Infrastructure.Auditing;

/// <summary>
/// Service implementation for querying audit trails.
/// </summary>
public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser _currentUser;

    public AuditService(
        ApplicationDbContext context,
        ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Get audit logs for current user with filters and pagination.
    /// </summary>
    public async Task<PaginationResponse<AuditDto>> GetMyAuditLogsAsync(
        GetMyAuditLogsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated()) throw new Boilerate.Application.Common.Exceptions.UnauthorizedException("Authentication required.");
        if (request.PageNumber < 1 || request.PageSize < 1 || request.PageSize > 100 || ((long)request.PageNumber - 1) * request.PageSize > int.MaxValue)
            throw new FluentValidation.ValidationException("Invalid pagination.");
        var userId = _currentUser.GetUserId();

        // Build query
        var query = _context.Trails
            .Where(a => a.UserId == userId)
            .AsNoTracking() // Performance
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(request.TableName))
        {
            query = query.Where(a => a.TableName == request.TableName);
        }

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            if (!Enum.TryParse<TrailType>(request.Type, out var trailType) || !Enum.IsDefined(trailType))
            {
                throw new FluentValidation.ValidationException("Unknown audit trail type.");
            }

            query = query.Where(a => a.Type == trailType);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(a => a.DateTime >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(a => a.DateTime <= request.ToDate.Value);
        }

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination and ordering
        var auditLogs = await query
            .OrderByDescending(a => a.DateTime)  // Latest first
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // Map to DTOs
        var dtos = auditLogs.Adapt<List<AuditDto>>();

        return new PaginationResponse<AuditDto>(
            dtos,
            totalCount,
            request.PageNumber,
            request.PageSize);
    }
}
