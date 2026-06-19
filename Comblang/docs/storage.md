# 📦 Storage Configuration — Dokumentasi

## Provider yang Didukung

Comblang mendukung **4 storage backend** yang bisa dipilih melalui `appsettings.json`:

| Provider | Key | Use Case |
|----------|-----|----------|
| **FileSystem** | `FileSystem` | Development lokal |
| **AWS S3** | `S3` | Production cloud (AWS) |
| **Azure Blob** | `AzureBlob` | Production cloud (Azure) |
| **MinIO** | `MinIO` | Self-hosted / on-premises |

---

## 1. FileSystem (Default)

Cocok untuk **development**. File disimpan di folder `wwwroot/uploads/`.

```json
{
  "Storage": {
    "Provider": "FileSystem",
    "BasePath": "wwwroot/uploads",
    "BaseUrl": "/uploads"
  }
}
```

| Setting | Default | Deskripsi |
|---------|---------|-----------|
| `BasePath` | `wwwroot/uploads` | Path relatif dari root proyek |
| `BaseUrl` | `/uploads` | Public URL prefix |

**Kelebihan**: Tanpa konfigurasi, langsung jalan.
**Kekurangan**: Tidak scalable, tidak untuk production multi-server.

---

## 2. AWS S3

```json
{
  "Storage": {
    "Provider": "S3",
    "S3": {
      "Bucket": "comblang-prod",
      "Region": "ap-southeast-1",
      "AccessKey": "AKIAXXXXXXXXXXXX",
      "SecretKey": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx",
      "PublicUrl": "https://cdn.example.com"
    }
  }
}
```

| Setting | Required | Deskripsi |
|---------|----------|-----------|
| `Bucket` | ✅ | Nama S3 bucket |
| `Region` | ✅ | AWS region (e.g. `ap-southeast-1`) |
| `AccessKey` | ✅ | AWS IAM Access Key |
| `SecretKey` | ✅ | AWS IAM Secret Key |
| `PublicUrl` | ❌ | Custom CDN/CloudFront URL |

**Setup IAM Policy minimal:**
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["s3:PutObject", "s3:GetObject", "s3:DeleteObject"],
      "Resource": "arn:aws:s3:::comblang-prod/*"
    }
  ]
}
```

---

## 3. Azure Blob Storage

```json
{
  "Storage": {
    "Provider": "AzureBlob",
    "AzureBlob": {
      "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net",
      "Container": "comblang",
      "PublicUrl": "https://cdn.example.com"
    }
  }
}
```

| Setting | Required | Deskripsi |
|---------|----------|-----------|
| `ConnectionString` | ✅ | Azure Storage connection string |
| `Container` | ✅ | Blob container name (auto-created) |
| `PublicUrl` | ❌ | Custom CDN URL |

**Mendapatkan Connection String:**
1. Buka Azure Portal → Storage Account
2. Security + networking → Access keys
3. Copy salah satu connection string

---

## 4. MinIO (Self-Hosted S3)

```json
{
  "Storage": {
    "Provider": "MinIO",
    "MinIO": {
      "Endpoint": "localhost:9000",
      "AccessKey": "minioadmin",
      "SecretKey": "minioadmin",
      "Bucket": "comblang",
      "UseSsl": false,
      "PublicUrl": "http://localhost:9000/comblang"
    }
  }
}
```

| Setting | Required | Deskripsi |
|---------|----------|-----------|
| `Endpoint` | ✅ | MinIO server address (`host:port`) |
| `AccessKey` | ✅ | MinIO access key |
| `SecretKey` | ✅ | MinIO secret key |
| `Bucket` | ✅ | Bucket name (auto-created) |
| `UseSsl` | ❌ | HTTPS (default: `false`) |
| `PublicUrl` | ❌ | Public URL prefix |

**Menjalankan MinIO dengan Docker:**
```bash
docker run -d \
  --name minio \
  -p 9000:9000 \
  -p 9001:9001 \
  -e MINIO_ROOT_USER=minioadmin \
  -e MINIO_ROOT_PASSWORD=minioadmin \
  minio/minio server /data --console-address ":9001"
```

---

## StorageProviderFactory

Factory otomatis memilih provider berdasarkan konfigurasi:

```csharp
// Di Program.cs
builder.Services.AddSingleton<IStorageProvider>(_ =>
    StorageProviderFactory.Create(builder.Configuration, builder.Environment.ContentRootPath));
```

Untuk menambah provider baru:
1. Implementasikan `IStorageProvider`
2. Tambahkan case di `StorageProviderFactory.Create()`
3. Tambahkan konfigurasi di `appsettings.json`

---

## Interface

Semua provider mengimplementasikan interface yang sama:

```csharp
public interface IStorageProvider
{
    Task<string> UploadAsync(string fileName, Stream content, string contentType);
    Task<Stream?> DownloadAsync(string fileName);
    Task<bool> DeleteAsync(string fileName);
    Task<string> GetPublicUrlAsync(string fileName);
}
```

Ini memungkinkan **hot-swap** storage backend tanpa mengubah kode aplikasi!
