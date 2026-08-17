using System;
using System.IO;
using System.Text.Json;
using Kater1EQ.Models;

namespace Kater1EQ.Services
{
    /// <summary>
    /// Đọc/ghi Settings.json tại %AppData%\Kater1EQ\settings.json (STEP 10). Theo đúng pattern
    /// JSON persist của PresetService/SocialService trong project này: đọc mềm dẻo (không throw
    /// nếu file hỏng/thiếu), ghi đè an toàn.
    /// </summary>
    public class SettingsService
    {
        private readonly string _settingsPath;

        public SettingsService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(appData, "Kater1EQ");
            Directory.CreateDirectory(folder);
            _settingsPath = Path.Combine(folder, "settings.json");
        }

        /// <summary>
        /// Load settings từ đĩa. Nếu file chưa tồn tại hoặc bị hỏng (không parse được), trả về
        /// Settings mặc định (Theme = Pixel) thay vì throw — tránh crash app lúc khởi động.
        /// </summary>
        public Settings Load()
        {
            if (!File.Exists(_settingsPath))
                return new Settings();

            try
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
            catch (JsonException)
            {
                return new Settings();
            }
        }

        public void Save(Settings settings)
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
    }
}
