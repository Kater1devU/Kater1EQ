# Kater1EQ

App equalizer toàn hệ thống (system-wide) cho Windows, dùng WPF (.NET 8) + Equalizer APO làm engine xử lý âm thanh.

## 1. Yêu cầu trước khi build

1. **Visual Studio 2022** (bản Community miễn phí) — cài kèm workload **".NET desktop development"**.
2. **.NET 8 SDK** — thường Visual Studio tự cài kèm, nếu chưa có: https://dotnet.microsoft.com/download/dotnet/8.0
3. **Equalizer APO** — driver xử lý audio toàn hệ thống, bắt buộc phải cài trước:
   - Tải tại: https://equalizerapo.com
   - Khi cài, nhớ **tick chọn đúng thiết bị output** (loa/tai nghe) bạn muốn EQ ở màn hình "Select capture devices".
   - Sau khi cài xong, **khởi động lại máy** 1 lần.

## 2. Cách build

1. Mở file `Kater1EQ.sln` bằng Visual Studio.
2. Nhấn `Ctrl+Shift+B` để build, hoặc `F5` để chạy thử ngay.
3. Nếu Visual Studio báo thiếu SDK, vào Tools → Get Tools and Features → tick ".NET desktop development" → Modify.

## 3. Cấu trúc project

```
Kater1EQ/
├── App.xaml / App.xaml.cs          # Điểm khởi động app
├── MainWindow.xaml / .cs           # Giao diện chính (10 band EQ)
├── PromptDialog.xaml / .cs         # Dialog nhỏ để đặt tên preset
├── Models/
│   ├── EqBand.cs                   # 1 band tần số (frequency + gain dB)
│   └── EqPreset.cs                 # 1 preset (tập hợp các band)
├── Services/
│   ├── EqualizerApoService.cs      # Ghi cấu hình xuống Equalizer APO
│   └── PresetService.cs            # Lưu/load preset dạng JSON trong %AppData%
├── Themes/
│   └── DarkTheme.xaml              # Toàn bộ style/màu sắc — SỬA Ở ĐÂY để đổi UI
└── Assets/
    └── app.ico                     # Icon app (thay bằng icon riêng nếu muốn)
```

## 4. Cách EQ hoạt động (giải thích ngắn)

- Equalizer APO đọc file `config.txt` trong thư mục cài đặt của nó (`C:\Program Files\EqualizerAPO\config\`).
- Kater1EQ **không** ghi trực tiếp vào `config.txt` để tránh phá cấu hình có sẵn của bạn. Thay vào đó nó:
  1. Thêm đúng 1 dòng `Include: Kater1EQ.txt` vào cuối `config.txt` (chỉ thêm 1 lần).
  2. Ghi toàn bộ giá trị EQ hiện tại vào file riêng `Kater1EQ.txt` theo định dạng `GraphicEQ`.
- Equalizer APO tự động theo dõi và áp dụng thay đổi file gần như ngay lập tức (không cần restart app nào khác).
- Khi bạn tắt công tắc (toggle) trên app, Kater1EQ ghi file rỗng để trả âm thanh về nguyên gốc.

## 5. Chỗ để bạn tuỳ chỉnh UI

Bạn nói muốn tự chỉnh UI, đây là các điểm chạm chính:

- **Màu sắc / theme**: `Themes/DarkTheme.xaml` — đổi các `SolidColorBrush` ở đầu file (`AccentColor`, `BgPrimary`,...).
- **Layout tổng thể**: `MainWindow.xaml` — hiện đang là Grid 3 hàng (Header / Bands / Footer).
- **Hình dạng slider**: style `EqVerticalSlider` và `EqSliderThumb` trong `DarkTheme.xaml`.
- **Icon app**: thay file `Assets/app.ico` bằng icon riêng của bạn (giữ đúng tên file hoặc sửa lại trong `Kater1EQ.csproj`).
- **Logo trong Header**: sửa phần `StackPanel` chứa `Ellipse` + `TextBlock "Kater1EQ"` trong `MainWindow.xaml`.

## 6. Tính năng hiện có (MVP)

- [x] 10-band Graphic EQ (31Hz – 16kHz), kéo thả mượt.
- [x] Áp dụng EQ toàn hệ thống qua Equalizer APO.
- [x] 4 preset mẫu: Flat, Bass Boost, Gaming - Footsteps, Vocal Clarity.
- [x] Lưu/xoá preset tuỳ chỉnh (lưu tại `%AppData%\Kater1EQ\Presets`).
- [x] Bật/tắt nhanh (master toggle).
- [x] Thu nhỏ xuống system tray khi bấm nút X (app tiếp tục chạy nền).

## 7. Việc cần làm tiếp (gợi ý roadmap)

- [ ] Hotkey toàn cục để bật/tắt nhanh (dùng `RegisterHotKey` Win32 API).
- [ ] Đồ thị đường cong tần số (vẽ Path/Polyline nối các điểm band).
- [ ] Publish self-contained, trimmed để giảm dung lượng cài đặt:
  ```
  dotnet publish -c Release -r win-x64 --self-contained true -p:PublishTrimmed=true -p:PublishSingleFile=true
  ```
- [ ] Installer bằng Inno Setup — tự động kiểm tra và cài Equalizer APO nếu chưa có.
- [ ] Auto-start cùng Windows (tuỳ chọn trong Settings).

## 8. Lưu ý về hiệu năng ("nhẹ nhất có thể")

- WPF + .NET 8 framework-dependent build ra khoảng **15–30MB**, khởi động nhanh hơn Electron/Unity đáng kể.
- Nếu muốn không cần cài .NET Runtime riêng trên máy người dùng, dùng `--self-contained true`, đổi lại dung lượng cài đặt sẽ tăng lên (~60-100MB) nhưng vẫn nhẹ hơn nhiều so với Unity.
- App **không** tự xử lý DSP/audio — toàn bộ việc lọc âm thanh do Equalizer APO (driver C++ hiệu năng cao) đảm nhiệm, nên gần như không tốn CPU/RAM đáng kể khi chạy nền.
# Kater1EQ
