using Boilerate.Domain.Catalog;
using Boilerate.Domain.Common.Contracts;
using Boilerate.Domain.Common.Exceptions;
namespace Boilerate.Domain.Media;

public enum MediaState
{
    Uploading = 1, Ready = 2, Deleting = 3, Deleted = 4
}
public sealed class MediaAsset : CommerceEntity, IAggregateRoot
{
    public string StorageKey { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long Size
    {
        get; private set;
    }
    public MediaState State { get; private set; } = MediaState.Uploading;
    public DateTime ExpiresAt
    {
        get; private set;
    }
    private MediaAsset()
    {
    }
    public MediaAsset(string extension)
    {
        extension = extension.ToLowerInvariant();
        if (extension is not ".png" and not ".jpg" and not ".jpeg")
            throw new DomainException("Only PNG/JPEG images are supported.");
        StorageKey = $"Files/Commerce/{Id:N}{extension}";
        ContentType = extension == ".png" ? "image/png" : "image/jpeg";
        ExpiresAt = DateTime.UtcNow.AddHours(24);
    }
    public void CompleteUpload(long size)
    {
        if (State != MediaState.Uploading || size is <= 0 or > 5242880)
            throw new DomainException("Invalid upload state or size.");
        Size = size;
        State = MediaState.Ready;
        Touch();
    }
    public void Attach()
    {
        if (State != MediaState.Ready)
            throw new DomainException("Media is not ready for attachment.");
        Touch(); // A write conflicts with the cleaner's CAS/transaction on the same asset.
    }
    public void BeginDeletion()
    {
        if (State == MediaState.Deleted)
            return;
        State = MediaState.Deleting;
        Touch();
    }
    public void CompleteDeletion()
    {
        State = MediaState.Deleted;
        Touch();
    }
}