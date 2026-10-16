using Boilerate.Application.Common.Models;

namespace Boilerate.Application.Identity.Users;

public class UserParameterFilter : PaginationFilter
{
    public bool? IsActive { get; set; }
}
