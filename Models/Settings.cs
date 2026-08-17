namespace Kater1EQ.Models
{
    /// <summary>
    /// Theme hiện tại của app. Giá trị hợp lệ để lưu vào settings.json: Pixel, Dark, Pink.
    /// </summary>
    public enum AppTheme
    {
        Pixel,
        Dark,
        Pink
    }

    /// <summary>
    /// Settings toàn app, persist tại %AppData%\Kater1EQ\settings.json (STEP 10).
    /// Theo đúng pattern JSON của PresetService/SocialService — model đơn giản, serialize
    /// trực tiếp bằng System.Text.Json.
    /// </summary>
    public class Settings
    {
        // Mặc định = Pink (nền trắng-hồng) theo yêu cầu Kat - người dùng lần đầu mở app (chưa có
        // settings.json) sẽ thấy ngay giao diện trắng-hồng thay vì Pixel tối.
        public AppTheme Theme { get; set; } = AppTheme.Pink;
    }
}
