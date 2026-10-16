using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Boilerate.Application.Common.Caching;
using Boilerate.Application.Common.Events;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.FileStorage;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Mailing;
using Boilerate.Application.Common.Models;
using Boilerate.Application.Common.Specification;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Identity;
using Boilerate.Infrastructure.Auth;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Shared.Authorization;
using Mapster;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Boilerate.Application.Common.BackgroundJobs;
using Boilerate.Application.Identity.Users.Password;

namespace Boilerate.Infrastructure.Identity;

internal partial class UserService : IUserService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ApplicationDbContext _db;
    private readonly IJobService _jobService;
    private readonly IMailService _mailService;
    private readonly SecuritySettings _securitySettings;
    private readonly IEmailTemplateService _templateService;
    private readonly IFileStorageService _fileStorage;
    private readonly IServiceProvider _services;
    private readonly AclGuard _guard;
    private readonly ICurrentUser _actor;

    public UserService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext db,
        IJobService jobService,
        IMailService mailService,
        IOptions<SecuritySettings> securitySettings,
        IEmailTemplateService templateService,
        IFileStorageService fileStorage,
        IServiceProvider services, AclGuard guard, ICurrentUser actor)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _jobService = jobService;
        _mailService = mailService;
        _securitySettings = securitySettings.Value;
        _templateService = templateService;
        _fileStorage = fileStorage;
        _services = services; _guard = guard; _actor = actor;
    }

    public async Task<PaginationResponse<UserDetailDto>> SearchAsync(UserParameterFilter filter, CancellationToken cancellationToken)
    {
        var spec = new EntitiesByPaginationFilterSpec<ApplicationUser>(filter);
        var query = _userManager.Users.AsNoTracking();
        if (filter.IsActive.HasValue)
        {
            query = query.Where(user => user.IsActive == filter.IsActive.Value);
        }

        var users = await query
            .WithSpecification(spec)
            .ProjectToType<UserDetailDto>()
            .ToListAsync(cancellationToken);

        int count = await query.WithSpecification(new EntitiesByBaseFilterSpec<ApplicationUser>(filter)).CountAsync(cancellationToken);

        return new PaginationResponse<UserDetailDto>(users, count, filter.PageNumber, filter.PageSize);
    }

    public async Task<bool> ExistsWithNameAsync(string name)
    {
        return await _userManager.FindByNameAsync(name) is not null;
    }

    public async Task<bool> ExistsWithEmailAsync(string email, string? exceptId = null)
    {
        return await _userManager.FindByEmailAsync(email) is ApplicationUser user && user.Id != exceptId;
    }

    public async Task<bool> ExistsWithPhoneNumberAsync(string phoneNumber, string? exceptId = null)
    {
        return await _userManager.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phoneNumber) is ApplicationUser user && user.Id != exceptId;
    }

    public async Task<string> GetFullName(Guid userId)
    {
        var user = await GetAsync(userId.ToString(), CancellationToken.None);
        return string.Join(" ", user.FirstName, user.LastName);
    }

    public async Task<List<UserDetailDto>> GetListAsync(CancellationToken cancellationToken) =>
        (await _userManager.Users.AsNoTracking().ToListAsync(cancellationToken)).Adapt<List<UserDetailDto>>();

    public Task<int> GetCountAsync(CancellationToken cancellationToken) =>
        _userManager.Users.AsNoTracking().CountAsync(cancellationToken);

    public async Task<UserDetailDto> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken);

        _ = user ?? throw new NotFoundException("User Not Found.");

        var dto = user.Adapt<UserDetailDto>();
        dto.Version = user.ConcurrencyStamp!;
        return dto;
    }

    public async Task ToggleStatusAsync(ToggleUserStatusRequest request, CancellationToken cancellationToken)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _guard.LockAsync(cancellationToken);
        var user = await _userManager.FindByIdAsync(request.UserId) ?? throw new NotFoundException("User not found.");
        AclGuard.CheckVersion(request.Version, user.ConcurrencyStamp);
        var before = new { user.IsActive };
        user.IsActive = request.ActivateUser;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) throw new ConflictException(string.Join("; ", result.GetErrors()));
        await _guard.EnsureAdministratorRemainsAsync(cancellationToken);
        if (!user.IsActive) await _db.UserSessions.Where(x => x.UserId == user.Id && x.RevokedOn == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedOn, DateTime.UtcNow), cancellationToken);
        _guard.Audit("Users", user.Id, before, new { user.IsActive });
        await _db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken);
    }
}
