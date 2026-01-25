using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DepthClockWallpaper.Models;
public interface IWritableOptions<T>
{
    T Value { get; }
    Task UpdateAsync(Action<T> applyChanges);


}

public class WritableJsonOptions<T> : IWritableOptions<T>
    where T : class, new()
{
    private readonly string _filePath;
    private readonly IConfigurationRoot _config;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _lock = new(1, 1);
    public WritableJsonOptions(
        IConfigurationRoot config,
        string filePath)
    {
        _config = config;
        _filePath = filePath;
        _jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    }

    public T Value => _config.Get<T>() ?? throw new InvalidOperationException("Failed to load config.json");

    public async Task UpdateAsync(Action<T> applyChanges)
    {
        await _lock.WaitAsync();
        try
        {
            var current = Value ?? new T();
            applyChanges(current);

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(
                current, _jsonOptions
                );

            await File.WriteAllTextAsync(_filePath, json);

            _config.Reload();
        }
        finally
        {
            _lock.Release();
        }
    }


}
