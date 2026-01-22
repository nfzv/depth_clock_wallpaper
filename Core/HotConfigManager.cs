using DepthClockWallpaper.Models;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DepthClockWallpaper.Core
{

    /// <summary>
    /// Configuration manager with hot-reload capability
    /// </summary>
    public static class HotConfigManager
    {
        private static readonly JsonSerializerOptions settings = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        private static AppConfig? _currentConfig;
        private static readonly object _configLock = new object();
        private static event Action<AppConfig>? _configChanged;

        public static event Action<AppConfig>? ConfigChanged
        {
            add
            {
                lock (_configLock)
                {
                    _configChanged += value;
                }
            }
            remove
            {
                lock (_configLock)
                {
                    _configChanged -= value;
                }
            }
        }

        public static AppConfig Current
        {
            get
            {
                lock (_configLock)
                {
                    if (_currentConfig == null)
                        _currentConfig = LoadFromFile();
                    return _currentConfig;
                }
            }
        }

        public static void UpdateConfig(Action<AppConfig> updateAction)
        {
            lock (_configLock)
            {
                if (_currentConfig == null)
                    _currentConfig = LoadFromFile();

                var oldConfig = CloneConfig(_currentConfig!);

                // Apply updates
                updateAction(_currentConfig!);

                // Save to file
                SaveToFile(_currentConfig!);

                // Notify all listeners of changes
                _configChanged?.Invoke(_currentConfig!);
            }
        }

        private static AppConfig LoadFromFile()
        {
            try
            {
                string configPath = "config.json";
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
                else
                {
                    // Create from example if config doesn't exist
                    var config = new AppConfig();
                    SaveToFile(config);
                    return config;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Error loading config: {ex.Message}");
                return new AppConfig();
            }
        }

        private static void SaveToFile(AppConfig config)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, settings);
                File.WriteAllText("config.json", json);

                Console.WriteLine("✓ Configuration updated and saved");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Error saving config: {ex.Message}");
            }
        }

        private static AppConfig CloneConfig(AppConfig config)
        {
            var json = JsonSerializer.Serialize(config);
            return JsonSerializer.Deserialize<AppConfig>(json)!;
        }
        /// <summary>
        /// Extracts embedded resources to temporary files if they don't exist
        /// </summary>
        public static void EnsureEmbeddedResources()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resources = new[] { "depth_anything_v2_small.onnx", "depth_anything_v2_small.onnx.data" };

            foreach (var resource in resources)
            {
                if (!File.Exists(resource))
                {
                    try
                    {
                        var resourceName = assembly.GetManifestResourceNames()
                            .FirstOrDefault(name => name.EndsWith(resource));

                        if (resourceName != null)
                        {
                            using var stream = assembly.GetManifestResourceStream(resourceName);
                            if (stream != null)
                            {
                                using var fileStream = File.Create(resource);
                                stream.CopyTo(fileStream);
                                Console.WriteLine($"✓ Extracted embedded resource: {resource}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠ Failed to extract {resource}: {ex.Message}");
                    }
                }
            }
        }

    }
}