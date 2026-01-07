using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;
using VolumeMount.BlazorWeb.Data;

namespace VolumeMount.BlazorWeb.Services
{
    public class PhotoUploadService : IPhotoUploadService
    {
        private readonly string _uploadPath;
        private readonly ILogger<PhotoUploadService> _logger;

        public PhotoUploadService(
            IOptions<PhotoUploadConfiguration> options,
            ILogger<PhotoUploadService> logger)
        {
            _uploadPath = options?.Value?.UploadPath ?? "/uploads";
            _logger = logger;
        }

        public async Task<bool> UploadPhotoAsync(IBrowserFile photo)
        {
            try
            {              
                if (!Directory.Exists(_uploadPath))
                {
                    Directory.CreateDirectory(_uploadPath);
                }

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(photo.Name)}";
                var filePath = Path.Combine(_uploadPath, fileName);

                // Save file directly on the server
                await using var fileStream = new FileStream(filePath, FileMode.Create);
                await using var uploadStream = photo.OpenReadStream(maxAllowedSize: 10485760);
                await uploadStream.CopyToAsync(fileStream);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading photo");
                throw;                    
            }
        }
    }
}
