using Boilerate.Application.Common.Models;
using MediatR;

namespace Boilerate.Application.Identity.Users;

public class ExportUsersRequest : BaseFilter, IRequest<byte[]>
{
    public bool? IsActive { get; set; }
    public string? Role { get; set; }
}
