using Kater1EQ.Services;

namespace Kater1EQ.Models
{
    /// <summary>
    /// Trạng thái đầy đủ của 1 band tại thời điểm lưu preset - đủ để phục dựng lại chính xác
    /// 100% band đó khi load lại (không chỉ gain như bản cũ), theo đúng yêu cầu "lưu toàn bộ
    /// trạng thái EQ hiện tại" (Frequency / Gain / Q / Filter type).
    /// </summary>
    public class EqBandState
    {
        public int FrequencyHz { get; set; }
        public double GainDb { get; set; }
        public double Q { get; set; } = 0.9;
        public EqFilterType Type { get; set; } = EqFilterType.Bell;
        public int Slope { get; set; } = 12;
        public bool IsEnabled { get; set; } = true;
    }
}
