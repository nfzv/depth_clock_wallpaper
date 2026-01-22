using DepthClockWallpaper.Models;
using System.Text.Json;

namespace DepthClockWallpaper.Core;

public class BingImage
{
    public string Title { get; set; } = "";
    public string Copyright { get; set; } = "";
    public string Url { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public DateTime Date { get; set; }
}

public class BingWallpaperService
{
    private readonly HttpClient _httpClient;

    public BingWallpaperService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<BingImage?> GetLatestImageAsync()
    {
        try
        {
            Console.WriteLine("Fetching latest Bing wallpaper...");

            // Bing API endpoint for HP images
            var response = await _httpClient.GetStringAsync(
                "https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=en-US");

            var jsonDoc = JsonDocument.Parse(response);
            var images = jsonDoc.RootElement.GetProperty("images").EnumerateArray();

            if (!images.MoveNext())
                return null;

            var imageJson = images.Current;
            var bingImage = new BingImage
            {
                Title = imageJson.GetProperty("title").GetString() ?? "",
                Copyright = imageJson.GetProperty("copyright").GetString() ?? "",
                Url = imageJson.GetProperty("urlbase").GetString() ?? "",
                Date = DateTime.Now
            };

            // Construct full resolution URL
            bingImage.Url = $"https://www.bing.com{bingImage.Url}_1920x1080.jpg";

            // Download and cache the image
            await DownloadAndCacheImageAsync(bingImage);

            return bingImage;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch Bing image: {ex.Message}");
            return null;
        }
    }

    private async Task DownloadAndCacheImageAsync(BingImage image)
    {
        try
        {
            string filePath = WallpaperPaths.BingWallpaper;

            Console.WriteLine($"Downloading Bing image: {image.Url}");

            var imageBytes = await _httpClient.GetByteArrayAsync(image.Url);
            await File.WriteAllBytesAsync(filePath, imageBytes);

            image.ImagePath = filePath;
            Console.WriteLine($"✓ Bing image saved to: {filePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to download Bing image: {ex.Message}");
            throw;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}