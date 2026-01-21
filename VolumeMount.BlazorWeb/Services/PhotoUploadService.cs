using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Options;
using VolumeMount.BlazorWeb.Data;

namespace VolumeMount.BlazorWeb.Services
{
    public class PhotoUploadService : IPhotoUploadService
    {
        private readonly string _uploadPath;
        private readonly ILogger<PhotoUploadService> _logger;
        private readonly string _placeholderImage;
        public string PlaceholderImage => _placeholderImage;
        public PhotoUploadService(
            IOptions<PhotoUploadConfiguration> options,
            ILogger<PhotoUploadService> logger)
        {
            _uploadPath = options?.Value?.UploadPath ?? "/uploads";
            _logger = logger;
            _placeholderImage = GetPlaceholderImage();
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

        private string GetPlaceholderImage() 
        {
            var placeholderPath = Path.Combine(Environment.CurrentDirectory, "wwwroot", "placeholder-image.jpg");
            if (File.Exists(placeholderPath))
            {
                var imageBytes = File.ReadAllBytes(placeholderPath);
                var base64String = Convert.ToBase64String(imageBytes);
                return $"data:image/jpeg;base64,{base64String}";
            } else 
            throw new FileNotFoundException("Placeholder image not found.", placeholderPath);
        }

    }
}
