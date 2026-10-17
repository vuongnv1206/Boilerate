using System.Security.Cryptography;
using Boilerate.Application.Catalog;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.Media;
using Boilerate.Domain.Messaging;
using Boilerate.Infrastructure.Commerce;
using Boilerate.Infrastructure.Persistence.Context;
using Boilerate.Infrastructure.Persistence.Initialization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Boilerate.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RecoveryTheoryAttribute : TheoryAttribute
{
    public RecoveryTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("BOILERATE_TEST_RECOVERY") != "1")
            Skip = "Set BOILERATE_TEST_RECOVERY=1 to run native database/media/key-ring recovery drills.";
    }
}

[Collection("Foundation")]
public sealed class RecoveryRuntimeTests
{
    private static readonly byte[] Image = [137, 80, 78, 71, 13, 10, 26, 10, 1];

    [RecoveryTheory, Trait("Category", "ProviderIntegration"), Trait("Runtime", "Recovery")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task NativeBackupRestoresCatalogMediaKeysAndOutboxIntoIsolatedInstallation(string provider)
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "boilerate-recovery-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        NativeRecoveryBackup? backup = null;
        try
        {
            string sourceFiles = Path.Combine(root, "source", "files"), sourceKeys = Path.Combine(root, "source", "keys");
            string restoredFiles = Path.Combine(root, "restored", "files"), restoredKeys = Path.Combine(root, "restored", "keys");
            await using var source = new FoundationDatabase(provider, catalog: true, configOverrides: RuntimeConfiguration(sourceFiles, sourceKeys));
            await source.CreateAsync();
            Guid actor = Guid.NewGuid();
            ProductDto product;
            ProductType type;
            Guid inventoryId, movementId, operationId = Guid.NewGuid(), orphanId, linkedId, orphanMessageId;
            Guid[] categoryIds;
            using (var scope = source.Scope(actor))
            {
                var service = Commerce(scope);
                await service.InitializeAsync(default);
                type = await service.CreateTypeAsync(new("RECOVERY", "Recovery type", []), default);
                var first = await service.CategoryAsync(new(null, null, "Recovery A", "recovery-a", null, null, 0, true), default);
                var second = await service.CategoryAsync(new(null, null, "Recovery B", "recovery-b", null, first.Id, 0, true), default);
                categoryIds = [first.Id, second.Id];
                linkedId = await Upload(scope);
                orphanId = await Upload(scope);
                product = await service.CreateAsync(new(type.Id, type.Version,
                    new("Recovery product", "recovery-product", "Snapshot description", categoryIds.ToList(), [], [new(linkedId, 0, "Snapshot image")]),
                    [], [new("RECOVERY-SKU", 170000, null, new())]), default);
                product = await service.StatusAsync(new(product.Id, product.Version, ProductStatus.Published), default);
                var inventory = Assert.Single((await service.InventoryAsync(1, 20, default)).Items);
                inventoryId = inventory.Id;
                movementId = (await service.AdjustAsync(new(inventory.Id, inventory.Version, operationId, 7, "Recovery snapshot stock"), default)).Id;
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var message = OutboxMessage.Cleanup(orphanId, actor, "recovery-drill");
                message.State = OutboxState.Processing;
                message.Attempts = 1;
                message.LeaseToken = Guid.NewGuid();
                message.LeaseUntil = DateTime.UtcNow.AddMinutes(-2);
                orphanMessageId = message.MessageId;
                db.OutboxMessages.Add(message);
                db.OutboxMessages.Add(OutboxMessage.Cleanup(linkedId, actor, "recovery-linked-protection"));
                await db.SaveChangesAsync();
            }
            string protectedValue = source.Services.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("RecoveryDrill.v1").Protect("snapshot-key-proof");
            Assert.NotEmpty(Directory.GetFiles(sourceKeys, "key-*.xml"));

            // No host/worker is started and all write scopes above are closed. Database and volume
            // snapshots therefore describe the same quiescent recovery point.
            backup = await NativeRecoveryBackup.CreateAsync(provider, source, root);
            var filesSnapshot = Manifest(sourceFiles);
            var keysSnapshot = Manifest(sourceKeys);
            CopyDirectory(sourceFiles, Path.Combine(root, "snapshot", "files"));
            CopyDirectory(sourceKeys, Path.Combine(root, "snapshot", "keys"));
            CopyDirectory(Path.Combine(root, "snapshot", "files"), restoredFiles);
            CopyDirectory(Path.Combine(root, "snapshot", "keys"), restoredKeys);
            AssertManifest(filesSnapshot, restoredFiles);
            AssertManifest(keysSnapshot, restoredKeys);

            // These post-backup writes must not appear in the restored installation.
            using (var scope = source.Scope(actor))
            {
                var service = Commerce(scope);
                product = await service.EditAsync(new(product.Id, product.Version, type.Version,
                    new("Source changed after backup", product.Slug, null, [], [], [new(linkedId, 0, null)])), default);
                var inventory = Assert.Single((await service.InventoryAsync(1, 20, default)).Items);
                await service.AdjustAsync(new(inventory.Id, inventory.Version, Guid.NewGuid(), 3, "After snapshot"), default);
            }
            var restoreConfiguration = RuntimeConfiguration(restoredFiles, restoredKeys);
            restoreConfiguration["SecuritySettings:JwtSettings:Key"] = source.Configuration["SecuritySettings:JwtSettings:Key"];
            await using var restored = new FoundationDatabase(provider, catalog: true, configOverrides: restoreConfiguration);
            await restored.CreateAsync(migrate: false);
            await backup.RestoreAsync(restored);
            using (var scope = restored.Scope(actor))
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
                await db.Database.MigrateAsync();
                await scope.ServiceProvider.GetRequiredService<FoundationBootstrap>().PreflightAsync();
                var service = Commerce(scope);
                var recovered = await service.GetBySlugAsync("recovery-product", default);
                Assert.Equal(product.Id, recovered.Id);
                Assert.Equal("Recovery product", recovered.Name);
                Assert.Equal("Snapshot description", recovered.Description);
                Assert.Equal(categoryIds.Order(), recovered.CategoryIds.Order());
                Assert.Equal(170000m, Assert.Single(recovered.Variants).Price);
                Assert.Equal("RECOVERY-SKU", Assert.Single(recovered.Variants).Sku);
                var categories = await service.CategoriesAsync(false, default);
                Assert.Equal(categoryIds[0], categories.Single(c => c.Id == categoryIds[1]).ParentId);
                var inventory = Assert.Single((await service.InventoryAsync(1, 20, default)).Items);
                Assert.Equal(inventoryId, inventory.Id);
                Assert.Equal(7, inventory.OnHand);
                var movement = Assert.Single((await service.MovementsAsync(inventory.Id, 1, 20, default)).Items);
                Assert.Equal(movementId, movement.Id);
                Assert.Equal(operationId, movement.OperationId);
                Assert.Equal(7, movement.QuantityAfter);
                Assert.Equal(actor, movement.ActorId);
                Assert.Equal("snapshot-key-proof", restored.Services.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("RecoveryDrill.v1").Unprotect(protectedValue));
                var opened = await scope.ServiceProvider.GetRequiredService<ICommerceMediaService>().OpenAsync(linkedId, true, default);
                await using (opened.Stream)
                {
                    using var image = new MemoryStream();
                    await opened.Stream.CopyToAsync(image);
                    Assert.Equal(Image, image.ToArray());
                }
                Assert.Equal(OutboxState.Processing, (await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.MessageId == orphanMessageId)).State);
            }
            // Independent scopes model a new worker process reclaiming a lease lost at the recovery point.
            for (int i = 0; i < 2; i++)
            {
                using var scope = restored.Scope(actor);
                Assert.True(await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().RunOneAsync(default));
            }
            using (var scope = restored.Scope(actor))
            {
                Assert.False(await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().RunOneAsync(default));
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.All(await db.OutboxMessages.AsNoTracking().ToListAsync(), m => Assert.Equal(OutboxState.Completed, m.State));
                Assert.Equal(MediaState.Deleted, (await db.MediaAssets.AsNoTracking().SingleAsync(m => m.Id == orphanId)).State);
                Assert.Equal(MediaState.Ready, (await db.MediaAssets.AsNoTracking().SingleAsync(m => m.Id == linkedId)).State);
                Assert.Equal(filesSnapshot.Count - 1, Manifest(restoredFiles).Count);
            }
            AssertManifest(filesSnapshot, sourceFiles); // The restored worker must not touch source storage.
            using (var scope = source.Scope(actor))
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Equal(MediaState.Ready, (await db.MediaAssets.AsNoTracking().SingleAsync(m => m.Id == orphanId)).State);
                Assert.Equal(10, (await db.InventoryItems.AsNoTracking().SingleAsync()).OnHand);
                Assert.Equal("Source changed after backup", (await db.Products.AsNoTracking().SingleAsync()).Name);
                Assert.All(await db.OutboxMessages.AsNoTracking().ToListAsync(), m => Assert.NotEqual(OutboxState.Completed, m.State));
            }
        }
        finally
        {
            try { backup?.DeleteBackup(); }
            finally { DeleteGeneratedDirectory(root); }
        }
    }

    private static Dictionary<string, string?> RuntimeConfiguration(string files, string keys) => new()
    {
        ["Storage:Root"] = files, ["DataProtection:KeysPath"] = keys,
        ["DataProtection:ApplicationName"] = "Boilerate.RecoveryDrill", ["Commerce:WorkerEnabled"] = "false"
    };

    private static CommerceService Commerce(IServiceScope scope)
    {
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        return new(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), scope.ServiceProvider.GetRequiredService<CommerceUnitOfWork>(),
            scope.ServiceProvider.GetRequiredService<ICurrentUser>(), users, Options.Create(new CommerceSettings()),
            scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());
    }

    private static async Task<Guid> Upload(IServiceScope scope)
    {
        using var stream = new MemoryStream(Image);
        return (await scope.ServiceProvider.GetRequiredService<ICommerceMediaService>()
            .UploadAsync(new FormFile(stream, 0, stream.Length, "file", "recovery.png"), default)).Id;
    }

    private static Dictionary<string, string> Manifest(string directory) => Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
        .ToDictionary(file => Path.GetRelativePath(directory, file), file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))), StringComparer.Ordinal);

    private static void AssertManifest(IReadOnlyDictionary<string, string> expected, string directory)
    {
        var actual = Manifest(directory);
        Assert.Equal(expected.OrderBy(x => x.Key, StringComparer.Ordinal), actual.OrderBy(x => x.Key, StringComparer.Ordinal));
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
        {
            Assert.False(File.GetAttributes(file).HasFlag(FileAttributes.ReparsePoint));
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
        }
        foreach (string child in Directory.GetDirectories(source))
        {
            Assert.False(File.GetAttributes(child).HasFlag(FileAttributes.ReparsePoint));
            CopyDirectory(child, Path.Combine(destination, Path.GetFileName(child)));
        }
    }

    private static void DeleteGeneratedDirectory(string directory)
    {
        string resolved = Path.GetFullPath(directory);
        string parent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string name = Path.GetFileName(resolved);
        Assert.Equal(parent, Path.GetDirectoryName(resolved));
        Assert.True(name.StartsWith("boilerate-recovery-", StringComparison.Ordinal) && Guid.TryParseExact(name["boilerate-recovery-".Length..], "N", out _));
        if (Directory.Exists(resolved))
            Directory.Delete(resolved, recursive: true);
    }
}
