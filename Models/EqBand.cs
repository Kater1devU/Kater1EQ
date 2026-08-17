using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Kater1EQ.Models
{
    /// <summary>
    /// Đại diện cho 1 band tần số. Mỗi band ánh xạ 1:1 sang 1 dòng "Filter:" thật trong
    /// Equalizer APO (biquad IIR chuẩn RBJ), không phải điểm nội suy GraphicEQ - nhờ vậy band
    /// có Q điều chỉnh được và có thể chọn Bell/Shelf/LowPass/HighPass đúng như yêu cầu DSP.
    /// </summary>
    public class EqBand : INotifyPropertyChanged
    {
        public int FrequencyHz { get; set; }

        /// <summary>Nhãn hiển thị, ví dụ "31" hoặc "16k"</summary>
        public string Label { get; set; } = string.Empty;

        private Services.EqFilterType _type = Services.EqFilterType.Bell;
        public Services.EqFilterType Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SupportsSlope));
                    OnPropertyChanged(nameof(SupportsGain));
                }
            }
        }

        private double _gainDb;
        public double GainDb
        {
            get => _gainDb;
            set
            {
                // -24..+24 dB theo đúng yêu cầu thiết kế. Gain hiển thị UI = gain DSP thực tế,
                // không có hệ số nhân/chia ẩn nào ở đây.
                var clamped = value < -24 ? -24 : (value > 24 ? 24 : value);
                if (_gainDb != clamped)
                {
                    _gainDb = clamped;
                    OnPropertyChanged();
                }
            }
        }

        private double _q = 0.9;
        /// <summary>
        /// Q mặc định vừa phải (không quá hẹp) để band tác động lên cả vùng tần số có ý nghĩa
        /// thay vì 1 điểm hẹp. Người dùng có thể chỉnh sau (ví dụ scroll chuột trên chấm EQ).
        /// </summary>
        public double Q
        {
            get => _q;
            set
            {
                var clamped = value < 0.1 ? 0.1 : (value > 10 ? 10 : value);
                if (_q != clamped)
                {
                    _q = clamped;
                    OnPropertyChanged();
                }
            }
        }

        private int _slope = 12;
        /// <summary>
        /// Độ dốc (dB/oct), chỉ có ý nghĩa với LowPass/HighPass. Giá trị hợp lệ: 12/24/36/48 -
        /// tương ứng số biquad cascade (1/2/3/4 stage) mà EqualizerApoService sẽ ghi ra.
        /// </summary>
        public int Slope
        {
            get => _slope;
            set
            {
                int[] allowed = { 12, 24, 36, 48 };
                int clamped = allowed.OrderBy(v => Math.Abs(v - value)).First();
                if (_slope != clamped)
                {
                    _slope = clamped;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isEnabled = true;
        /// <summary>Cho phép bật/tắt riêng từng band mà không cần xoá (UX mục 13).</summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set { if (_isEnabled != value) { _isEnabled = value; OnPropertyChanged(); } }
        }

        /// <summary>Filter type nào hiện đang hỗ trợ Slope (chỉ LowPass/HighPass, theo đúng spec
        /// "Slope nếu filter hỗ trợ slope").</summary>
        public bool SupportsSlope => Type == Services.EqFilterType.LowPass || Type == Services.EqFilterType.HighPass;

        /// <summary>Notch không có tham số Gain thực (RBJ band-reject cố định) nên UI cần biết để
        /// vô hiệu hoá control Gain khi loại này được chọn.</summary>
        public bool SupportsGain => Type != Services.EqFilterType.Notch;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
