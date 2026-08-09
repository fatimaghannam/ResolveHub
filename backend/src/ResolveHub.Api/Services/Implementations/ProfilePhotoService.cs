using Microsoft.EntityFrameworkCore;
using ResolveHub.Api.Data;
using ResolveHub.Api.DTOs.Profile;
using ResolveHub.Api.Services.Interfaces;
using ResolveHub.Api.Services.Models;

namespace ResolveHub.Api.Services.Implementations;

public sealed class ProfilePhotoService(
    ApplicationDbContext dbContext,
    IWebHostEnvironment environment,
    ILogger<ProfilePhotoService> logger) : IProfilePhotoService
{
    public const long MaximumFileSize = 5 * 1024 * 1024;
    public const string RelativeDirectory = "profile-images";

    private static readonly Dictionary<string, string> AllowedTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

    public async Task<TicketServiceResult<ProfilePhotoResponse>> UploadAsync(
        int userId, IFormFile photo, CancellationToken cancellationToken)
    {
        if (photo.Length <= 0)
            return Invalid("Please select a valid image.");
        if (photo.Length > MaximumFileSize)
            return Invalid("Profile photo must be smaller than 5 MB.");

        var extension = Path.GetExtension(Path.GetFileName(photo.FileName));
        if (!AllowedTypes.TryGetValue(extension, out var expectedMime) ||
            !string.Equals(photo.ContentType, expectedMime, StringComparison.OrdinalIgnoreCase) ||
            !await HasValidSignatureAsync(photo, extension, cancellationToken))
            return Invalid("Please select a JPG, PNG, or WebP image.");

        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == userId, cancellationToken);
        if (user is null)
            return new(TicketOperationStatus.NotFound);

        var storedName = $"profile_{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var relativePath = $"/{RelativeDirectory}/{storedName}";
        var directory = GetStorageDirectory();
        Directory.CreateDirectory(directory);
        var physicalPath = Path.Combine(directory, storedName);
        var previousPath = user.ProfileImagePath;

        try
        {
            await using (var stream = new FileStream(
                physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous))
                await photo.CopyToAsync(stream, cancellationToken);

            user.ProfileImagePath = relativePath;
            user.UpdatedDate = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            TryDelete(physicalPath);
            throw;
        }

        DeleteStoredPhoto(previousPath);
        return new(TicketOperationStatus.Success,
            new ProfilePhotoResponse(relativePath));
    }

    public async Task<TicketServiceResult<ProfilePhotoResponse>> RemoveAsync(
        int userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
            item => item.Id == userId, cancellationToken);
        if (user is null)
            return new(TicketOperationStatus.NotFound);

        var previousPath = user.ProfileImagePath;
        user.ProfileImagePath = null;
        user.UpdatedDate = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        DeleteStoredPhoto(previousPath);

        return new(TicketOperationStatus.Success,
            new ProfilePhotoResponse(null));
    }

    private string GetStorageDirectory() => Path.GetFullPath(Path.Combine(
        environment.ContentRootPath, "App_Data", "ProfileImages"));

    private void DeleteStoredPhoto(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        var fileName = Path.GetFileName(relativePath);
        var path = Path.GetFullPath(Path.Combine(GetStorageDirectory(), fileName));
        if (!path.StartsWith(GetStorageDirectory(), StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            TryDelete(path);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not delete old profile photo for user storage cleanup.");
        }
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    private static TicketServiceResult<ProfilePhotoResponse> Invalid(string message) =>
        new(TicketOperationStatus.Invalid, Message: message);

    private static async Task<bool> HasValidSignatureAsync(
        IFormFile photo, string extension, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        await using var stream = photo.OpenReadStream();
        var read = await stream.ReadAsync(header, cancellationToken);
        if (read < 4) return false;

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => read >= 8 && header.AsSpan(0, 8).SequenceEqual(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".webp" => read >= 12 &&
                header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
    }
}
