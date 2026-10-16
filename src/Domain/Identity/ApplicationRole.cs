using System.ComponentModel.DataAnnotations.Schema;
using Boilerate.Domain.Common.Contracts;
using Microsoft.AspNetCore.Identity;

namespace Boilerate.Domain.Identity;

public class ApplicationRole : IdentityRole, IEntity
{
    public ApplicationRole() { }
    
    public ApplicationRole(string roleName, string? description = null) : base(roleName)
    {
        Description = description;
        NormalizedName = roleName.ToUpperInvariant();
    }

    public string? Description { get; set; }

    [NotMapped]
    public List<DomainEvent> DomainEvents { get; } = new();
}
