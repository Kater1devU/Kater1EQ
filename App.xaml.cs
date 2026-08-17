using System;
using System.Windows;
using Kater1EQ.Models;
using Kater1EQ.Services;

namespace Kater1EQ
{
    public partial class App : Application
    {
        private static readonly SettingsService SettingsService = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Bắt lỗi không xử lý được để app không crash im lặng
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                MessageBox.Show(
                    $"Đã xảy ra lỗi:\n{args.ExceptionObject}",
                    "Kater1EQ - Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            // STEP 10: đọc theme đã lưu (settings.json) và áp dụng TRƯỚC khi tạo MainWindow,
            // thay vì luôn hard-code DarkTheme.xaml như trước STEP 10.
            var settings = SettingsService.Load();
            ApplyTheme(settings.Theme);

            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        /// <summary>
        /// Đổi theme lúc runtime bằng cách thay Application.Current.Resources.MergedDictionaries
        /// (không restart app). Dùng {DynamicResource} xuyên suốt XAML để control tự cập nhật
        /// khi dictionary bị thay. Gọi từ SettingsPanel (STEP 14) hoặc lúc khởi động (ở trên).
        /// </summary>
        public static void ApplyTheme(AppTheme theme)
        {
            var dictionaries = Current.Resources.MergedDictionaries;
            dictionaries.Clear();

            switch (theme)
            {
                case AppTheme.Dark:
                    dictionaries.Add(new ResourceDictionary { Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative) });
                    break;

                case AppTheme.Pink:
                    dictionaries.Add(new ResourceDictionary { Source = new Uri("Themes/PinkTheme.xaml", UriKind.Relative) });
                    break;

                case AppTheme.Pixel:
                default:
                    dictionaries.Add(new ResourceDictionary { Source = new Uri("Themes/PixelTheme.xaml", UriKind.Relative) });
                    dictionaries.Add(new ResourceDictionary { Source = new Uri("Themes/PixelFonts.xaml", UriKind.Relative) });
                    dictionaries.Add(new ResourceDictionary { Source = new Uri("Themes/PixelStyles.xaml", UriKind.Relative) });
                    break;
            }
        }

        /// <summary>Đổi theme và lưu lại ngay (dùng khi người dùng chọn theme trong SettingsPanel).</summary>
        public static void ApplyAndSaveTheme(AppTheme theme)
        {
            ApplyTheme(theme);
            SettingsService.Save(new Settings { Theme = theme });
        }
    }
}
