# 🗄️ Storage Providers

## Overview

JuraganKost mendukung 4 provider penyimpanan file melalui interface `IStorageProvider`.

## Interface

```csharp
public interface IStorageProvider
{
    Task<string> UploadAsync(string fileName, Stream content, string contentType);
    Task<bool> DeleteAsync(string fileKey);
    Task<Stream?> DownloadAsync(string fileKey);
    string GetPublicUrl(string fileKey);
    Task<bool> ExistsAsync(string fileKey);
    Task<List<string>> ListAsync(string? prefix = null);
}
```

## Provider

### 1. 📁 FileSystem (Default)

Menyimpan file di folder lokal `wwwroot/uploads`.

```json
"StorageProvider": "FileSystem",
"StorageConfig": {
  "FileSystem": { "Path": "wwwroot/uploads" }
}
```

- URL file: `/uploads/{guid}.jpg`
- Auto-create folder saat startup
- Cocok untuk development

### 2. ☁️ Azure Blob Storage

```json
"StorageProvider": "AzureBlob",
"StorageConfig": {
  "AzureBlob": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...",
    "ContainerName": "juragankost"
  }
}
```

- Auto-create container dengan public access
- URL: `https://{account}.blob.core.windows.net/{container}/{guid}.jpg`

### 3. 🪣 AWS S3

```json
"StorageProvider": "S3",
"StorageConfig": {
  "S3": {
    "AccessKey": "AKIA...",
    "SecretKey": "...",
    "BucketName": "juragankost",
    "Region": "ap-southeast-1"
  }
}
```

- Juga kompatibel dengan S3-compatible services (DigitalOcean Spaces, dll)
- Tambah `ServiceUrl` untuk custom endpoint

### 4. 🏠 MinIO

```json
"StorageProvider": "MinIO",
"StorageConfig": {
  "MinIO": {
    "Endpoint": "localhost:9000",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin",
    "BucketName": "juragankost",
    "UseSsl": false
  }
}
```

- Self-hosted S3-compatible storage
- URL: `http://localhost:9000/juragankost/{guid}.jpg`

## Dependency Injection

```csharp
// Program.cs
builder.Services.AddStorageProvider(builder.Configuration);

// StorageServiceExtensions.cs
public static IServiceCollection AddStorageProvider(this IServiceCollection services, IConfiguration config)
{
    var provider = config.GetValue<string>("StorageProvider") ?? "FileSystem";
    switch (provider)
    {
        case "AzureBlob": services.AddSingleton<IStorageProvider, AzureBlobStorageProvider>(); break;
        case "S3": services.AddSingleton<IStorageProvider, S3StorageProvider>(); break;
        case "MinIO": services.AddSingleton<IStorageProvider, MinIOStorageProvider>(); break;
        default: services.AddSingleton<IStorageProvider, FileSystemStorageProvider>(); break;
    }
}
```

## Penggunaan di Chat

Chat.razor menggunakan storage untuk upload gambar/dokumen:

```csharp
// Upload file via InputFile
await using var stream = file.OpenReadStream(10 * 1024 * 1024);
var url = await Storage.UploadAsync(file.Name, stream, file.ContentType);

// Gambar → ImageContent ke AI
// Dokumen → URL disertakan di text message
```

## API Endpoints

| Method | URL | Deskripsi |
|---|---|---|
| `POST` | `/api/v1/storage/upload` | Upload file (multipart) |
| `GET` | `/api/v1/storage/list?prefix=` | List files |
| `DELETE` | `/api/v1/storage/{fileKey}` | Delete file |
