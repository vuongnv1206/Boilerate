using System.ComponentModel.DataAnnotations.Schema;
using Boilerate.Domain.Common.Contracts;
using Microsoft.AspNetCore.Identity;

namespace Boilerate.Domain.Identity;

public class ApplicationUser : IdentityUser, IEntity
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? ObjectId { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    [NotMapped]
    public List<DomainEvent> DomainEvents { get; } = new();
}
