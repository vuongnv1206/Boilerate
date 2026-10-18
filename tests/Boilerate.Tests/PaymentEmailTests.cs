using System.Collections.Concurrent;
using System.Text.Json;
using Boilerate.Application.Common.Interfaces;
using Boilerate.Application.Common.Mailing;
using Boilerate.Application.Identity.Users;
using Boilerate.Domain.Catalog;
using Boilerate.Domain.DigitalInventory;
using Boilerate.Domain.Messaging;
using Boilerate.Domain.Ordering;
using Boilerate.Domain.Payments;
using Boilerate.Infrastructure.Commerce;
using Boilerate.Infrastructure.FileStorage;
using Boilerate.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Boilerate.Tests;

[Collection("Foundation")]
public sealed class PaymentEmailTests
{
    private const string DeliverySecret = "encrypted-account-password-must-never-enter-email";

    private static OutboxProcessor Processor(IServiceScope scope, IMailService mail, int attempts = 8)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<LocalFileStorageService>(),
            Options.Create(new CommerceSettings { MaximumAttempts = attempts }), NullLogger<OutboxProcessor>.Instance, mail);
    }

    private static CommerceService Catalog(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = Substitute.For<IUserService>();
        users.HasPermissionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        return new(db, new CommerceUnitOfWork(db), scope.ServiceProvider.GetRequiredService<ICurrentUser>(), users,
            Options.Create(new CommerceSettings()), scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>());
    }

    private static async Task<(Guid OrderId, Guid MessageId)> Seed(ApplicationDbContext db)
    {
        var type = new ProductType("EMAIL", "Email fixture");
        var product = new Product(type.Id, "Unique account", "email-account", fulfillmentKind: ProductFulfillmentKind.DigitalUnique);
        var variant = product.AddVariant("EMAIL-ACCOUNT", 100, null, new Dictionary<Guid, Guid>(), 200);
        var cart = new Cart(Guid.NewGuid());
        var item = new OrderItem(product.Id, variant.Id, product.Name, variant.Sku, "", ProductFulfillmentKind.DigitalUnique, 100, 1);
        var now = DateTime.UtcNow;
        var order = new Order(cart.CustomerId, cart.Id, Guid.NewGuid(), new string('A', 64), OrderPaymentMethod.Prepaid,
            "Email customer", "buyer@example.test", "0901234567", null, 0, now, 30, [item]);
        var stock = new DigitalStockItem(Guid.NewGuid(), product.Id, variant.Id, ProductFulfillmentKind.DigitalUnique,
            "EMAIL-ONE", "stock-account-password-must-never-enter-email");
        stock.Reserve(item.Id);
        stock.Deliver(item.Id);
        order.RecordPayment(now);
        order.CompleteFulfillment(true);
        var receipt = new PaymentReceipt(order.Id, Guid.NewGuid(), new string('B', 64), "EMAIL-BANK-REF", 100, "VND", now, now);
        var delivery = new DigitalDelivery(Guid.NewGuid(), order.Id, item.Id, variant.Id, stock.Id, 1,
            ProductFulfillmentKind.DigitalUnique, DeliverySecret);
        var message = OutboxMessage.DigitalDelivery(order.Id, Guid.NewGuid());
        db.AddRange(type, product, cart, order, stock, receipt, delivery, message);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (order.Id, message.MessageId);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task MailContainsOnlyAnOrderNotificationRunsAfterCommitAndIsClaimedOnce(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeded = await Seed(db);
        var mail = Substitute.For<IMailService>();
        MailRequest? sent = null;
        mail.SendAsync(Arg.Any<MailRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            Assert.Null(db.Database.CurrentTransaction);
            sent = call.Arg<MailRequest>();
            return Task.CompletedTask;
        });
        var processor = Processor(scope, mail);
        await using (var transaction = await db.Database.BeginTransactionAsync())
            await Assert.ThrowsAsync<InvalidOperationException>(() => processor.RunOneAsync(default));
        Assert.True(await processor.RunOneAsync(default));
        Assert.False(await processor.RunOneAsync(default));
        await mail.Received(1).SendAsync(Arg.Any<MailRequest>(), Arg.Any<CancellationToken>());
        Assert.NotNull(sent);
        Assert.Equal("buyer@example.test", Assert.Single(sent.To));
        Assert.Contains("Đơn hàng của tôi", sent.Body);
        Assert.Empty(sent.AttachmentData);
        Assert.Empty(sent.Bcc);
        Assert.Empty(sent.Cc);
        Assert.DoesNotContain(DeliverySecret, JsonSerializer.Serialize(sent));
        Assert.DoesNotContain("stock-account-password", JsonSerializer.Serialize(sent));
        Assert.Equal($"<outbox.{seeded.MessageId:N}@boilerate.invalid>", sent.Headers["Message-Id"]);
        var message = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal("DigitalDeliveryReady", message.Type);
        Assert.Equal(1, message.SchemaVersion);
        Assert.Equal(new DigitalDeliveryReady(seeded.OrderId), JsonSerializer.Deserialize<DigitalDeliveryReady>(message.Payload));
        Assert.DoesNotContain("buyer@example.test", message.Payload);
        Assert.DoesNotContain(DeliverySecret, message.Payload);
        Assert.Equal(OutboxState.Completed, message.State);
        Assert.Equal(1, message.Attempts);
        Assert.Null(message.LeaseToken);
        Assert.Null(message.LastError);
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task MailFailureRetriesDeadLettersAndReplaysWithoutChangingPaymentOrDeliveries(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var seeded = await Seed(db);
        var mail = Substitute.For<IMailService>();
        var requests = new List<MailRequest>();
        var failing = true;
        mail.SendAsync(Arg.Any<MailRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            Assert.Null(db.Database.CurrentTransaction);
            requests.Add(call.Arg<MailRequest>());
            return failing ? Task.FromException(new InvalidOperationException("smtp-private-error-must-not-be-logged")) : Task.CompletedTask;
        });
        var processor = Processor(scope, mail, 2);
        Assert.True(await processor.RunOneAsync(default));
        var message = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(OutboxState.Pending, message.State);
        Assert.Equal(1, message.Attempts);
        Assert.Equal("InvalidOperationException", message.LastError);
        Assert.True(message.NextAttemptAt > DateTime.UtcNow);
        Assert.False(await processor.RunOneAsync(default));
        await db.OutboxMessages.ExecuteUpdateAsync(s => s.SetProperty(m => m.NextAttemptAt, DateTime.UtcNow.AddMinutes(-1)));
        Assert.True(await processor.RunOneAsync(default));
        message = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(OutboxState.DeadLetter, message.State);
        Assert.Equal(2, message.Attempts);
        Assert.False(await processor.RunOneAsync(default));
        Assert.True(await Catalog(scope).ReplayAsync(new(seeded.MessageId), default));
        failing = false;
        Assert.True(await processor.RunOneAsync(default));
        message = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(OutboxState.Completed, message.State);
        Assert.Equal(1, message.Attempts);
        Assert.Null(message.LastError);
        Assert.Equal(3, requests.Count);
        Assert.Single(requests.Select(r => r.Headers["Message-Id"]).Distinct());
        Assert.Single(await db.PaymentReceipts.ToListAsync());
        Assert.Single(await db.DigitalDeliveries.ToListAsync());
        Assert.Equal(OrderPaymentStatus.Paid, (await db.Orders.SingleAsync()).PaymentStatus);
        Assert.Equal(DigitalStockState.Delivered, (await db.DigitalStockItems.SingleAsync()).State);
        Assert.True(await db.Trails.AnyAsync(t => t.TableName == "OutboxMessages"));
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task ExpiredLeaseAndTwoWorkersSendOnlyOneNotificationForAClaim(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using (var scope = fixture.Scope(Guid.NewGuid()))
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await Seed(db);
            await db.OutboxMessages.ExecuteUpdateAsync(s => s.SetProperty(m => m.State, OutboxState.Processing)
                .SetProperty(m => m.LeaseToken, Guid.NewGuid()).SetProperty(m => m.LeaseUntil, DateTime.UtcNow.AddMinutes(-1)));
        }
        var requests = new ConcurrentQueue<MailRequest>();
        var mail = Substitute.For<IMailService>();
        mail.SendAsync(Arg.Any<MailRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            requests.Enqueue(call.Arg<MailRequest>());
            return Task.CompletedTask;
        });
        async Task Work()
        {
            using var scope = fixture.Scope();
            await Processor(scope, mail).RunOneAsync(default);
        }
        await Task.WhenAll(Work(), Work());
        Assert.Single(requests);
        using var verification = fixture.Scope();
        var verified = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var message = await verified.OutboxMessages.SingleAsync();
        Assert.Equal(OutboxState.Completed, message.State);
        Assert.Equal(1, message.Attempts);
        Assert.Single(await verified.DigitalDeliveries.ToListAsync());
    }

    [Theory, Trait("Category", "ProviderIntegration")]
    [InlineData("mssql"), InlineData("postgresql")]
    public async Task InterruptedSendCanBeReclaimedWithoutLosingTheNotificationOrDuplicatingDelivery(string provider)
    {
        await using var fixture = new FoundationDatabase(provider, true);
        await fixture.CreateAsync();
        using var scope = fixture.Scope(Guid.NewGuid());
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await Seed(db);
        using var cancellation = new CancellationTokenSource();
        var interruptedMail = Substitute.For<IMailService>();
        interruptedMail.SendAsync(Arg.Any<MailRequest>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            cancellation.Cancel();
            return Task.FromCanceled(cancellation.Token);
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Processor(scope, interruptedMail).RunOneAsync(cancellation.Token));
        var message = await db.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(OutboxState.Processing, message.State);
        Assert.NotNull(message.LeaseToken);
        Assert.Equal(1, message.Attempts);
        await db.OutboxMessages.ExecuteUpdateAsync(s => s.SetProperty(m => m.LeaseUntil, DateTime.UtcNow.AddMinutes(-1)));
        using var restarted = fixture.Scope();
        var mail = Substitute.For<IMailService>();
        mail.SendAsync(Arg.Any<MailRequest>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        Assert.True(await Processor(restarted, mail).RunOneAsync(default));
        var restartedDb = restarted.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        message = await restartedDb.OutboxMessages.AsNoTracking().SingleAsync();
        Assert.Equal(OutboxState.Completed, message.State);
        Assert.Equal(2, message.Attempts);
        await mail.Received(1).SendAsync(Arg.Any<MailRequest>(), Arg.Any<CancellationToken>());
        Assert.Single(await restartedDb.DigitalDeliveries.ToListAsync());
        Assert.Single(await restartedDb.PaymentReceipts.ToListAsync());
    }
}
