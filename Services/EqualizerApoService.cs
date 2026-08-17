using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Kater1EQ.Models;
using Microsoft.Win32;
using System.Text;

namespace Kater1EQ.Services
{
    /// <summary>
    /// Chịu trách nhiệm giao tiếp với Equalizer APO:
    /// - Tìm thư mục cài đặt / thư mục config
    /// - Ghi 1 file riêng "Kater1EQ.txt" (không đụng vào config.txt gốc của người dùng
    ///   ngoại trừ 1 dòng "Include:" duy nhất)
    /// - Bật/tắt EQ toàn hệ thống
    /// </summary>
    public class EqualizerApoService
    {
        private const string IncludeFileName = "Kater1EQ.txt";
        private string? _configFolder;

        public bool IsEqualizerApoInstalled => TryLocateConfigFolder() != null;

        /// <summary>
        /// Cố gắng tìm thư mục "config" của Equalizer APO qua registry,
        /// fallback về đường dẫn mặc định nếu không tìm thấy.
        /// </summary>
        public string? TryLocateConfigFolder()
        {
            if (_configFolder != null && Directory.Exists(_configFolder))
                return _configFolder;

            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\EqualizerAPO")
                    ?? Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\EqualizerAPO");

                var installLocation = key?.GetValue("InstallLocation") as string;
                if (!string.IsNullOrWhiteSpace(installLocation))
                {
                    var candidate = Path.Combine(installLocation, "config");
                    if (Directory.Exists(candidate))
                    {
                        _configFolder = candidate;
                        return _configFolder;
                    }
                }
            }
            catch
            {
                // Bỏ qua lỗi registry, thử fallback bên dưới
            }

            // Fallback: đường dẫn cài đặt mặc định
            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "EqualizerAPO", "config");

            if (Directory.Exists(defaultPath))
            {
                _configFolder = defaultPath;
                return _configFolder;
            }

            return null;
        }

        /// <summary>
        /// Đảm bảo config.txt gốc có include file riêng của Kater1EQ.
        /// Chỉ thêm 1 dòng, không xoá gì của người dùng.
        /// </summary>
        private void EnsureIncludeDirective(string configFolder)
        {
            var mainConfigPath = Path.Combine(configFolder, "config.txt");
            var includeLine = $"Include: {IncludeFileName}";

            if (!File.Exists(mainConfigPath))
            {
                File.WriteAllText(mainConfigPath, includeLine + Environment.NewLine);
                return;
            }

            var lines = File.ReadAllLines(mainConfigPath);
            if (!lines.Any(l => l.Trim().Equals(includeLine, StringComparison.OrdinalIgnoreCase)))
            {
                File.AppendAllText(mainConfigPath, Environment.NewLine + includeLine + Environment.NewLine);
            }
        }

        /// <summary>
        /// Ghi toàn bộ band hiện tại xuống file Kater1EQ.txt dưới dạng GraphicEQ.
        /// Equalizer APO sẽ tự động reload (nó theo dõi thay đổi file).
        /// </summary>
        public void WriteBands(IEnumerable<EqBand> bands, bool enabled) => WriteBands(bands, enabled, 0.0);

        private static string ApoTypeCode(EqFilterType type) => type switch
        {
            EqFilterType.Bell => "PK",
            EqFilterType.LowShelf => "LSC",
            EqFilterType.HighShelf => "HSC",
            EqFilterType.LowPass => "LPQ",
            EqFilterType.HighPass => "HPQ",
            EqFilterType.Notch => "NO",
            _ => "PK"
        };

        /// <summary>
        /// Ghi toàn bộ band + preamp (master volume) hiện tại xuống file Kater1EQ.txt.
        /// Mỗi band được ghi thành 1 dòng "Filter:" - biquad IIR thật của Equalizer APO
        /// (RBJ cookbook), có Fc/Gain/Q riêng biệt, KHÔNG dùng GraphicEQ (chỉ nội suy tuyến
        /// tính, không có Q thật). Preamp = giá trị người dùng chỉnh tay + phần bù tự động
        /// (RecomputeCompensation) để tránh clip khi nhiều band cộng dồn peak dương, mà không
        /// làm suy yếu hình dạng đường cong EQ.
        /// </summary>
        /// <param name="masterVolumeDb">Preamp thủ công, tương ứng thanh trượt VOL bên trái giao diện.</param>
        public void WriteBands(IEnumerable<EqBand> bands, bool enabled, double masterVolumeDb)
        {
            var folder = TryLocateConfigFolder();
            if (folder == null)
                throw new InvalidOperationException(
                    "Không tìm thấy Equalizer APO. Vui lòng cài đặt trước tại equalizerapo.com");

            EnsureIncludeDirective(folder);

            var filePath = Path.Combine(folder, IncludeFileName);

            if (!enabled)
            {
                // Ghi filter "no-op" khi tắt EQ, giữ nguyên âm thanh gốc - bypass thật, không
                // chỉ giảm gain về 0 (tránh trường hợp round-trip float khác biệt rất nhỏ).
                File.WriteAllText(filePath, "# Kater1EQ - Disabled" + Environment.NewLine);
                return;
            }

            // Band bị tắt (IsEnabled=false) không được tính vào DSP thật, giữ nguyên trong danh
            // sách UI (không xoá) - đúng yêu cầu "Enable/disable band" ở mục UX.
            var bandList = bands.Where(b => b.IsEnabled).OrderBy(b => b.FrequencyHz).ToList();

            // Auto gain compensation: chỉ trừ đúng bằng peak dương thực tế của tổng các band,
            // không bao giờ nén hay boost thêm - hình dạng EQ giữ nguyên 100%.
            var curveInput = bandList.Select(b => (b.Type, (double)b.FrequencyHz, b.GainDb, b.Q, b.Slope));
            double autoCompensationDb = -Math.Max(0, EqCurveMath.FindPeakDb(curveInput));

            double totalPreampDb = masterVolumeDb + autoCompensationDb;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# File này được Kater1EQ tự động sinh ra, không chỉnh sửa tay");
            sb.AppendLine("Preamp: " + totalPreampDb.ToString("0.0", CultureInfo.InvariantCulture) + " dB");

            foreach (var b in bandList)
            {
                string typeCode = ApoTypeCode(b.Type);
                bool isNotch = b.Type == EqFilterType.Notch;
                string gainPart = isNotch ? string.Empty
                    : $"Gain {b.GainDb.ToString("0.0", CultureInfo.InvariantCulture)} dB ";

                // Slope: Equalizer APO không có tham số slope riêng cho LPQ/HPQ (1 biquad = 12dB/oct
                // cố định), nên để đạt 24/36/48 dB/oct, Kater1EQ ghi lặp lại N dòng Filter: giống hệt
                // nhau nối tiếp (cascade) - đây là cách chuẩn để tăng bậc filter bằng biquad rời rạc.
                int stages = (b.Type == EqFilterType.LowPass || b.Type == EqFilterType.HighPass)
                    ? EqCurveMath.StageCountForSlope(b.Slope)
                    : 1;

                for (int s = 0; s < stages; s++)
                {
                    // Filter: ON <type> Fc <freq> Hz Gain <gain> dB Q <q>
                    sb.AppendLine(
                        $"Filter: ON {typeCode} Fc {b.FrequencyHz} Hz {gainPart}Q " +
                        $"{b.Q.ToString("0.00", CultureInfo.InvariantCulture)}");
                }
            }

            File.WriteAllText(filePath, sb.ToString());
        }
    }
}
