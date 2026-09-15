using System.IO;

namespace ContosoDashboard.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(IConfiguration configuration)
    {
        var configuredRoot = configuration["DocumentStorage:RootPath"] ?? Path.Combine(AppContext.BaseDirectory, "AppData", "uploads");
        _rootPath = Path.GetFullPath(configuredRoot);

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, string relativePath)
    {
        if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("File name is required.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentException("Relative path is required.", nameof(relativePath));

        var fullPath = Path.Combine(_rootPath, relativePath.TrimStart('/', '\\'));
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var outputStream = File.Create(fullPath);
        await fileStream.CopyToAsync(outputStream);

        return relativePath;
    }

    public Task DeleteAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Task.CompletedTask;
        }

        var fullPath = Path.Combine(_rootPath, filePath.TrimStart('/', '\\'));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public async Task<Stream> DownloadAsync(string filePath)
    {
        var fullPath = Path.Combine(_rootPath, filePath.TrimStart('/', '\\'));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Document file was not found.", fullPath);
        }

        return await Task.FromResult<Stream>(File.OpenRead(fullPath));
    }

    public Task<string> GetUrlAsync(string filePath, TimeSpan expiration)
    {
        var relative = filePath.TrimStart('/', '\\');
        return Task.FromResult($"/documents/download?path={Uri.EscapeDataString(relative)}");
    }
}
