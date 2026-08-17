namespace Kater1EQ.Models
{
    /// <summary>
    /// Một liên kết mạng xã hội. Hiện dùng cho danh sách link cố định của tác giả app
    /// (xem Services/DeveloperSocialLinks.cs) - hiển thị ở tab SOCIAL để credit người làm app.
    /// </summary>
    public class SocialLink
    {
        public string Name { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        /// <summary>Khoá nhận diện icon tương ứng (vd "Discord", "GitHub"...).</summary>
        public string IconKey { get; set; } = string.Empty;
    }
}
