namespace VolumeMount.BlazorWeb.Services;

public interface IPhotoDeleteService
{
    Task<List<UploadedFileInfo>> GetUploadedFilesAsync();
    Task DeletePhotosAsync(List<string> fileNames);
}

public class UploadedFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public DateTime UploadDate { get; set; }
}
