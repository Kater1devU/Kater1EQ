using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Kater1EQ.Services
{
    /// <summary>
    /// Bắt lại (loopback) âm thanh đang phát ra loa/tai nghe mặc định của Windows,
    /// KHÔNG thu âm micro. Dùng để vẽ sóng nhạc real-time phía sau đường cong EQ.
    /// </summary>
    public sealed class AudioLoopbackService : IDisposable
    {
        /// <summary>Số cột biên độ trả về mỗi lần có dữ liệu mới.</summary>
        public const int BarCount = 64;

        private WasapiLoopbackCapture? _capture;
        private MMDevice? _device;

        /// <summary>Bắn ra mảng biên độ (0..1) mỗi khi có 1 khối audio mới. Chạy trên thread audio, không phải UI thread.</summary>
        public event Action<float[]>? SamplesAvailable;

        /// <summary>Tên thiết bị phát hiện đang được lắng nghe (vd "Loa (Realtek Audio)").</summary>
        public string? DeviceFriendlyName => _device?.FriendlyName;

        public bool IsRunning { get; private set; }

        /// <summary>
        /// Bắt đầu lắng nghe thiết bị phát mặc định. Trả về false nếu máy không có
        /// thiết bị phát nào hoặc WASAPI không khởi tạo được (một số driver ảo/RDP không hỗ trợ loopback).
        /// </summary>
        public bool Start()
        {
            if (IsRunning) return true;

            try
            {
                var enumerator = new MMDeviceEnumerator();
                _device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                var capture = new WasapiLoopbackCapture(_device);
                capture.DataAvailable += Capture_DataAvailable;
                // BUG FIX: closure giữ tham chiếu riêng tới "capture" cục bộ (không phải field
                // _capture) để khi RecordingStopped bắn ra, nó luôn Dispose() đúng CHÍNH object đã
                // dừng - kể cả khi field _capture lúc đó đã được Start() kế tiếp gán sang 1 capture
                // mới. Trước đây dùng field trong closure khiến Start() thứ 2 vô tình mất capture
                // mới (bị closure của lần Stop() trước null hoá / dispose nhầm) -> waveform không
                // còn nhận DataAvailable, đứng im thành 1 đường thẳng sau khi tắt rồi bật lại.
                capture.RecordingStopped += (_, _) =>
                {
                    try { capture.Dispose(); } catch { /* ignore */ }
                };
                capture.StartRecording();
                _capture = capture;
                IsRunning = true;
                return true;
            }
            catch
            {
                // Không có thiết bị phát / thiết bị không hỗ trợ loopback / bị chiếm bởi app khác ở exclusive mode
                IsRunning = false;
                return false;
            }
        }

        private void Capture_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_capture == null || e.BytesRecorded == 0) return;

            // WasapiLoopbackCapture luôn trả về IEEE Float (mix format của thiết bị)
            int bytesPerSample = _capture.WaveFormat.BitsPerSample / 8;
            int channels = Math.Max(1, _capture.WaveFormat.Channels);
            int frameCount = e.BytesRecorded / (bytesPerSample * channels);
            if (frameCount <= 0) return;

            var levels = new float[BarCount];
            int framesPerBar = Math.Max(1, frameCount / BarCount);

            for (int bar = 0; bar < BarCount; bar++)
            {
                float peak = 0f;
                int startFrame = bar * framesPerBar;
                int endFrame = Math.Min(startFrame + framesPerBar, frameCount);

                for (int frame = startFrame; frame < endFrame; frame++)
                {
                    // Chỉ đọc kênh đầu tiên là đủ cho mục đích trực quan hoá
                    int byteOffset = frame * bytesPerSample * channels;
                    if (byteOffset + 4 > e.BytesRecorded) break;

                    float sample = BitConverter.ToSingle(e.Buffer, byteOffset);
                    float abs = Math.Abs(sample);
                    if (abs > peak) peak = abs;
                }

                levels[bar] = Math.Min(1f, peak);
            }

            SamplesAvailable?.Invoke(levels);
        }

        public void Stop()
        {
            if (!IsRunning) return;

            // IsRunning tắt NGAY (đồng bộ) để nút bấm / TickWaveform phản hồi tức thì. Việc dọn
            // dẹp _capture thật sự (Dispose) do closure đăng ký trong Start() đảm nhiệm khi
            // RecordingStopped bắn ra - không dispose ở đây để tránh dispose khi capture engine
            // của NAudio chưa kịp dừng hẳn (nguyên nhân gây waveform đứng im sau khi bật lại).
            IsRunning = false;
            try
            {
                _capture?.StopRecording();
            }
            catch
            {
                // ignore - nếu StopRecording tự lỗi, RecordingStopped có thể không bắn ra, nhưng
                // capture cũ dù rò rỉ nhẹ cũng không ảnh hưởng vì field đã được thả ở dưới.
            }
            _capture = null;
        }

        public void Dispose() => Stop();
    }
}
