# Pixel font asset location

Đặt file font pixel thật vào thư mục này.

`Themes/PixelStyles.xaml` (theme Pixel) trỏ tới `./Assets/Fonts/#Press Start 2P` với
fallback Consolas/Segoe UI - app KHÔNG crash nếu chưa có file font ở đây, chỉ tự
dùng font fallback cho tới khi file thật được thêm vào.

`Themes/PinkTheme.xaml` (theme Pink) trỏ tới `./Assets/Fonts/#Monocraft` (font kiểu
Minecraft, mã nguồn mở MIT - AN TOÀN bản quyền, không phải font chính thức của Mojang):

  1. Tải file `.ttf` tại: https://github.com/IdreesInc/Monocraft/releases
     (chọn `Monocraft.ttf` bản thường, không cần bản nerd-fonts)
  2. Đổi tên/bỏ nguyên file vào thư mục này (`Assets/Fonts/Monocraft.ttf`)
  3. Build lại - theme Pink sẽ tự nhận diện, không cần sửa code

Muốn dùng "Minecraftia" (font freeware khác, giống UI gốc Minecraft hơn nhưng
không phải open-source) thay vì Monocraft: tải tại dafont.com/minecraftia.font,
rồi đổi `#Monocraft` thành `#Minecraftia` trong `PinkTheme.xaml` (dòng khai báo
`x:Key="AppFont"`).

KHÔNG dùng font chính thức "Minecraft Seven"/"Mojangles" của Mojang - Mojang chỉ
cho phép dùng trong nội dung liên quan trực tiếp tới Minecraft, không phải để nhúng
vào app bên ngoài.
