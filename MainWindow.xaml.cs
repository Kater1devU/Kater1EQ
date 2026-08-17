using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Kater1EQ.Models;
using Kater1EQ.Services;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Kater1EQ
{
    public partial class MainWindow : Window
    {
        private readonly EqualizerApoService _apoService = new();
        private readonly PresetService _presetService = new();
        private readonly AudioLoopbackService _audioService = new();
        private readonly SystemVolumeService _systemVolume = new();
        private System.Windows.Forms.NotifyIcon? _trayIcon;

        private readonly ObservableCollection<EqBand> _bands = new()
        {
            new EqBand { FrequencyHz = 31,    Label = "31" },
            new EqBand { FrequencyHz = 62,    Label = "62" },
            new EqBand { FrequencyHz = 125,   Label = "125" },
            new EqBand { FrequencyHz = 250,   Label = "250" },
            new EqBand { FrequencyHz = 500,   Label = "500" },
            new EqBand { FrequencyHz = 1000,  Label = "1k" },
            new EqBand { FrequencyHz = 2000,  Label = "2k" },
            new EqBand { FrequencyHz = 4000,  Label = "4k" },
            new EqBand { FrequencyHz = 8000,  Label = "8k" },
            new EqBand { FrequencyHz = 16000, Label = "16k" },
        };

        private bool _eqEnabled = true;

        // Debounce nhẹ để không ghi file ~60 lần/giây khi kéo slider
        private DateTime _lastWrite = DateTime.MinValue;
        private static readonly TimeSpan WriteThrottle = TimeSpan.FromMilliseconds(100);

        public MainWindow()
        {
            InitializeComponent();

            foreach (var band in _bands)
                band.PropertyChanged += Band_PropertyChanged;

            SetupTrayIcon();
            LoadPresetNames();
            LoadSocialLinks();
            SetupFilterEditor();
            UpdateCurve();
            SetupAudioVisualizer();

            // STEP 13: phím tắt toàn cục - Esc đóng Filter Editor, Delete reset band đang chọn.
            // Đăng ký ở PreviewKeyDown (tunneling, bắt trước khi control con xử lý) nhưng CHỦ ĐỘNG
            // bỏ qua khi focus đang ở TextBox / PresetListBox để không phá gõ chữ hay lỡ tay
            // xoá/reset preset khi người dùng chỉ đang duyệt danh sách preset bằng bàn phím.
            PreviewKeyDown += MainWindow_PreviewKeyDown;

            if (!_apoService.IsEqualizerApoInstalled)
            {
                MessageBox.Show(
                    "Không tìm thấy Equalizer APO trên máy.\n\n" +
                    "Kater1EQ cần Equalizer APO để chỉnh âm thanh toàn hệ thống.\n" +
                    "Vui lòng tải và cài tại: equalizerapo.com",
                    "Kater1EQ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            WriteCurrentState();
        }

        // ===================================================================
        // GHI CẤU HÌNH EQ
        // ===================================================================

        // STEP 8: helper an toàn để đọc brush theo theme hiện tại - dùng TryFindResource thay vì
        // FindResource() trực tiếp, vì FindResource ném ResourceReferenceKeyNotFoundException và
        // làm crash toàn app nếu 1 key tạm thời chưa sẵn sàng (vd stale build cache, theme đang
        // đổi dở dang), trong khi TryFindResource trả về null và cho phép fallback êm.
        private Brush ThemeBrush(string key, Brush fallback)
            => TryFindResource(key) as Brush ?? fallback;

        private void Band_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            ThrottledWrite();
            // Nếu band đang bị chỉnh (vd kéo chấm trên đồ thị, hoặc bật/tắt) chính là band đang mở
            // trong Filter Editor, đồng bộ số liệu hiển thị ngay - đúng yêu cầu 2 chiều đồ thị <-> panel.
            if (ReferenceEquals(sender, SelectedBand))
                RefreshFilterEditorFields();
        }

        private void ThrottledWrite()
        {
            var now = DateTime.UtcNow;
            if (now - _lastWrite < WriteThrottle) return;
            _lastWrite = now;
            WriteCurrentState();
        }

        private void WriteCurrentState()
        {
            try
            {
                _apoService.WriteBands(_bands, _eqEnabled, MasterVolumeSlider?.Value ?? 0.0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
            UpdateCurve();
        }

        // ===================================================================
        // ĐƯỜNG CONG EQ (curve) + CHẤM KÉO (draggable dots)
        // ===================================================================

        private Path? _curveFillPath;
        private Path? _curveLinePath;
        private readonly List<Ellipse> _curveDots = new();
        private readonly List<TextBlock> _bandNumberLabels = new();
        private int _draggingIndex = -1;
        private int _selectedBandIndex = -1;

        /// <summary>Band đang được chọn trên EQ graph (click chấm để chọn) - Filter Editor sẽ đọc
        /// giá trị này để hiển thị/chỉnh trực tiếp band tương ứng.</summary>
        public EqBand? SelectedBand => _selectedBandIndex >= 0 && _selectedBandIndex < _bands.Count
            ? _bands[_selectedBandIndex] : null;

        /// <summary>Bắn ra mỗi khi người dùng chọn 1 band khác trên EQ graph (click chấm).</summary>
        public event Action<int>? BandSelected;

        private void SelectBand(int index)
        {
            if (index < 0 || index >= _bands.Count) return;
            _selectedBandIndex = index;
            UpdateDotSelectionVisuals();
            RefreshFilterEditorFields();
            BandSelected?.Invoke(index);
        }

        /// <summary>Tô đậm chấm của band đang chọn để phân biệt trực quan với các band còn lại.</summary>
        private void UpdateDotSelectionVisuals()
        {
            // STEP 8: band selected dùng SelectedBorderBrush (viền pixel rõ ràng) thay vì
            // Brushes.White hard-code, và nền panel làm lõi chấm thay vì trắng thuần -
            // đồng bộ đúng PixelTheme thay vì tự chế 1 màu ngoài hệ thống resource.
            var accent = ThemeBrush("AccentColor", Brushes.Goldenrod);
            var selectedBrush = ThemeBrush("SelectedBorderBrush", Brushes.White);
            var dotCoreBrush = ThemeBrush("BgCard", Brushes.Black);
            for (int i = 0; i < _curveDots.Count; i++)
            {
                bool selected = i == _selectedBandIndex;
                var dot = _curveDots[i];
                dot.Width = dot.Height = selected ? 16 : 12;
                dot.StrokeThickness = selected ? 2.4 : 1.6;
                dot.Fill = selected ? dotCoreBrush : accent;
                dot.Stroke = selected ? selectedBrush : accent;
                dot.Opacity = _bands[i].IsEnabled ? 1.0 : 0.35;

                if (i < _bandNumberLabels.Count)
                    _bandNumberLabels[i].Opacity = _bands[i].IsEnabled ? (selected ? 1.0 : 0.75) : 0.3;
            }
        }

        /// <summary>Vùng tô dưới đường cong: gradient nhạt dần từ màu EQCurveBrush hiện tại thay
        /// vì hard-code hồng cố định (#FF6FA8) - để khớp đúng theme đang active (Pixel/Dark/Pink).</summary>
        private static Brush BuildCurveFillBrush(Brush curveBrush)
        {
            Color baseColor = curveBrush is SolidColorBrush scb ? scb.Color : Colors.Gray;
            return new LinearGradientBrush(
                Color.FromArgb(90, baseColor.R, baseColor.G, baseColor.B),
                Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B),
                90);
        }

        private const double GainRangeDb = 24.0; // khớp EqBand.GainDb (-24..+24 dB theo spec DSP)

        private void CurveCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateCurve();

        private void WaveformCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawWaveform();

        private static double GainToY(double gainDb, double height)
        {
            const double padding = 6;
            double usableHeight = height - padding * 2;
            double midY = height / 2;
            double normalized = gainDb / GainRangeDb; // -1..1
            return midY - normalized * (usableHeight / 2);
        }

        private static double YToGain(double y, double height)
        {
            const double padding = 6;
            double usableHeight = height - padding * 2;
            double midY = height / 2;
            double normalized = (midY - y) / (usableHeight / 2);
            double gain = normalized * GainRangeDb;
            return gain < -GainRangeDb ? -GainRangeDb : (gain > GainRangeDb ? GainRangeDb : gain);
        }

        private static string FormatFreqLabel(double hz)
        {
            if (hz >= 1000)
                return (hz / 1000.0).ToString("0.#") + "k";
            return hz.ToString("0");
        }

        /// <summary>
        /// Vẽ đường cong tần số mượt (Catmull-Rom → Bezier) dựa trên gain hiện tại của các band,
        /// kèm vùng tô gradient bên dưới. Mỗi band có 1 chấm có thể kéo trực tiếp trên đồ thị.
        /// </summary>
        private void UpdateCurve()
        {
            if (CurveCanvas == null) return;
            double width = CurveCanvas.ActualWidth;
            double height = CurveCanvas.ActualHeight;
            if (width <= 0 || height <= 0) return;

            int n = _bands.Count;
            if (n < 2) return;

            // Khởi tạo 1 lần duy nhất, các lần sau chỉ cập nhật dữ liệu hình học
            if (_curveFillPath == null)
            {
                // STEP 8: đường cong dùng EQCurveBrush riêng (không phải AccentColor dùng cho
                // button/selection) để tách biệt ý nghĩa thị giác, đúng yêu cầu thiết kế pixel.
                // Alias EQCurveBrush đã được thêm vào cả DarkTheme/PinkTheme (STEP 8) nên
                // FindResource an toàn ở cả 3 theme, không chỉ riêng Pixel.
                _curveFillPath = new Path { StrokeThickness = 0 };
                _curveLinePath = new Path
                {
                    StrokeThickness = 2,
                    // Miter thay vì Round: giữ góc gãy sắc nét đúng tinh thần pixel/technical
                    // thay vì bo tròn mềm kiểu audio software hiện đại.
                    StrokeLineJoin = PenLineJoin.Miter,
                    SnapsToDevicePixels = true
                };
                RenderOptions.SetEdgeMode(_curveLinePath, EdgeMode.Aliased);
                CurveCanvas.Children.Add(_curveFillPath);
                CurveCanvas.Children.Add(_curveLinePath);

                for (int i = 0; i < n; i++)
                {
                    var dot = new Ellipse
                    {
                        Width = 12,
                        Height = 12,
                        StrokeThickness = 1.6,
                        Cursor = Cursors.SizeNS
                    };
                    dot.MouseLeftButtonDown += CurveDot_MouseLeftButtonDown;
                    dot.MouseMove += CurveDot_MouseMove;
                    dot.MouseLeftButtonUp += CurveDot_MouseLeftButtonUp;
                    dot.MouseWheel += CurveDot_MouseWheel;

                    _curveDots.Add(dot);
                    CurveCanvas.Children.Add(dot);

                    // Số thứ tự band, hiển thị ngay trên chấm - dùng font/màu theo theme hiện tại
                    // (SmallFont khi theme Pixel có merge PixelFonts, fallback mặc định ở theme khác).
                    var numberLabel = new TextBlock
                    {
                        Text = (i + 1).ToString(),
                        FontSize = 9,
                        FontWeight = FontWeights.Bold,
                        Foreground = ThemeBrush("TextSecondary", Brushes.LightGray),
                        IsHitTestVisible = false,
                        TextAlignment = TextAlignment.Center
                    };
                    _bandNumberLabels.Add(numberLabel);
                    CurveCanvas.Children.Add(numberLabel);
                }
            }

            // Cập nhật brush theo theme hiện tại ở MỖI lần vẽ (không chỉ lúc khởi tạo) để đổi
            // theme lúc runtime (STEP 10) phản ánh đúng lên màu đường cong / chấm ngay lập tức.
            var curveBrush = ThemeBrush("EQCurveBrush", Brushes.Goldenrod);
            _curveLinePath!.Stroke = curveBrush;
            _curveFillPath!.Fill = BuildCurveFillBrush(curveBrush);

            // Đường cong được tính từ magnitude response THẬT của các biquad (RBJ, cùng công thức
            // Equalizer APO dùng cho "Filter:"), không phải vẽ giả chỉ dựa trên vị trí slider - vì
            // vậy khi đổi Q, đường cong sẽ phình/hẹp đúng thực tế thay vì luôn là 1 đường Catmull-Rom cố định.
            var curveInput = _bands.Where(b => b.IsEnabled)
                .Select(b => (b.Type, (double)b.FrequencyHz, b.GainDb, b.Q, b.Slope)).ToList();

            const int subdivisions = 10; // số điểm nội suy log-frequency giữa 2 band liên tiếp
            var points = new List<Point>();
            for (int i = 0; i < n; i++)
            {
                int steps = (i < n - 1) ? subdivisions : 1;
                for (int s = 0; s < steps; s++)
                {
                    double frac = s / (double)subdivisions;
                    double x = n == 1 ? width / 2 : (i + frac) * (width / (n - 1));

                    double freq = (i < n - 1)
                        ? _bands[i].FrequencyHz * Math.Pow((double)_bands[i + 1].FrequencyHz / _bands[i].FrequencyHz, frac)
                        : _bands[i].FrequencyHz;

                    double gainDb = EqCurveMath.CombinedMagnitudeDb(curveInput, freq);
                    double y = GainToY(Math.Max(-GainRangeDb, Math.Min(GainRangeDb, gainDb)), height);
                    points.Add(new Point(x, y));
                }
            }

            var figure = new PathFigure { StartPoint = points[0], IsClosed = false };
            for (int i = 1; i < points.Count; i++)
                figure.Segments.Add(new LineSegment(points[i], true));

            var lineGeometry = new PathGeometry();
            lineGeometry.Figures.Add(figure);

            var fillFigure = figure.Clone();
            fillFigure.Segments.Add(new LineSegment(new Point(points[^1].X, height), true));
            fillFigure.Segments.Add(new LineSegment(new Point(points[0].X, height), true));
            fillFigure.IsClosed = true;
            var fillGeometry = new PathGeometry();
            fillGeometry.Figures.Add(fillFigure);

            _curveLinePath!.Data = lineGeometry;
            _curveFillPath!.Data = fillGeometry;

            // Chấm kéo (dots) đặt đúng tại gain riêng của từng band, để kéo trực quan -
            // độc lập với các điểm nội suy dùng để vẽ đường cong ở trên.
            for (int i = 0; i < n && i < _curveDots.Count; i++)
            {
                double dotX = n == 1 ? width / 2 : i * (width / (n - 1));
                double dotY = GainToY(_bands[i].GainDb, height);
                var dot = _curveDots[i];
                Canvas.SetLeft(dot, dotX - dot.Width / 2);
                Canvas.SetTop(dot, dotY - dot.Height / 2);

                // Số band đặt phía trên chấm (hoặc dưới nếu chấm quá sát mép trên) - clamp để
                // không bao giờ bị cắt bởi canvas, không bao giờ đủ lớn để che mất đường cong.
                if (i < _bandNumberLabels.Count)
                {
                    var label = _bandNumberLabels[i];
                    label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double labelWidth = Math.Max(label.DesiredSize.Width, 12);
                    const double gap = 3;
                    double labelTop = dotY - dot.Height / 2 - label.DesiredSize.Height - gap;
                    if (labelTop < 0)
                        labelTop = dotY + dot.Height / 2 + gap; // đủ gần mép trên -> đặt xuống dưới chấm
                    Canvas.SetLeft(label, dotX - labelWidth / 2);
                    Canvas.SetTop(label, labelTop);
                }
            }
            UpdateDotSelectionVisuals();
        }

        private void CurveDot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Ellipse dot) return;
            int idx = _curveDots.IndexOf(dot);
            if (idx < 0) return;
            _draggingIndex = idx;
            // Click vào band -> chọn ngay để Filter Editor (bước 3) hiển thị đúng band này,
            // kể cả khi người dùng chỉ click (không kéo).
            SelectBand(idx);
            dot.CaptureMouse();
            e.Handled = true;
        }

        private void CurveDot_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggingIndex < 0 || e.LeftButton != MouseButtonState.Pressed) return;
            var band = _bands[_draggingIndex];
            if (!band.SupportsGain) return; // Notch: không có Gain để kéo
            double height = CurveCanvas.ActualHeight;
            if (height <= 0) return;
            var pos = e.GetPosition(CurveCanvas);
            band.GainDb = YToGain(pos.Y, height); // setter tự clamp -24..24
        }

        private void CurveDot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Ellipse dot) dot.ReleaseMouseCapture();
            _draggingIndex = -1;
        }

        /// <summary>Scroll chuột ngay trên chấm band để chỉnh nhanh Q mà không cần mở Filter Editor -
        /// khớp yêu cầu "chọn và chỉnh trực tiếp" trên EQ graph.</summary>
        private void CurveDot_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not Ellipse dot) return;
            int idx = _curveDots.IndexOf(dot);
            if (idx < 0) return;
            SelectBand(idx);
            double step = e.Delta > 0 ? 0.05 : -0.05;
            _bands[idx].Q += step; // setter tự clamp 0.1..10
            e.Handled = true;
        }

        // ===================================================================
        // FILTER EDITOR PANEL
        // ===================================================================

        private bool _isUpdatingFilterEditor;

        private sealed class FilterTypeOption
        {
            public EqFilterType Type { get; }
            private readonly string _label;
            public FilterTypeOption(EqFilterType type, string label) { Type = type; _label = label; }
            public override string ToString() => _label;
        }

        private static readonly FilterTypeOption[] _filterTypeOptions =
        {
            new(EqFilterType.Bell, "BELL"),
            new(EqFilterType.LowShelf, "LOW SHELF"),
            new(EqFilterType.HighShelf, "HIGH SHELF"),
            new(EqFilterType.LowPass, "LOW PASS"),
            new(EqFilterType.HighPass, "HIGH PASS"),
            new(EqFilterType.Notch, "NOTCH"),
        };

        private static readonly int[] _slopeOptions = { 12, 24, 36, 48 };

        private void SetupFilterEditor()
        {
            FilterEditorTypeCombo.ItemsSource = _filterTypeOptions;
            FilterEditorSlopeCombo.ItemsSource = _slopeOptions.Select(s => $"{s} dB/oct").ToList();
        }

        /// <summary>Đổ dữ liệu của band đang chọn vào panel và hiện panel lên. Gọi lại mỗi khi band
        /// đang chọn thay đổi (kể cả do kéo chấm trên đồ thị) để giữ đồng bộ 2 chiều.</summary>
        private void RefreshFilterEditorFields()
        {
            var band = SelectedBand;
            if (band == null)
            {
                if (FilterEditorPanel.Visibility == Visibility.Visible)
                    AnimatePanelVisibility(FilterEditorPanel, FilterEditorPanelTransform, show: false);
                return;
            }

            _isUpdatingFilterEditor = true;
            try
            {
                if (FilterEditorPanel.Visibility != Visibility.Visible)
                    AnimatePanelVisibility(FilterEditorPanel, FilterEditorPanelTransform, show: true);
                FilterEditorBandLabel.Text = (_selectedBandIndex + 1).ToString("00");

                var typeOption = _filterTypeOptions.FirstOrDefault(o => o.Type == band.Type);
                if (FilterEditorTypeCombo.SelectedItem != typeOption)
                    FilterEditorTypeCombo.SelectedItem = typeOption;

                if (!FilterEditorFreqBox.IsFocused)
                    FilterEditorFreqBox.Text = band.FrequencyHz.ToString(CultureInfo.InvariantCulture);

                FilterEditorGainBox.IsEnabled = band.SupportsGain;
                if (!FilterEditorGainBox.IsFocused)
                    FilterEditorGainBox.Text = band.SupportsGain
                        ? band.GainDb.ToString("0.0", CultureInfo.InvariantCulture)
                        : "—";

                if (!FilterEditorQBox.IsFocused)
                    FilterEditorQBox.Text = band.Q.ToString("0.00", CultureInfo.InvariantCulture);

                FilterEditorSlopeRow.Visibility = band.SupportsSlope ? Visibility.Visible : Visibility.Collapsed;
                if (band.SupportsSlope)
                {
                    int slopeIdx = Array.IndexOf(_slopeOptions, band.Slope);
                    FilterEditorSlopeCombo.SelectedIndex = slopeIdx < 0 ? 0 : slopeIdx;
                }

                FilterEditorEnabledCheck.IsChecked = band.IsEnabled;
            }
            finally
            {
                _isUpdatingFilterEditor = false;
            }
        }

        private void CloseFilterEditor_Click(object sender, RoutedEventArgs e)
        {
            _selectedBandIndex = -1;
            AnimatePanelVisibility(FilterEditorPanel, FilterEditorPanelTransform, show: false);
            UpdateDotSelectionVisuals();
        }

        /// <summary>STEP 12: fade + slide nhẹ (120ms, không bounce/elastic) khi mở/đóng panel
        /// overlay (Filter Editor, Social). Chỉ animate Opacity/RenderTransform trên UI thread -
        /// không đụng tới audio thread hay waveform rendering. Khi ẩn, Visibility chỉ đổi thành
        /// Collapsed sau khi animation chạy xong (Completed) để không "biến mất" đột ngột.</summary>
        private static void AnimatePanelVisibility(FrameworkElement panel, TranslateTransform transform, bool show)
        {
            const double slideDistance = 12;
            var duration = new Duration(TimeSpan.FromMilliseconds(120));
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            if (show)
            {
                panel.Visibility = Visibility.Visible;
                panel.Opacity = 0;
                transform.Y = -slideDistance;

                var fadeIn = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
                var slideIn = new DoubleAnimation(-slideDistance, 0, duration) { EasingFunction = ease };
                panel.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                transform.BeginAnimation(TranslateTransform.YProperty, slideIn);
            }
            else
            {
                var fadeOut = new DoubleAnimation(panel.Opacity, 0, duration) { EasingFunction = ease };
                var slideOut = new DoubleAnimation(transform.Y, slideDistance, duration) { EasingFunction = ease };
                fadeOut.Completed += (_, _) => panel.Visibility = Visibility.Collapsed;
                panel.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                transform.BeginAnimation(TranslateTransform.YProperty, slideOut);
            }
        }

        /// <summary>
        /// STEP 13: Esc đóng Filter Editor (nếu đang mở); Delete reset band đang chọn về mặc định.
        /// Không xử lý Delete khi focus đang ở TextBox (đang gõ chữ) hoặc PresetListBox (tránh nhầm
        /// với thao tác xoá preset) - chỉ áp dụng cho phần còn lại của cửa sổ (vd đang focus vào
        /// chấm band trên đồ thị hoặc vùng trống).
        /// </summary>
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (FilterEditorPanel.Visibility == Visibility.Visible)
                {
                    CloseFilterEditor_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.Delete)
            {
                var focused = Keyboard.FocusedElement;
                if (focused is TextBox || focused is ListBox || focused is ListBoxItem)
                    return; // đang gõ chữ hoặc đang thao tác trong PresetListBox - bỏ qua

                if (SelectedBand != null)
                {
                    ResetBand_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
            }
        }

        private void FilterEditorTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingFilterEditor) return;
            var band = SelectedBand;
            if (band == null || FilterEditorTypeCombo.SelectedItem is not FilterTypeOption option) return;
            band.Type = option.Type;
            RefreshFilterEditorFields(); // TYPE đổi có thể làm hiện/ẩn ô GAIN, SLOPE
        }

        private void FilterEditorSlopeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingFilterEditor) return;
            var band = SelectedBand;
            if (band == null || FilterEditorSlopeCombo.SelectedIndex < 0) return;
            band.Slope = _slopeOptions[FilterEditorSlopeCombo.SelectedIndex];
        }

        private void FilterEditorEnabledCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingFilterEditor) return;
            var band = SelectedBand;
            if (band == null) return;
            band.IsEnabled = FilterEditorEnabledCheck.IsChecked == true;
            UpdateDotSelectionVisuals();
        }

        private void FilterEditorFreqBox_LostFocus(object sender, RoutedEventArgs e) => CommitFreqBox();
        private void FilterEditorFreqBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { CommitFreqBox(); Keyboard.ClearFocus(); }
        }

        private void CommitFreqBox()
        {
            var band = SelectedBand;
            if (band == null) return;
            if (int.TryParse(FilterEditorFreqBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hz) && hz >= 10 && hz <= 20000)
            {
                band.FrequencyHz = hz;
                ResortBandsByFrequency(); // giữ _bands tăng dần theo tần số để UpdateCurve nội suy đúng
                // FrequencyHz là property thường (không notify) để tránh ghi file khi đang kéo UI khác,
                // nên ở đây chủ động gọi WriteCurrentState (ghi APO + vẽ lại curve) thay vì chờ PropertyChanged.
                WriteCurrentState();
            }
            RefreshFilterEditorFields();
        }

        private void FilterEditorGainBox_LostFocus(object sender, RoutedEventArgs e) => CommitGainBox();
        private void FilterEditorGainBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { CommitGainBox(); Keyboard.ClearFocus(); }
        }

        private void CommitGainBox()
        {
            var band = SelectedBand;
            if (band == null || !band.SupportsGain) return;
            if (double.TryParse(FilterEditorGainBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var gain))
                band.GainDb = gain; // setter tự clamp -24..24
            RefreshFilterEditorFields();
        }

        private void FilterEditorQBox_LostFocus(object sender, RoutedEventArgs e) => CommitQBox();
        private void FilterEditorQBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { CommitQBox(); Keyboard.ClearFocus(); }
        }

        private void CommitQBox()
        {
            var band = SelectedBand;
            if (band == null) return;
            if (double.TryParse(FilterEditorQBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var q))
                band.Q = q; // setter tự clamp 0.1..10
            RefreshFilterEditorFields();
        }

        private void ResetBand_Click(object sender, RoutedEventArgs e)
        {
            var band = SelectedBand;
            if (band == null) return;
            band.Type = EqFilterType.Bell;
            band.Q = 0.9;
            band.Slope = 12;
            band.GainDb = 0;
            band.IsEnabled = true;
            RefreshFilterEditorFields();
        }

        // ===================================================================
        // FrequencyHz thay đổi (Filter Editor) có thể phá thứ tự tăng dần của _bands mà UpdateCurve
        // giả định để nội suy đường cong - sắp xếp lại khi Frequency đổi, KHÔNG đổi số lượng/band nào
        // đang chọn (dùng tham chiếu EqBand, không dùng index, để việc sắp xếp lại không làm mất lựa chọn).
        // ===================================================================
        private void ResortBandsByFrequency()
        {
            var selected = SelectedBand;
            var sorted = _bands.OrderBy(b => b.FrequencyHz).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                int currentIndex = _bands.IndexOf(sorted[i]);
                if (currentIndex != i) _bands.Move(currentIndex, i);
            }
            if (selected != null) _selectedBandIndex = _bands.IndexOf(selected);
        }

        // ===================================================================
        // SÓNG NHẠC REAL-TIME (loopback âm thanh hệ thống, không phải giả lập)
        // ===================================================================

        private readonly double[] _displayLevels = new double[AudioLoopbackService.BarCount];
        private readonly double[] _targetLevels = new double[AudioLoopbackService.BarCount];
        private readonly object _levelsLock = new();
        private readonly Random _idleRnd = new();
        private Path? _waveformLinePath;
        private Path? _waveformFillPath;
        private DispatcherTimer? _waveformTimer;
        private readonly List<System.Windows.Shapes.Line> _waveformGridLines = new();
        private readonly List<TextBlock> _waveformTickLabels = new();
        // Frequency ticks (Hz) for labels under the waveform (log scale)
        private static readonly double[] _freqTicks = new double[] { 5, 10, 20, 40, 80, 160, 320, 640, 1300, 2600, 5100, 10000, 20000 };

        private void SetupAudioVisualizer()
        {
            _audioService.SamplesAvailable += levels =>
            {
                lock (_levelsLock)
                {
                    for (int i = 0; i < levels.Length && i < _targetLevels.Length; i++)
                        _targetLevels[i] = levels[i];
                }
            };

            bool started = _audioService.Start();
            SourceNameText.Text = started
                ? (_audioService.DeviceFriendlyName ?? "thiết bị phát mặc định")
                : "không phát hiện nguồn phát";

            _waveformTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(45)
            };
            _waveformTimer.Tick += (s, e) => TickWaveform();
            _waveformTimer.Start();
        }

        private void TickWaveform()
        {
            double[] targets;
            lock (_levelsLock) targets = (double[])_targetLevels.Clone();
            // Waveform đang bị TẮT hiển thị (người dùng bấm nút) - luôn flatten về 0, KHÔNG đụng
            // tới _audioService (nó vẫn chạy nền bình thường, chỉ là mình không vẽ dữ liệu ra).
            if (!_waveformVisible || !_audioService.IsRunning)
            {
                for (int i = 0; i < _displayLevels.Length; i++)
                    _displayLevels[i] = 0.0;
                DrawWaveform();
                return;
            }

            bool silent = targets.All(t => t < 0.02);

            for (int i = 0; i < _displayLevels.Length; i++)
            {
                double t = targets[i];
                if (silent)
                {
                    // Fast decay to flat when input is silent
                    _displayLevels[i] += (0.0 - _displayLevels[i]) * 0.9;
                }
                else
                {
                    // Smooth toward target when audio is present
                    _displayLevels[i] += (t - _displayLevels[i]) * 0.45;
                }
            }

            DrawWaveform();
        }

        private void DrawWaveform()
        {
            if (WaveformCanvas == null) return;
            double width = WaveformCanvas.ActualWidth;
            double height = WaveformCanvas.ActualHeight;
            if (width <= 0 || height <= 0) return;

            // Initialize visual elements (grid, stroke + filled area, labels) once
            if (_waveformLinePath == null)
            {
                // STEP 8: grid line + fill gradient trước đây hard-code trắng/hồng - đổi sang
                // BorderColor/EQCurveBrush của theme hiện tại để waveform luôn khớp Pixel/Dark/Pink.
                var gridBrush = ThemeBrush("BorderColor", Brushes.DimGray);
                for (int i = 0; i < _freqTicks.Length; i++)
                {
                    var line = new System.Windows.Shapes.Line
                    {
                        Stroke = gridBrush,
                        StrokeThickness = 1,
                        Opacity = 0.6
                    };
                    _waveformGridLines.Add(line);
                    WaveformCanvas.Children.Add(line);
                }

                _waveformFillPath = new Path
                {
                    Fill = BuildCurveFillBrush(ThemeBrush("EQCurveBrush", Brushes.Goldenrod)),
                    Opacity = 0.5,
                    IsHitTestVisible = false
                };

                _waveformLinePath = new Path
                {
                    Stroke = ThemeBrush("EQCurveBrush", Brushes.Goldenrod),
                    StrokeThickness = 1.0,
                    Opacity = 0.95,
                    StrokeLineJoin = PenLineJoin.Miter,
                    StrokeStartLineCap = PenLineCap.Flat,
                    StrokeEndLineCap = PenLineCap.Flat
                };
                RenderOptions.SetEdgeMode(_waveformLinePath, EdgeMode.Aliased);

                WaveformCanvas.Children.Add(_waveformFillPath);
                WaveformCanvas.Children.Add(_waveformLinePath);

                // Labels
                var labelBrush = ThemeBrush("TextSecondary", Brushes.LightGray);
                for (int i = 0; i < _freqTicks.Length; i++)
                {
                    var tb = new TextBlock
                    {
                        FontSize = 10,
                        Foreground = labelBrush,
                        Opacity = 0.8,
                        Text = FormatFreqLabel(_freqTicks[i])
                    };
                    _waveformTickLabels.Add(tb);
                    WaveformCanvas.Children.Add(tb);
                }
            }

            int n = _displayLevels.Length;
            double midY = height / 2;

            // Small smoothing window to reduce jitter
            var smooth = new double[n];
            for (int i = 0; i < n; i++)
            {
                double sum = 0; int cnt = 0;
                for (int j = i - 1; j <= i + 1; j++)
                {
                    if (j < 0 || j >= n) continue;
                    sum += _displayLevels[j]; cnt++;
                }
                smooth[i] = sum / Math.Max(1, cnt);
            }

            // Render waveform across the full canvas width
            double waveformWidth = width;
            double startX = 0; // start at left edge

            double maxLvl = Math.Max(0.0001, smooth.Max());
            // Apply master volume (preamp) from UI so waveform scales when user adjusts preamp slider
            double masterDb = MasterVolumeSlider?.Value ?? 0.0;
            double masterGain = Math.Pow(10.0, masterDb / 20.0); // dB -> linear
            // Read system master volume scalar and apply so waveform follows system volume too
            double systemGain = 1.0;
            try { systemGain = _systemVolume.GetMasterVolumeScalar(); } catch { systemGain = 1.0; }
            masterGain *= systemGain;
            var points = new List<Point>(n);
            for (int i = 0; i < n; i++)
            {
                double x = startX + i * (waveformWidth / (n - 1));
                double normalized = smooth[i] / maxLvl; // 0..1
                // gentle non-linear mapping for clearer peaks without harsh boost
                // reduce vertical amplitude to half so the waveform height is shorter
                double amp = Math.Pow(normalized, 0.6) * (midY - 6) * 0.5;
                double y = midY - amp; // single-sided waveform (upper hemisphere)
                points.Add(new Point(x, y));
            }

            if (points.Count < 2) return;

            // Build smooth Bezier curve from points (Catmull-Rom -> Bezier)
            var figure = new PathFigure { StartPoint = points[0], IsClosed = false };
            for (int i = 0; i < points.Count - 1; i++)
            {
                Point p0 = i == 0 ? points[0] : points[i - 1];
                Point p1 = points[i];
                Point p2 = points[i + 1];
                Point p3 = i + 2 < points.Count ? points[i + 2] : points[i + 1];

                Point c1 = new(p1.X + (p2.X - p0.X) / 6, p1.Y + (p2.Y - p0.Y) / 6);
                Point c2 = new(p2.X - (p3.X - p1.X) / 6, p2.Y - (p3.Y - p1.Y) / 6);

                figure.Segments.Add(new BezierSegment(c1, c2, p2, true));
            }

            var lineGeometry = new PathGeometry();
            lineGeometry.Figures.Add(figure);

            // Build filled area under curve (to bottom) for subtle filled effect
            var fillFigure = figure.Clone();
            fillFigure.Segments.Add(new LineSegment(new Point(points[^1].X, height), true));
            fillFigure.Segments.Add(new LineSegment(new Point(points[0].X, height), true));
            fillFigure.IsClosed = true;
            var fillGeometry = new PathGeometry();
            fillGeometry.Figures.Add(fillFigure);

            lineGeometry.Freeze();
            fillGeometry.Freeze();

            _waveformLinePath!.Data = lineGeometry;
            _waveformFillPath!.Data = fillGeometry;

            // Position grid lines and labels (logarithmic mapping across 5..20000 Hz)
            double minLog = Math.Log10(_freqTicks.First());
            double maxLog = Math.Log10(_freqTicks.Last());
            for (int i = 0; i < _freqTicks.Length; i++)
            {
                double f = _freqTicks[i];
                double posNorm = (Math.Log10(f) - minLog) / (maxLog - minLog);
                double x = startX + posNorm * waveformWidth;
                var line = _waveformGridLines[i];
                line.X1 = x; line.X2 = x; line.Y1 = 4; line.Y2 = height - 18; // inset

                var tb = _waveformTickLabels[i];
                Canvas.SetLeft(tb, x - 10);
                Canvas.SetTop(tb, height - 16);
            }
        }

        // STEP: đổi hướng - trước đây nút này Stop()/Start() lại toàn bộ WASAPI loopback capture,
        // vốn dễ vỡ khi restart liên tục (dù đã fix race condition dispose, thực tế Kat vẫn gặp
        // waveform đứng im sau vài lần bấm - restart audio engine nhiều lần vẫn không đủ tin cậy).
        // Giờ capture chạy NỀN LIÊN TỤC suốt vòng đời app (đã Start() 1 lần trong
        // SetupAudioVisualizer, không đụng tới nữa) - nút này chỉ bật/tắt HIỂN THỊ, không đụng gì
        // tới audio engine, nên không còn phụ thuộc vào việc restart WASAPI có ổn định hay không.
        private bool _waveformVisible = true;

        // STEP: ResizeMode="NoResize" khiến Windows tự ẩn nút minimize mặc định trên title bar,
        // nên nút MinimizeButton tự vẽ trong header sẽ gọi thẳng WindowState = Minimized ở đây.
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void ToggleWaveformButton_Click(object sender, RoutedEventArgs e)
        {
            _waveformVisible = !_waveformVisible;
            SourceNameText.Text = _waveformVisible
                ? (_audioService.DeviceFriendlyName ?? "thiết bị phát mặc định")
                : "đã tắt sóng nhạc";
        }

        private void CompactViewButton_Click(object sender, RoutedEventArgs e)
        {
            var vis = DbAxisPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            DbAxisPanel.Visibility = vis;
            GroupLabelsRow.Visibility = vis;
        }

        // ===== Master volume (preamp) =====
        private void MasterVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MasterVolumeText == null) return;
            MasterVolumeText.Text = e.NewValue.ToString("+0.0;-0.0;0.0");
            ThrottledWrite();
            // Update waveform visual immediately to reflect preamp change
            DrawWaveform();
        }

        // ===== EQ on/off pill =====
        private void EqToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _eqEnabled = !_eqEnabled;
            EqToggleText.Text = _eqEnabled ? "Stop EQing" : "Start EQing";
            WriteCurrentState();
        }

        // ===== Presets (STEP 9: PresetListBox thay cho PresetComboBox cũ) =====

        /// <summary>Item hiển thị trong PresetListBox — bọc tên preset + có phải mặc định hay
        /// không (để hiện ★), dùng logic có sẵn PresetService.IsDefaultPreset, không tạo logic
        /// duplicate.</summary>
        private class PresetListItem
        {
            public string Name { get; init; } = string.Empty;
            public bool IsDefault { get; init; }
            public Visibility StarVisibility => IsDefault ? Visibility.Visible : Visibility.Collapsed;
        }

        private void LoadPresetNames()
        {
            var items = _presetService.ListPresetNames()
                .Select(n => new PresetListItem { Name = n, IsDefault = _presetService.IsDefaultPreset(n) })
                .ToList();

            PresetListBox.ItemsSource = items;
            if (PresetListBox.Items.Count > 0)
                PresetListBox.SelectedIndex = 0;
        }

        /// <summary>Chọn lại 1 item trong PresetListBox theo tên preset (sau Add/Rename/Overwrite).</summary>
        private void SelectPresetByName(string name)
        {
            if (PresetListBox.ItemsSource is IEnumerable<PresetListItem> items)
                PresetListBox.SelectedItem = items.FirstOrDefault(i =>
                    string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private void PresetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePresetActionButtonsState();
            if (PresetListBox.SelectedItem is not PresetListItem item) return;
            ApplyPreset(item.Name);
        }

        /// <summary>Disable Ghi đè/Đổi tên/Xoá khi preset đang chọn là mặc định — hành vi chặn
        /// thật đã nằm ở PresetService.Delete/Rename (STEP 9 chỉ cần disable nút tương ứng cho
        /// đúng UX, không tạo logic chặn duplicate).</summary>
        private void UpdatePresetActionButtonsState()
        {
            bool isDefault = PresetListBox.SelectedItem is PresetListItem item && item.IsDefault;
            OverwritePresetButton.IsEnabled = !isDefault;
            RenamePresetButton.IsEnabled = !isDefault;
            DeletePresetButton.IsEnabled = !isDefault;
        }

        private void ApplyPreset(string name)
        {
            var preset = _presetService.Load(name);
            if (preset == null) return;

            // Match theo INDEX (không theo Frequency) vì Filter Editor cho phép người dùng đổi
            // Frequency của band tự do - preset vẫn phải load đúng vào cùng 10 "khe" band cố định.
            int count = Math.Min(_bands.Count, preset.Bands.Count);
            for (int i = 0; i < count; i++)
            {
                var band = _bands[i];
                var state = preset.Bands[i];
                band.FrequencyHz = state.FrequencyHz;
                band.Type = state.Type;
                band.Q = state.Q;
                band.Slope = state.Slope;
                band.IsEnabled = state.IsEnabled;
                band.GainDb = state.GainDb; // đặt Gain sau cùng để chỉ trigger 1 lần ghi/vẽ curve
            }
            ResortBandsByFrequency();

            if (MasterVolumeSlider != null)
                MasterVolumeSlider.Value = preset.PreampDb;

            PresetSubtitleText.Text = name;
            WriteCurrentState();
        }

        /// <summary>Chụp lại toàn bộ trạng thái EQ hiện tại (preamp + mọi band: freq/gain/Q/type).</summary>
        private EqPreset CaptureCurrentState(string name)
        {
            var preset = new EqPreset { Name = name, PreampDb = MasterVolumeSlider?.Value ?? 0.0 };
            foreach (var band in _bands)
            {
                preset.Bands.Add(new EqBandState
                {
                    FrequencyHz = band.FrequencyHz,
                    GainDb = band.GainDb,
                    Q = band.Q,
                    Type = band.Type,
                    Slope = band.Slope,
                    IsEnabled = band.IsEnabled
                });
            }
            return preset;
        }

        /// <summary>"+ THÊM PRESET" - luôn tạo 1 preset MỚI với tên do người dùng nhập,
        /// không bao giờ ghi đè lên preset hiện có (đó là chức năng của nút "Ghi đè").</summary>
        private void AddPreset_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new PromptDialog("Tên preset mới:", "My Preset") { Owner = this };
            if (dialog.ShowDialog() != true) return;

            var input = dialog.ResultText;
            if (string.IsNullOrWhiteSpace(input)) return;

            if (_presetService.Exists(input))
            {
                var overwrite = MessageBox.Show(
                    $"Preset \"{input}\" đã tồn tại. Ghi đè?", "Kater1EQ",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (overwrite != MessageBoxResult.Yes) return;
            }

            _presetService.Save(CaptureCurrentState(input));
            LoadPresetNames();
            SelectPresetByName(input);
            PresetSubtitleText.Text = input;
        }

        /// <summary>Ghi đè preset đang chọn với trạng thái EQ hiện tại. Chặn với preset mặc định
        /// (nút tương ứng cũng bị disable khi preset đang chọn là default - xem UpdatePresetActionButtonsState).</summary>
        private void OverwritePreset_Click(object sender, RoutedEventArgs e)
        {
            if (PresetListBox.SelectedItem is not PresetListItem selected) return;
            var name = selected.Name;

            if (_presetService.IsDefaultPreset(name))
            {
                MessageBox.Show("Không thể ghi đè preset mặc định. Hãy dùng \"+ THÊM PRESET\" để lưu thành preset mới.",
                    "Kater1EQ", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _presetService.Save(CaptureCurrentState(name));
            LoadPresetNames();
            SelectPresetByName(name);
        }

        private void RenamePreset_Click(object sender, RoutedEventArgs e)
        {
            if (PresetListBox.SelectedItem is not PresetListItem selected) return;
            var name = selected.Name;

            if (_presetService.IsDefaultPreset(name))
            {
                MessageBox.Show("Không thể đổi tên preset mặc định.", "Kater1EQ",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new PromptDialog("Tên mới:", name) { Owner = this };
            if (dialog.ShowDialog() != true) return;

            var newName = dialog.ResultText;
            if (string.IsNullOrWhiteSpace(newName) || newName == name) return;

            if (!_presetService.Rename(name, newName))
            {
                MessageBox.Show($"Không thể đổi tên: preset \"{newName}\" đã tồn tại.", "Kater1EQ",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadPresetNames();
            SelectPresetByName(newName);
            PresetSubtitleText.Text = newName;
        }

        private void DeletePreset_Click(object sender, RoutedEventArgs e)
        {
            if (PresetListBox.SelectedItem is not PresetListItem selected) return;
            var name = selected.Name;

            if (_presetService.IsDefaultPreset(name))
            {
                MessageBox.Show("Preset mặc định của ứng dụng không thể xoá.", "Kater1EQ",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show($"Xoá preset \"{name}\"?", "Kater1EQ",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            _presetService.Delete(name);
            LoadPresetNames();
        }

        private void ResetFlat_Click(object sender, RoutedEventArgs e)
        {
            foreach (var band in _bands)
                band.GainDb = 0;
            MasterVolumeSlider.Value = 0;
            PresetSubtitleText.Text = "None";
            WriteCurrentState();
        }

        // ===== Bottom nav =====
        private enum MainTab { Eq, Presets, Social }

        private void NavEq_Click(object sender, RoutedEventArgs e) => ShowTab(MainTab.Eq);
        private void NavPresets_Click(object sender, RoutedEventArgs e) => ShowTab(MainTab.Presets);
        private void NavSocial_Click(object sender, RoutedEventArgs e) => ShowTab(MainTab.Social);

        private void ShowTab(MainTab tab)
        {
            // Vùng nội dung chính: EQ (curve/waveform) hoặc Social - luôn loại trừ nhau.
            EqMainContentGrid.Visibility = tab == MainTab.Social ? Visibility.Collapsed : Visibility.Visible;
            AnimatePanelVisibility(SocialPanel, SocialPanelTransform, show: tab == MainTab.Social);

            // Cảnh báo âm lượng chỉ có ý nghĩa khi đang xem EQ/Presets.
            WarningPanel.Visibility = tab == MainTab.Social ? Visibility.Collapsed : Visibility.Visible;

            // Action bar dưới cùng: chỉ 1 trong 2 panel EQ/Presets hiện, Social không có action bar riêng.
            EqActionsPanel.Visibility = tab == MainTab.Eq ? Visibility.Visible : Visibility.Collapsed;
            PresetsActionsPanel.Visibility = tab == MainTab.Presets ? Visibility.Visible : Visibility.Collapsed;

            // Ở tab Presets: bỏ hẳn waveform (đỡ rối mắt/chồng lấn với danh sách preset bên dưới),
            // nhường không gian đó cho đồ thị EQ (curve) - Row "*" của curve tự giãn ra lấp chỗ
            // trống khi row waveform co về 0. KHÔNG đụng audio capture/_waveformVisible - chỉ ẩn
            // hiển thị, TickWaveform vẫn chạy nền bình thường như cũ (đúng kiến trúc waveform).
            bool hideWaveform = tab == MainTab.Presets;
            WaveformBorder.Visibility = hideWaveform ? Visibility.Collapsed : Visibility.Visible;
            WaveformRowDef.Height = hideWaveform ? new GridLength(0) : new GridLength(130);

            var accent = ThemeBrush("AccentColor", Brushes.Goldenrod);
            var dim = ThemeBrush("TextSecondary", Brushes.LightGray);
            NavEqButton.Foreground = tab == MainTab.Eq ? accent : dim;
            NavPresetsButton.Foreground = tab == MainTab.Presets ? accent : dim;
            NavSocialButton.Foreground = tab == MainTab.Social ? accent : dim;
        }

        private void NavComingSoon_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Tính năng này sẽ có trong bản cập nhật tiếp theo.", "Kater1EQ",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ===== Social (credit tác giả — link CỐ ĐỊNH, không cho sửa) =====

        /// <summary>Map tên nền tảng -> TextBlock hiển thị URL, dùng để đổ dữ liệu từ
        /// DeveloperSocialLinks.All lên UI mà không lặp code cho từng dòng.</summary>
        private Dictionary<string, TextBlock> SocialLinkTextBlocks => new()
        {
            ["Facebook"] = FacebookLinkText,
            ["Instagram"] = InstagramLinkText,
            ["GitHub"] = GitHubLinkText,
            ["TikTok"] = TikTokLinkText,
            ["Steam"] = SteamLinkText,
        };

        /// <summary>Đổ danh sách link cố định của tác giả lên UI (chỉ hiển thị, không đọc/ghi
        /// file - dữ liệu nằm cố định trong DeveloperSocialLinks).</summary>
        private void LoadSocialLinks()
        {
            var textBlocks = SocialLinkTextBlocks;
            foreach (var link in DeveloperSocialLinks.All)
            {
                if (textBlocks.TryGetValue(link.Name, out var block))
                    block.Text = link.Url;
            }
        }

        private void DeveloperSocialLink_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement el || el.Tag is not string name) return;

            var link = DeveloperSocialLinks.All.FirstOrDefault(l =>
                string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
            if (link == null) return;

            if (!Uri.TryCreate(link.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                MessageBox.Show("Đường dẫn không hợp lệ.", "Kater1EQ",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = uri.ToString(),
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể mở đường dẫn: {ex.Message}", "Kater1EQ",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ===== System tray =====
        private void SetupTrayIcon()
        {
            System.Drawing.Icon trayIconImage;
            try
            {
                trayIconImage = new System.Drawing.Icon(
                    System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
            }
            catch
            {
                // Fallback nếu vì lý do gì đó không tìm thấy app.ico lúc runtime, tránh crash app.
                trayIconImage = System.Drawing.SystemIcons.Application;
            }

            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                // STEP: trước đây dùng SystemIcons.Application (icon Windows mặc định) khiến
                // icon dưới khay hệ thống không phải logo Kater1EQ. Load trực tiếp từ app.ico
                // đóng gói kèm exe (Content) để icon khay khớp icon app.
                Icon = trayIconImage,
                Visible = true,
                Text = "Kater1EQ"
            };

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Mở Kater1EQ", null, (s, e) => ShowFromTray());
            menu.Items.Add("Thoát", null, (s, e) => System.Windows.Application.Current.Shutdown());
            _trayIcon.ContextMenuStrip = menu;

            _trayIcon.DoubleClick += (s, e) => ShowFromTray();
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        // STEP: trước đây OnStateChanged sẽ Hide() cửa sổ ngay khi Minimized, khiến app biến mất
        // hoàn toàn thay vì xuống taskbar (chỉ còn icon dưới khay hệ thống). Bỏ hành vi này để
        // nút thu nhỏ hoạt động bình thường như mọi app Windows khác: thu xuống taskbar, bấm vào
        // icon dưới taskbar là mở lại được. Khay hệ thống (tray) vẫn giữ nguyên, chỉ dùng khi
        // bấm nút X (xem OnClosing bên dưới).
        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // STEP: trước đây bấm X sẽ Cancel + Hide() để thu xuống tray, EQ vẫn chạy nền.
            // Theo yêu cầu, giờ bấm X sẽ thoát app luôn (không override, để WPF đóng cửa sổ và
            // tắt process bình thường). Muốn chạy nền thì dùng nút thu nhỏ (xuống taskbar) thay
            // vì X.
            base.OnClosing(e);
        }

        protected override void OnClosed(EventArgs e)
        {
            _waveformTimer?.Stop();
            _audioService.Dispose();
            _trayIcon?.Dispose();
            base.OnClosed(e);
        }
    }
}
