using Boilerate.Infrastructure.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using System.Diagnostics;
using Boilerate.Application.Common.Caching;
using Boilerate.Application.Common.FileStorage;
using Boilerate.Application.Common.Persistence;
using Filter = Boilerate.Application.Common.Models.Filter;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Boilerate.Application.Common.BlobStorage;
using Boilerate.Application.Common.Exceptions;
using Boilerate.Application.Common.Models;
using Boilerate.Application.Common.Specification;
using Boilerate.Domain.Catalog;
using Boilerate.Infrastructure.BlobStorage;
using Boilerate.Infrastructure.BlobStorage.Aws;
using Boilerate.Infrastructure.BlobStorage.Azure;
using Boilerate.Infrastructure.Caching;
using Boilerate.Infrastructure.Common.Services;
using Boilerate.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace Boilerate.Tests;

public sealed class WarningSecurityRegressionTests
{
    [Fact]
    public async Task OptionalCacheOnlyIgnoresInfrastructureFailuresAndHonorsCancellation()
    {
        var backend = Substitute.For<IDistributedCache>();
        var cache = new DistributedCacheService(backend, new NewtonSoftService(), NullLogger<DistributedCacheService>.Instance);
        backend.Get("outage").Returns(_ => throw new TimeoutException("Cache server unavailable"));
        Assert.Null(cache.Get<string>("outage"));
        backend.Get("bug").Returns(_ => throw new InvalidOperationException("Programming/configuration error"));
        Assert.Throws<InvalidOperationException>(() => cache.Get<string>("bug"));
        backend.Get("redis-command").Returns(_ => throw new RedisCommandException("Invalid command"));
        Assert.Throws<RedisCommandException>(() => cache.Get<string>("redis-command"));
        backend.Get("redis-server").Returns(_ => throw new RedisServerException("WRONGTYPE"));
        Assert.Throws<RedisServerException>(() => cache.Get<string>("redis-server"));
        backend.Get("redis-offline").Returns(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Offline"));
        Assert.Null(cache.Get<string>("redis-offline"));
        backend.Get("corrupt").Returns(Encoding.UTF8.GetBytes("{invalid-json"));
        Assert.Null(cache.Get<Dictionary<string, string>>("corrupt"));
        backend.Received(1).Remove("corrupt");

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        backend.GetAsync("cancel", cancellation.Token).Returns(Task.FromCanceled<byte[]?>(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetAsync<string>("cancel", cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.SetAsync("cancel", "value", cancellationToken: cancellation.Token));
    }

    [Fact]
    public void CorruptCacheLoggingDoesNotIncludeTheCachedValueOrConversionException()
    {
        var backend = Substitute.For<IDistributedCache>();
        var logger = Substitute.For<ILogger<DistributedCacheService>>();
        var cache = new DistributedCacheService(backend, new NewtonSoftService(), logger);
        backend.Get("sensitive").Returns(Encoding.UTF8.GetBytes("\"private-cached-value\""));
        Assert.Equal(0, cache.Get<int>("sensitive"));
        var log = Assert.Single(logger.ReceivedCalls().Where(call => call.GetMethodInfo().Name == "Log"));
        Assert.Null(log.GetArguments()[3]);
        Assert.DoesNotContain("private-cached-value", log.GetArguments()[2]!.ToString());
        backend.Received(1).Remove("sensitive");
    }

    [Fact]
    public async Task LocalUploadsHandleShortReadsAndRejectMisreportedLength()
    {
        using var files = new TemporaryImages();
        byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        using var input = new OneByteReadStream(signature);
        var upload = new FormFile(input, 0, signature.Length, "image", "valid.png");
        string image = await files.Storage.UploadAsync<Product>(upload, Boilerate.Domain.Common.FileType.Image);
        using Stream result = files.Storage.OpenRead(image);
        Assert.Equal(signature.Length, result.Length);

        using var oversized = new MemoryStream(new byte[5 * 1024 * 1024 + 1]);
        await oversized.WriteAsync(signature); oversized.Position = 0;
        var dishonest = Substitute.For<IFormFile>();
        dishonest.FileName.Returns("oversized.png"); dishonest.Length.Returns(8);
        dishonest.OpenReadStream().Returns(oversized);
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => files.Storage.UploadAsync<Product>(dishonest, Boilerate.Domain.Common.FileType.Image));
        Assert.Single(Directory.GetFiles(files.Root, "*.png", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("Files/../outside.png")]
    [InlineData("Files/Images/item.png:secret")]
    [InlineData("Files/Images/item.svg")]
    public void StorageRejectsTraversalAlternateStreamsAndNonImageReferences(string path)
    {
        using var files = new TemporaryImages();
        Assert.Throws<ForbiddenException>(() => files.Storage.OpenRead(path));
        Assert.Throws<ForbiddenException>(() => files.Storage.Remove(path));
    }

    [Fact]
    public async Task S3NoOverwriteIsAtomicAndOnly404MeansAbsent()
    {
        var sdk = Substitute.For<IAmazonS3>();
        var options = Options.Create(new BlobStorageSettings { Aws = new AwsStorageSettings { BucketName = "test-bucket", Region = "us-east-1" } });
        var storage = new AwsS3StorageService(sdk, options, NullLogger<AwsS3StorageService>.Instance);
        using var stream = new MemoryStream("data"u8.ToArray());
        string url = await storage.UploadAsync(new UploadBlobRequest { ContainerName = "tenant", BlobName = "a b#c.txt", Data = stream, Overwrite = false });
        await sdk.Received(1).PutObjectAsync(Arg.Is<PutObjectRequest>(r => r.IfNoneMatch == "*" && !r.AutoCloseStream), Arg.Any<CancellationToken>());
        Assert.Contains("a%20b%23c.txt", url);
        Assert.True(stream.CanRead);

        var denied = new AmazonS3Exception("Access denied") { StatusCode = System.Net.HttpStatusCode.Forbidden };
        sdk.GetObjectMetadataAsync("test-bucket", "tenant/blob", Arg.Any<CancellationToken>()).Returns(Task.FromException<GetObjectMetadataResponse>(denied));
        var failure = await Assert.ThrowsAsync<InternalServerException>(() => storage.ExistsAsync("tenant", "blob"));
        Assert.Same(denied, failure.InnerException);
        sdk.GetObjectMetadataAsync("test-bucket", "tenant/missing", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<GetObjectMetadataResponse>(new AmazonS3Exception("Missing") { StatusCode = System.Net.HttpStatusCode.NotFound }));
        Assert.False(await storage.ExistsAsync("tenant", "missing"));
        await Assert.ThrowsAsync<NotSupportedException>(() => storage.CreateContainerAsync("tenant", isPublic: true));
    }

    [Fact]
    public async Task S3ListingSeparatesContainerPrefixes()
    {
        var sdk = Substitute.For<IAmazonS3>();
        sdk.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ListObjectsV2Response { S3Objects = new List<S3Object>() }));
        var storage = new AwsS3StorageService(sdk, Options.Create(new BlobStorageSettings { Aws = new AwsStorageSettings { BucketName = "test-bucket", Region = "us-east-1" } }), NullLogger<AwsS3StorageService>.Instance);
        await storage.ListBlobsAsync("tenant");
        await sdk.Received(1).ListObjectsV2Async(Arg.Is<ListObjectsV2Request>(r => r.Prefix == "tenant/"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task S3ContainerListingFollowsContinuationAndDoesNotInventUrlsForLegacyDotKeys()
    {
        var sdk = Substitute.For<IAmazonS3>();
        sdk.ListObjectsV2Async(Arg.Any<ListObjectsV2Request>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<ListObjectsV2Request>();
            return Task.FromResult(request.Delimiter == "/"
                ? request.ContinuationToken is null
                    ? new ListObjectsV2Response { CommonPrefixes = new() { "first/" }, IsTruncated = true, NextContinuationToken = "next-page" }
                    : new ListObjectsV2Response { CommonPrefixes = new() { "second/" } }
                : new ListObjectsV2Response { S3Objects = new() { new() { Key = "tenant/a/../old.txt", LastModified = DateTime.UtcNow } } });
        });
        var storage = new AwsS3StorageService(sdk, Options.Create(new BlobStorageSettings { Aws = new AwsStorageSettings { BucketName = "test-bucket", Region = "us-east-1" } }), NullLogger<AwsS3StorageService>.Instance);
        Assert.Equal(new[] { "first", "second" }, await storage.ListContainersAsync());
        var legacy = Assert.Single(await storage.ListBlobsAsync("tenant"));
        Assert.Equal("a/../old.txt", legacy.Name);
        Assert.Null(legacy.Url);
    }

    [Theory]
    [InlineData("a/../file.txt")]
    [InlineData("./file.txt")]
    [InlineData("folder/.")]
    public async Task S3RejectsDotSegmentsBeforeWritingOrGeneratingUrls(string name)
    {
        var sdk = Substitute.For<IAmazonS3>();
        var storage = new AwsS3StorageService(sdk, Options.Create(new BlobStorageSettings { Aws = new AwsStorageSettings { BucketName = "test-bucket", Region = "us-east-1" } }), NullLogger<AwsS3StorageService>.Instance);
        using var data = new MemoryStream("data"u8.ToArray());
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => storage.UploadAsync(new() { ContainerName = "tenant", BlobName = name, Data = data }));
        await sdk.DidNotReceive().PutObjectAsync(Arg.Any<PutObjectRequest>(), Arg.Any<CancellationToken>());
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => storage.GetBlobUrlAsync("tenant", name));
    }

    [Fact]
    public async Task AzureNoOverwriteUsesConditionalWriteAndReportsConflicts()
    {
        var client = Substitute.For<BlobServiceClient>();
        var container = Substitute.For<BlobContainerClient>();
        var blob = Substitute.For<BlobClient>();
        client.GetBlobContainerClient("tenant").Returns(container); container.GetBlobClient("blob").Returns(blob);
        blob.Uri.Returns(new Uri("https://test.invalid/tenant/blob"));
        var settings = Options.Create(new BlobStorageSettings { Azure = new AzureStorageSettings { ConnectionString = "UseDevelopmentStorage=true" } });
        var storage = new AzureBlobStorageService(settings, NullLogger<AzureBlobStorageService>.Instance, client);
        using var data = new MemoryStream("data"u8.ToArray());
        await storage.UploadAsync(new UploadBlobRequest { ContainerName = "tenant", BlobName = "blob", Data = data, Overwrite = false });
        await blob.Received(1).UploadAsync(Arg.Any<Stream>(), Arg.Is<BlobUploadOptions>(o => o.Conditions!.IfNoneMatch == ETag.All), Arg.Any<CancellationToken>());
        blob.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<Response<BlobContentInfo>>(new RequestFailedException(412, "Precondition failed")));
        await Assert.ThrowsAsync<ConflictException>(() => storage.UploadAsync(new UploadBlobRequest { ContainerName = "tenant", BlobName = "blob", Data = data, Overwrite = false }));
        blob.ExistsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<Response<bool>>(new RequestFailedException(403, "Denied")));
        await Assert.ThrowsAsync<InternalServerException>(() => storage.ExistsAsync("tenant", "blob"));
    }

    [Theory]
    [InlineData(404)]
    [InlineData(403)]
    public async Task AzureDownloadMapsMissingObjectsAndDisposesFailedBuffers(int status)
    {
        var client = Substitute.For<BlobServiceClient>();
        var container = Substitute.For<BlobContainerClient>();
        var blob = Substitute.For<BlobClient>();
        client.GetBlobContainerClient("tenant").Returns(container);
        container.GetBlobClient("blob").Returns(blob);
        Stream? buffer = null;
        blob.DownloadToAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            buffer = call.Arg<Stream>();
            return Task.FromException<Response>(new RequestFailedException(status, "Provider details"));
        });
        var settings = Options.Create(new BlobStorageSettings { Azure = new AzureStorageSettings { ConnectionString = "UseDevelopmentStorage=true" } });
        var storage = new AzureBlobStorageService(settings, NullLogger<AzureBlobStorageService>.Instance, client);
        if (status == 404)
        {
            await Assert.ThrowsAsync<NotFoundException>(() => storage.DownloadAsync("tenant", "blob"));
        }
        else
        {
            await Assert.ThrowsAsync<InternalServerException>(() => storage.DownloadAsync("tenant", "blob"));
        }

        Assert.NotNull(buffer);
        Assert.False(buffer.CanRead);
        await blob.DidNotReceive().ExistsAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Name sideways")]
    [InlineData("Name asc extra")]
    [InlineData("Name")]
    public void InvalidOrDuplicateOrderingFieldsAreValidationErrors(string? ordering)
    {
        string[] fields = ordering == "Name" ? new[] { "Name", "Name desc" } : new[] { ordering! };
        Assert.Throws<FluentValidation.ValidationException>(() => new EntitiesByPaginationFilterSpec<Product>(new PaginationFilter { PageNumber = 1, PageSize = 10, OrderBy = fields }));
    }

    [Fact]
    public void FiltersRejectUnboundedTreesAndInvalidValuesAndHandleNullStrings()
    {
        var empty = new Filter { Logic = FilterLogic.AND, Filters = new() };
        Assert.Throws<FluentValidation.ValidationException>(() => new EntitiesByBaseFilterSpec<Product>(new BaseFilter { AdvancedFilter = empty }));
        var recursive = new Filter { Logic = FilterLogic.AND, Filters = new() }; recursive.Filters.Add(recursive);
        Assert.Throws<FluentValidation.ValidationException>(() => new EntitiesByBaseFilterSpec<Product>(new BaseFilter { AdvancedFilter = recursive }));
        Assert.Throws<FluentValidation.ValidationException>(() => new EntitiesByBaseFilterSpec<Boilerate.Domain.Catalog.ProductVariant>(new BaseFilter
            { AdvancedFilter = new() { Field = "Price", Operator = "eq", Value = "not-a-price" } }));
        Assert.Throws<FluentValidation.ValidationException>(() => new EntitiesByBaseFilterSpec<Boilerate.Domain.Catalog.ProductVariant>(new BaseFilter
            { AdvancedFilter = new() { Field = "Price", Operator = "contains", Value = 12 } }));
        var products = new[] { new Product(Guid.NewGuid(), "Null", "null"), new Product(Guid.NewGuid(), "Match", "match", "Needle") };
        var filter = new EntitiesByBaseFilterSpec<Product>(new BaseFilter { AdvancedFilter = new() { Field = "Description", Operator = "contains", Value = "needle" } });
        Assert.Equal("Match", Assert.Single(filter.Evaluate(products)).Name);
    }

    [Fact]
    public async Task StorageRejectsDirectoryLinksForReadsDeletesAndUploads()
    {
        using var files = new TemporaryImages();
        string outside = Path.Combine(files.BaseDirectory, "outside");
        Directory.CreateDirectory(outside);
        string secret = Path.Combine(outside, "secret.png");
        await File.WriteAllTextAsync(secret, "outside storage");
        string link = Path.Combine(files.Root, "Images");
        if (OperatingSystem.IsWindows())
        {
            var info = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-Command", "New-Item -ItemType Junction -Path $env:BOILERATE_LINK_PATH -Target $env:BOILERATE_LINK_TARGET | Out-Null" })
            {
                info.ArgumentList.Add(argument);
            }

            info.Environment["BOILERATE_LINK_PATH"] = link;
            info.Environment["BOILERATE_LINK_TARGET"] = outside;
            using Process process = Process.Start(info)!;
            try
            {
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            }
            catch
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }

                throw;
            }

            Assert.Equal(0, process.ExitCode);
        }
        else
        {
            Directory.CreateSymbolicLink(link, outside);
        }

        try
        {
            Assert.Throws<ForbiddenException>(() => files.Storage.OpenRead("Files/Images/secret.png"));
            Assert.Throws<ForbiddenException>(() => files.Storage.Remove("Files/Images/secret.png"));
            using var image = new MemoryStream(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            await Assert.ThrowsAsync<ForbiddenException>(() => files.Storage.UploadAsync<Product>(new FormFile(image, 0, image.Length, "image", "image.png"), Boilerate.Domain.Common.FileType.Image));
            Assert.Equal("outside storage", await File.ReadAllTextAsync(secret));
            Assert.False(Directory.Exists(Path.Combine(outside, nameof(Product))));
        }
        finally
        {
            // Remove only the link itself; the target remains available for the assertions above.
            Directory.Delete(link);
        }
    }

    [Theory]
    [InlineData("23505", 409)]
    [InlineData("23503", 409)]
    [InlineData("08006", 500)]
    public async Task ErrorBoundaryDistinguishesConflictsFromDatabaseFailuresAndHidesDetails(string state, int expectedStatus)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        var middleware = new ExceptionMiddleware(NullLogger<ExceptionMiddleware>.Instance, environment);
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services, TraceIdentifier = "review-trace" };
        using var output = new MemoryStream();
        context.Response.Body = output;
        var databaseError = new DbUpdateException("Private database details", new PostgresException("Private server details", "ERROR", "ERROR", state));
        await middleware.InvokeAsync(context, _ => Task.FromException(databaseError));
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        string body = Encoding.UTF8.GetString(output.ToArray());
        Assert.Contains("review-trace", body);
        Assert.DoesNotContain("Private", body);
    }

    [Theory]
    [InlineData(400, true)]
    [InlineData(500, false)]
    public async Task ProductionBoundaryExplainsRejectedDomainRulesAndHidesServerFailures(int status, bool includesDetail)
    {
        const string explanation = "Active SKUs require prices before publication.";
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Production);
        var middleware = new ExceptionMiddleware(NullLogger<ExceptionMiddleware>.Instance, environment);
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        using var output = new MemoryStream();
        context.Response.Body = output;
        var rejected = new Boilerate.Domain.Common.Exceptions.DomainException(explanation, statusCode: (System.Net.HttpStatusCode)status);

        await middleware.InvokeAsync(context, _ => Task.FromException(rejected));

        Assert.Equal(status, context.Response.StatusCode);
        using var body = System.Text.Json.JsonDocument.Parse(output.ToArray());
        if (includesDetail)
            Assert.Equal(explanation, body.RootElement.GetProperty("detail").GetString());
        else
            Assert.DoesNotContain(explanation, body.RootElement.GetRawText());
    }

    private sealed class OneByteReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }

    private sealed class TemporaryImages : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "boilerate_warning_" + Guid.NewGuid().ToString("N"));
        public TemporaryImages()
        {
            Root = Path.Combine(_directory, "Files"); Directory.CreateDirectory(Root);
            Storage = new LocalFileStorageService(Root);
        }

        public string BaseDirectory => _directory;

        public string Root { get; }
        public LocalFileStorageService Storage { get; }

        public void Dispose()
        {
            string target = Path.GetFullPath(_directory);
            string expected = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(expected, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(target).StartsWith("boilerate_warning_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unsafe test cleanup path.");
            }

            Directory.Delete(target, recursive: true);
        }
    }
}
