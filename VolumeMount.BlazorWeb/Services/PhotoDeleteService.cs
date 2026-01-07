using Microsoft.Extensions.Options;

namespace VolumeMount.BlazorWeb.Services;

public class PhotoDeleteService : IPhotoDeleteService
{
    private readonly string _uploadPath;
    private readonly ILogger<PhotoDeleteService> _logger;

    public PhotoDeleteService(
        IOptions<PhotoUploadConfiguration> options,
        ILogger<PhotoDeleteService> logger)
    {
        _uploadPath = options?.Value?.UploadPath ?? "/uploads";
        _logger = logger;
    }

    public async Task<List<UploadedFileInfo>> GetUploadedFilesAsync()
    {
        try
        {
            if (!Directory.Exists(_uploadPath))
            {
                return new List<UploadedFileInfo>();
            }

            var files = Directory.GetFiles(_uploadPath)
                .Select(filePath => new FileInfo(filePath))
                .OrderByDescending(f => f.CreationTime)
                .Select(f => new UploadedFileInfo
                {
                    FileName = f.Name,
                    DisplayName = f.Name,
                    ThumbnailUrl = GetFileDataUrl(f.FullName),
                    UploadDate = f.CreationTime
                })
                .ToList();

            return await Task.FromResult(files);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting uploaded files");
            throw;
        }
    }

    public async Task DeletePhotosAsync(List<string> fileNames)
    {
        try
        {
            foreach (var fileName in fileNames)
            {
                var filePath = Path.Combine(_uploadPath, fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted file: {FileName}", fileName);
                }
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting photos");
            throw;
        }
    }

    private string GetFileDataUrl(string filePath)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var mimeType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/png"
            };
            return $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file for thumbnail: {FilePath}", filePath);
            return string.Empty;
        }
    }
}
