using Boilerate.Application.Common.Exporters;
using Boilerate.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Boilerate.Application.Identity.Users;

public class ExportUsersHandler : IRequestHandler<ExportUsersRequest, byte[]>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IExcelWriter _excelWriter;

    public ExportUsersHandler(
        UserManager<ApplicationUser> userManager,
        IExcelWriter excelWriter)
    {
        _userManager = userManager;
        _excelWriter = excelWriter;
    }

    public async Task<byte[]> Handle(
        ExportUsersRequest request,
        CancellationToken cancellationToken)
    {
        // Query users
        var usersQuery = _userManager.Users.AsNoTracking();

        if (request.IsActive.HasValue)
        {
            usersQuery = usersQuery.Where(u => u.IsActive == request.IsActive.Value);
        }

        if (!string.IsNullOrEmpty(request.Keyword))
        {
            usersQuery = usersQuery.Where(u => 
                u.UserName!.Contains(request.Keyword) || 
                u.Email!.Contains(request.Keyword) ||
                (u.FirstName != null && u.FirstName.Contains(request.Keyword)) ||
                (u.LastName != null && u.LastName.Contains(request.Keyword)));
        }

        if (await usersQuery.CountAsync(cancellationToken) > 10000) throw new FluentValidation.ValidationException("Export is limited to 10000 users; narrow the filters.");
        // Project to export DTO
        var users = await usersQuery
            .Select(u => new UserExportDto
            {
                UserName = u.UserName ?? string.Empty,
                FirstName = u.FirstName ?? string.Empty,
                LastName = u.LastName ?? string.Empty,
                Email = u.Email ?? string.Empty,
                PhoneNumber = u.PhoneNumber,
                IsActive = u.IsActive
            })
            .ToListAsync(cancellationToken);

        // Export to Excel
        return await _excelWriter.WriteAsync(
            data: users,
            sheetName: "Users",
            headers: new List<string>
            {
                "Username",
                "First Name",
                "Last Name",
                "Email",
                "Phone Number",
                "Active"
            },
            title: "DANH SÁCH NGƯỜI DÙNG HỆ THỐNG",
            cancellationToken: cancellationToken);
    }
}
