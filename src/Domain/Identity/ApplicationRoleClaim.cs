using Microsoft.AspNetCore.Identity;

namespace Boilerate.Domain.Identity;

public class ApplicationRoleClaim : IdentityRoleClaim<string>
{
    public string? Description { get; set; }
    public string? Group { get; set; }
    public DateTime CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
}
