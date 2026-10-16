using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.FileStorage;
using Boilerate.Application.Identity.Users;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Boilerate.Host.Controllers;

[Authorize]
public sealed class FilesController(ApplicationDbContext db, IUserService users, IConfiguration config, IFileStorageService storage) : ControllerBase
{
    [HttpGet("/files/{**path}")]
    public async Task<IActionResult> Get(string path, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(path) || path.Contains("..", StringComparison.Ordinal) || path.Contains('\\') || path.Contains(':'))
        {
            throw new ForbiddenException("Invalid file path.");
        }

        string relative = "Files/" + path;
        string userId = User.GetUserId()!;
        string? storedPath = await db.Users.Where(u => u.Id == userId && u.ImageUrl == relative)
            .Select(u => u.ImageUrl).FirstOrDefaultAsync(ct);
        if (storedPath is null && await users.HasPermissionAsync(userId, "Permissions.User.View", ct))
        {
            storedPath = await db.Users.Where(u => u.ImageUrl == relative).Select(u => u.ImageUrl).FirstOrDefaultAsync(ct);
        }

        if (storedPath is null && config.GetValue<bool>("Modules:Catalog:Enabled") &&
            await users.HasPermissionAsync(userId, "Permissions.Product.View", ct))
        {
            storedPath = await db.Products.Where(p => p.ImagePath == relative).Select(p => p.ImagePath).FirstOrDefaultAsync(ct);
        }

        if (storedPath is null)
        {
            throw new NotFoundException("File not found.");
        }

        try
        {
            Stream image = storage.OpenRead(storedPath);
            Response.Headers.XContentTypeOptions = "nosniff";
            string contentType = Path.GetExtension(storedPath).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
            // MVC owns and disposes the stream after completing the response.
            return File(image, contentType);
        }
        catch (IOException ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new NotFoundException("File not found.");
        }
    }
}
