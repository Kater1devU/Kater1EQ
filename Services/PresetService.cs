using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Kater1EQ.Models;

namespace Kater1EQ.Services
{
    public class PresetService
    {
        private readonly string _presetFolder;

        /// <summary>
        /// Tên các preset mặc định của ứng dụng (được seed sẵn) - những preset này không được
        /// phép xoá, chỉ để tránh người dùng lỡ tay mất hết preset gốc; vẫn cho phép chỉnh sửa
        /// và lưu vào 1 preset MỚI khác (Save As) nếu muốn.
        /// </summary>
        private static readonly HashSet<string> DefaultPresetNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Flat", "Bass Boost", "Gaming - Footsteps", "Vocal Clarity", "PUBG", "CS2", "Pop",
            "Valorant", "Apex Legends", "Warzone",
            "Rock", "EDM", "Hip-Hop", "Classical", "Jazz", "Acoustic",
            "Movie", "Podcast", "Night Mode",
            "League of Legends", "Dota 2", "Minecraft",
            "Metal", "Lo-fi"
        };

        public PresetService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _presetFolder = Path.Combine(appData, "Kater1EQ", "Presets");
            Directory.CreateDirectory(_presetFolder);

            SeedDefaultPresetsIfEmpty();
        }

        public bool IsDefaultPreset(string name) => DefaultPresetNames.Contains(name);

        /// <summary>
        /// Danh sách tên preset mặc định (đúng 7 preset seed sẵn), dùng cho UI (STEP 9 PresetPanel)
        /// để hiển thị icon ★/lock mà không cần duplicate danh sách ở lớp UI.
        /// </summary>
        public static IReadOnlyCollection<string> DefaultPresetNameList => DefaultPresetNames;

        public bool Exists(string name) => File.Exists(PathFor(name));

        private string PathFor(string name) => Path.Combine(_presetFolder, SanitizeFileName(name) + ".json");

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        public IEnumerable<string> ListPresetNames()
        {
            return Directory.GetFiles(_presetFolder, "*.json")
                .Select(f => Load(Path.GetFileNameWithoutExtension(f) ?? string.Empty)?.Name
                             ?? Path.GetFileNameWithoutExtension(f) ?? string.Empty)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                // Preset mặc định lên đầu danh sách, còn lại theo alphabet - khớp UI mockup preset panel
                .OrderByDescending(n => IsDefaultPreset(n) ? 1 : 0)
                .ThenBy(n => n, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Load 1 preset từ đĩa. Nếu file bị hỏng/sai định dạng (vd bị ghi dở khi app tắt đột
        /// ngột giữa lúc Save, hoặc bị sửa tay sai schema), trả về null thay vì ném exception -
        /// tránh crash app lúc khởi động khi liệt kê danh sách preset (ListPresetNames).
        /// </summary>
        public EqPreset? Load(string name)
        {
            var path = PathFor(name);
            if (!File.Exists(path)) return null;

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<EqPreset>(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>Lưu preset mới hoặc ghi đè preset cùng tên đã tồn tại (Overwrite).</summary>
        public void Save(EqPreset preset)
        {
            var path = PathFor(preset.Name);
            var json = JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        /// <summary>Xoá preset. Preset mặc định của ứng dụng được bảo vệ, không thể xoá.</summary>
        /// <returns>false nếu đây là preset mặc định và bị chặn xoá.</returns>
        public bool Delete(string name)
        {
            if (IsDefaultPreset(name)) return false;
            var path = PathFor(name);
            if (File.Exists(path)) File.Delete(path);
            return true;
        }

        /// <summary>Đổi tên preset (không cho đổi tên preset mặc định để tránh lệch với danh sách bảo vệ).</summary>
        public bool Rename(string oldName, string newName)
        {
            if (IsDefaultPreset(oldName)) return false;
            if (string.IsNullOrWhiteSpace(newName)) return false;
            if (!string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase) && Exists(newName)) return false;

            var preset = Load(oldName);
            if (preset == null) return false;

            preset.Name = newName;
            Save(preset);

            var oldPath = PathFor(oldName);
            if (!string.Equals(oldPath, PathFor(newName), StringComparison.OrdinalIgnoreCase) && File.Exists(oldPath))
                File.Delete(oldPath);

            return true;
        }

        private void SeedDefaultPresetsIfEmpty()
        {
            var freqs = new[] { 31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 };

            void SeedIfMissing(string name, double[] gains)
            {
                // Seed lại nếu file chưa có HOẶC file có nhưng bị hỏng/sai schema (Load() trả
                // null) - tránh trường hợp preset mặc định "tồn tại" trên đĩa dưới dạng file lỗi
                // (vd còn sót lại từ bản cũ) nên không bao giờ được seed lại, mà cũng không đọc
                // được, khiến preset biến mất khỏi danh sách dù file vẫn nằm đó.
                if (Load(name) != null) return;

                var preset = new EqPreset { Name = name };
                for (int i = 0; i < freqs.Length; i++)
                    preset.Bands.Add(new EqBandState { FrequencyHz = freqs[i], GainDb = gains[i], Q = 0.9, Type = EqFilterType.Bell });
                Save(preset);
            }

            SeedIfMissing("Flat", new double[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
            SeedIfMissing("Bass Boost", new double[] { 6, 5, 4, 2, 0, 0, 0, 0, 0, 0 });

            // Gaming - Footsteps: chỉnh MẠNH TAY - cắt sâu sub-bass/bass để rumble không che
            // tiếng bước chân, đẩy rất mạnh dải 1-2kHz (bước chân) và 4kHz (nạp đạn/vật dụng).
            // Ưu tiên tuyệt đối "nghe rõ để chơi", không quan tâm nhạc có bị chua/mất cân bằng.
            SeedIfMissing("Gaming - Footsteps", new double[] { -8, -6, -3, 1, 5, 9, 8, 6, 4, 2 });

            SeedIfMissing("Vocal Clarity", new double[] { -1, -1, 0, 1, 3, 4, 3, 2, 1, 0 });

            // PUBG: chỉnh MẠNH TAY hơn bản cũ - cắt sâu sub-bass/bass (giảm rumble/tiếng xe cộ
            // lấn át), đẩy rất mạnh 1-2kHz (bước chân) và 2kHz (tiếng súng xa/hướng âm thanh),
            // nhấn treble rõ để định vị hướng tốt hơn khi combat tầm xa.
            SeedIfMissing("PUBG", new double[] { -10, -8, -5, -1, 3, 8, 10, 8, 5, 3 });

            // CS2: chỉnh MẠNH TAY hơn bản cũ - cắt bass sâu nhất trong các preset gaming (Source 2
            // vốn đã rõ tiếng cao), dồn toàn lực vào 2-4kHz (bước chân, nạp đạn, defuse kit, mở
            // cửa) - preset này ưu tiên tuyệt đối "nghe tin tức chiến thuật", chấp nhận nhạc/voice
            // nghe chói nếu cần.
            SeedIfMissing("CS2", new double[] { -12, -9, -6, -2, 2, 7, 11, 11, 6, 2 });

            SeedIfMissing("Pop", new double[] { 3, 2, 1, 0, -1, -1, 0, 1, 2, 2 });

            // Valorant: engine tương tự CS2 (Unreal, âm thanh rất rõ dải cao) nhưng game có nhiều
            // ability/VFX âm thanh ở dải giữa cần nghe rõ hơn CS2 - đẩy rộng hơn qua cả 1-4kHz,
            // cắt bass mạnh để giảm nhiễu từ tiếng nổ ability.
            SeedIfMissing("Valorant", new double[] { -11, -8, -5, -1, 4, 9, 10, 9, 5, 2 });

            // Apex Legends: bản đồ rộng, cần nghe bước chân/tiếng súng từ xa lẫn tiếng zipline,
            // banner phía sau - đẩy mạnh 1-4kHz như PUBG/CS2 nhưng giữ lại chút sub-bass hơn 2
            // preset kia để vẫn cảm nhận được tiếng nổ ultimate/grenade gần.
            SeedIfMissing("Apex Legends", new double[] { -7, -6, -3, 0, 4, 9, 9, 7, 4, 2 });

            // Warzone: bản đồ cực rộng, nhiều tiếng xe cộ/trực thăng (dải bass) dễ che tiếng bước
            // chân gần - cắt bass mạnh nhất trong nhóm battle royale, đẩy rất cao 2-4kHz để tách
            // tiếng bước chân khỏi tạp âm chiến trường.
            SeedIfMissing("Warzone", new double[] { -13, -10, -6, -2, 3, 8, 11, 10, 5, 2 });

            // Rock: "smiley curve" mạnh hơn Pop - bass chắc, mid hơi lõm để guitar/drum không bị
            // đục, treble đẩy rõ cho tiếng cymbal/distortion sắc nét.
            SeedIfMissing("Rock", new double[] { 4, 3, 1, 0, -1, 0, 1, 3, 4, 4 });

            // EDM: bass rất mạnh (kick/sub bass), mid lõm sâu để không che tiếng synth, treble
            // đẩy cao cho hi-hat/presence rõ, đúng tinh thần "club sound".
            SeedIfMissing("EDM", new double[] { 6, 5, 3, 0, -2, -1, 1, 3, 5, 5 });

            // Hip-Hop: bass sâu và ấm (808/sub bass), mid giữ tự nhiên để vocal rõ, treble chỉ
            // nhấn nhẹ tránh chói.
            SeedIfMissing("Hip-Hop", new double[] { 7, 6, 4, 2, 0, -1, 0, 1, 2, 2 });

            // Classical: gần như flat, chỉ nhấn rất nhẹ để giữ độ tự nhiên của dàn nhạc - không
            // boost mạnh chỗ nào để tránh phá mất dynamic range của bản thu acoustic.
            SeedIfMissing("Classical", new double[] { 1, 1, 0, 0, 0, 0, 0, 1, 2, 2 });

            // Jazz: bass ấm vừa phải, mid nhấn nhẹ cho kèn/piano/double bass mộc, treble giữ tự
            // nhiên không đẩy quá cao để không mất chất "live".
            SeedIfMissing("Jazz", new double[] { 3, 2, 1, 0, 0, 1, 1, 1, 1, 0 });

            // Acoustic: nhấn nhẹ dải mid-high (500Hz-2kHz) để tiếng guitar/vocal mộc rõ chi tiết,
            // bass và treble chỉ nâng nhẹ, giữ cảm giác "gần micro", chân thực.
            SeedIfMissing("Acoustic", new double[] { 2, 1, 1, 0, 1, 2, 2, 1, 1, 1 });

            // Movie: đẩy rõ dải thoại (1-3kHz) để nghe lời thoại rõ, giữ bass vừa phải cho cảnh
            // hành động/nhạc nền, treble nhẹ nhàng - không gắt như preset gaming.
            SeedIfMissing("Movie", new double[] { 2, 2, 1, 1, 2, 3, 3, 2, 1, 1 });

            // Podcast: cắt hẳn bass/treble thừa, chỉ tập trung dải giọng nói (300Hz-3kHz) để nghe
            // rõ và đỡ mệt tai khi nghe lâu.
            SeedIfMissing("Podcast", new double[] { -6, -5, -3, 0, 3, 4, 3, 0, -3, -5 });

            // Night Mode: giảm 2 dải dễ gây ồn/giật mình khi nghe khuya (sub-bass, treble sắc),
            // vẫn giữ rõ giọng nói/vocal ở dải giữa để nghe nhạc/phim nhẹ nhàng ban đêm.
            SeedIfMissing("Night Mode", new double[] { -4, -3, -1, 0, 1, 2, 2, 1, -1, -3 });

            // League of Legends: game nhìn từ trên xuống, không cần định vị âm thanh 3D như FPS -
            // ưu tiên nghe rõ hiệu ứng kỹ năng/thông báo (1-2kHz) hơn là bước chân, giữ bass/treble
            // gần tự nhiên.
            SeedIfMissing("League of Legends", new double[] { 0, 0, 0, 1, 2, 3, 2, 1, 1, 0 });

            // Dota 2: tương tự LoL nhưng đẩy rộng hơn qua cả 2-4kHz vì nhiều hiệu ứng kỹ năng ở
            // dải cao hơn (nuke damage, item sound cues).
            SeedIfMissing("Dota 2", new double[] { 0, 0, 1, 1, 2, 3, 3, 2, 1, 0 });

            // Minecraft: game không cần chỉnh mạnh - giữ gần tự nhiên, chỉ nhấn rất nhẹ mid-high
            // để nghe rõ tiếng mob/redstone/bước chân trong hang.
            SeedIfMissing("Minecraft", new double[] { 1, 1, 0, 0, 0, 1, 1, 1, 0, 0 });

            // Metal: bass boost mạnh + mid-scoop sâu hơn Rock (double kick, distortion không bị
            // đục), treble đẩy rất cao cho tiếng cymbal/distortion sắc nét.
            SeedIfMissing("Metal", new double[] { 5, 4, 2, -1, -3, -1, 2, 4, 6, 6 });

            // Lo-fi: làm mượt tổng thể - ấm bass-mid, cắt dần treble để không có tiếng sắc/gắt,
            // đúng chất "chill", nghe thư giãn không mệt tai.
            SeedIfMissing("Lo-fi", new double[] { 3, 2, 1, 1, 0, -1, -1, -2, -3, -4 });
        }
    }
}
