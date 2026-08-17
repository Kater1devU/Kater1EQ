using System.Collections.Generic;
using Kater1EQ.Models;

namespace Kater1EQ.Services
{
    /// <summary>
    /// Danh sách link mạng xã hội CỐ ĐỊNH của tác giả app, hiển thị ở tab SOCIAL để người dùng
    /// biết đây là app do ai phát triển. Khác với thiết kế Social ban đầu (STEP 4 - link do
    /// người dùng tự nhập/lưu), danh sách này KHÔNG cho chỉnh sửa và không đọc/ghi file - đây là
    /// thông tin credit công khai của chính tác giả app, cố định trong source theo yêu cầu.
    /// </summary>
    public static class DeveloperSocialLinks
    {
        public static readonly IReadOnlyList<SocialLink> All = new List<SocialLink>
        {
            new() { Name = "Facebook",  Url = "https://www.facebook.com/patapimmmm",     IconKey = "Facebook" },
            new() { Name = "Instagram", Url = "https://www.instagram.com/qan.dev/",       IconKey = "Instagram" },
            new() { Name = "GitHub",    Url = "https://github.com/Kater1devU",            IconKey = "GitHub" },
            new() { Name = "TikTok",    Url = "https://www.tiktok.com/@kateridev",        IconKey = "TikTok" },
            new() { Name = "Steam",     Url = "https://steamcommunity.com/id/katr1n4/",   IconKey = "Steam" },
        };
    }
}
