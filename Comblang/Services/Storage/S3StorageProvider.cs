using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

namespace Comblang.Services.Storage;

/// <summary>
/// AWS S3 storage provider. Uses the AWS SDK to upload, download, and
/// delete objects in a configurable bucket.
/// </summary>
public class S3StorageProvider : IStorageProvider
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucket;
    private readonly string _publicUrlPrefix;

    /// <summary>
    /// Creates an S3 storage provider.
    /// </summary>
    /// <param name="bucket">S3 bucket name.</param>
    /// <param name="region">AWS region (e.g. us-east-1).</param>
    /// <param name="accessKey">AWS access key ID.</param>
    /// <param name="secretKey">AWS secret access key.</param>
    /// <param name="publicUrlPrefix">
    /// Optional public URL prefix (e.g. CDN domain). If empty the default S3
    /// URL is used.
    /// </param>
    public S3StorageProvider(
        string bucket,
        string region,
        string accessKey,
        string secretKey,
        string? publicUrlPrefix = null)
    {
        _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        _s3Client = new AmazonS3Client(credentials, RegionEndpoint.GetBySystemName(region));

        _publicUrlPrefix = !string.IsNullOrWhiteSpace(publicUrlPrefix)
            ? publicUrlPrefix.TrimEnd('/')
            : $"https://{bucket}.s3.{region}.amazonaws.com";
    }

    /// <inheritdoc />
    public async Task<string> UploadAsync(string fileName, Stream content, string contentType)
    {
        // Close the existing stream before reusing? No—we use the provided stream directly.
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = fileName,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
            AutoResetStreamPosition = true
        };

        await _s3Client.PutObjectAsync(request);

        var publicUrl = $"{_publicUrlPrefix}/{fileName}";
        return publicUrl;
    }

    /// <inheritdoc />
    public async Task<Stream?> DownloadAsync(string fileName)
    {
        try
        {
            var response = await _s3Client.GetObjectAsync(_bucket, fileName);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string fileName)
    {
        try
        {
            await _s3Client.DeleteObjectAsync(_bucket, fileName);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task<string> GetPublicUrlAsync(string fileName)
    {
        var url = $"{_publicUrlPrefix}/{fileName}";
        return Task.FromResult(url);
    }
}
