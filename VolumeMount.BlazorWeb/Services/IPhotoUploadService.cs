using Microsoft.AspNetCore.Components.Forms;

namespace VolumeMount.BlazorWeb.Services
{
    public interface IPhotoUploadService
    {
        Task<bool> UploadPhotoAsync(IBrowserFile photo);
    }
}
