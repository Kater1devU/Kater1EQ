using System.Collections.Generic;

namespace Kater1EQ.Models
{
    public class EqPreset
    {
        public string Name { get; set; } = "Untitled";

        /// <summary>Preamp/master volume (dB) tại thời điểm lưu preset.</summary>
        public double PreampDb { get; set; }

        /// <summary>Toàn bộ trạng thái từng band: Frequency, Gain, Q, Filter Type.</summary>
        public List<EqBandState> Bands { get; set; } = new();
    }
}
