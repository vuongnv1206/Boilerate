namespace Boilerate.Infrastructure.BlobStorage.Aws;

public class AwsStorageSettings
{
    public string AccessKey { get; set; } = default!;
    public string SecretKey { get; set; } = default!;
    public string Region { get; set; } = default!;
    public string BucketName { get; set; } = default!;
}
