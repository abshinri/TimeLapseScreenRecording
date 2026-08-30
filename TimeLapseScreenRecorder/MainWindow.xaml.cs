using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;

namespace TimeLapseScreenRecorder
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _captureTimer = new();
        private readonly List<ScreenInfo> _screens = new();
        private Bitmap? _lastCapturedBitmap;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            _captureTimer.Tick += CaptureTimer_Tick;

            CaptureFormatComboBox.ItemsSource = new[] { "PNG", "JPG", "BMP" };
            CaptureQualityComboBox.ItemsSource = new[] { "低质量(50)", "中等质量(75)", "高质量(90)", "极高质量(100)" };
            CaptureFormatComboBox.SelectedIndex = 0;
            CaptureQualityComboBox.SelectedIndex = 2;
            CaptureIntervalText.Text = "10";
            VideoFpsText.Text = "10";
            VideoOutputText.Text = "timelapse.mp4";
            StopCaptureButton.IsEnabled = false;
            CaptureLogText.Visibility = Visibility.Collapsed;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PopulateMonitorList();
            ApplyConfig(LoadConfig());
            UpdateCaptureLogVisibility();
        }

        private void ShowCaptureLogCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateCaptureLogVisibility();
        }

        private void ShowCaptureLogCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateCaptureLogVisibility();
        }

        private void UpdateCaptureLogVisibility()
        {
            CaptureLogText.Visibility = ShowCaptureLogCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _lastCapturedBitmap?.Dispose();
            _lastCapturedBitmap = null;
            SaveConfig();
        }

        private void PopulateMonitorList()
        {
            _screens.Clear();
            var screens = Screen.AllScreens;
            foreach (var screen in screens)
            {
                _screens.Add(new ScreenInfo(screen.DeviceName, screen.Bounds));
            }

            MonitorComboBox.ItemsSource = _screens.Select(s => s.Name).ToList();
            if (_screens.Count > 0)
            {
                MonitorComboBox.SelectedIndex = 0;
            }
        }

        private static string GetConfigPath()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var directory = Path.Combine(localAppData, "TimeLapseScreenRecorder");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "config.json");
        }

        private AppConfig LoadConfig()
        {
            var configPath = GetConfigPath();
            if (!File.Exists(configPath))
            {
                return new AppConfig();
            }

            try
            {
                var json = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            catch
            {
                return new AppConfig();
            }
        }

        private void SaveConfig()
        {
            var config = BuildConfig();
            var configPath = GetConfigPath();

            try
            {
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
            catch
            {
                // Ignore persistence failures to prevent the app from crashing during shutdown.
            }
        }

        private AppConfig BuildConfig()
        {
            return new AppConfig
            {
                CaptureFolder = CaptureFolderText.Text,
                CaptureIntervalSeconds = CaptureIntervalText.Text,
                CaptureFormat = CaptureFormatComboBox.SelectedItem?.ToString() ?? "PNG",
                CaptureQuality = CaptureQualityComboBox.SelectedItem?.ToString() ?? "高质量(90)",
                SkipUnchangedCapture = SkipUnchangedCaptureCheckBox.IsChecked == true,
                SelectedScreenIndex = MonitorComboBox.SelectedIndex,
                DedupeFolder = DedupeFolderText.Text,
                VideoFolder = VideoFolderText.Text,
                VideoFps = VideoFpsText.Text,
                VideoFfmpegPath = VideoFfmpegPathText.Text,
                VideoOutputDirectory = VideoOutputDirectoryText.Text,
                VideoOutputFileName = VideoOutputText.Text,
            };
        }

        private void ApplyConfig(AppConfig config)
        {
            if (!string.IsNullOrWhiteSpace(config.CaptureFolder))
            {
                CaptureFolderText.Text = config.CaptureFolder;
            }

            if (!string.IsNullOrWhiteSpace(config.CaptureIntervalSeconds))
            {
                CaptureIntervalText.Text = config.CaptureIntervalSeconds;
            }

            if (!string.IsNullOrWhiteSpace(config.CaptureFormat))
            {
                var formatIndex = CaptureFormatComboBox.Items.IndexOf(config.CaptureFormat);
                if (formatIndex >= 0)
                {
                    CaptureFormatComboBox.SelectedIndex = formatIndex;
                }
            }

            if (!string.IsNullOrWhiteSpace(config.CaptureQuality))
            {
                var qualityIndex = CaptureQualityComboBox.Items.IndexOf(config.CaptureQuality);
                if (qualityIndex >= 0)
                {
                    CaptureQualityComboBox.SelectedIndex = qualityIndex;
                }
            }

            SkipUnchangedCaptureCheckBox.IsChecked = config.SkipUnchangedCapture;

            if (config.SelectedScreenIndex >= 0 && config.SelectedScreenIndex < MonitorComboBox.Items.Count)
            {
                MonitorComboBox.SelectedIndex = config.SelectedScreenIndex;
            }

            if (!string.IsNullOrWhiteSpace(config.DedupeFolder))
            {
                DedupeFolderText.Text = config.DedupeFolder;
            }

            if (!string.IsNullOrWhiteSpace(config.VideoFolder))
            {
                VideoFolderText.Text = config.VideoFolder;
            }

            if (!string.IsNullOrWhiteSpace(config.VideoFps))
            {
                VideoFpsText.Text = config.VideoFps;
            }

            if (!string.IsNullOrWhiteSpace(config.VideoFfmpegPath))
            {
                VideoFfmpegPathText.Text = config.VideoFfmpegPath;
            }

            if (!string.IsNullOrWhiteSpace(config.VideoOutputDirectory))
            {
                VideoOutputDirectoryText.Text = config.VideoOutputDirectory;
            }

            if (!string.IsNullOrWhiteSpace(config.VideoOutputFileName))
            {
                VideoOutputText.Text = config.VideoOutputFileName;
            }
        }

        private ScreenInfo? GetSelectedScreen()
        {
            if (MonitorComboBox.SelectedIndex < 0)
            {
                return null;
            }

            return _screens[MonitorComboBox.SelectedIndex];
        }

        private void ChooseCaptureFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "选择截图保存目录",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                CaptureFolderText.Text = dialog.SelectedPath;
            }
        }

        private void OpenCaptureFolderButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFolder(CaptureFolderText.Text, "保存文件夹为空，无法打开。");
        }

        private void StartCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CaptureFolderText.Text))
            {
                CaptureLogText.AppendText("请先选择保存文件夹。\n");
                return;
            }

            if (!Directory.Exists(CaptureFolderText.Text))
            {
                Directory.CreateDirectory(CaptureFolderText.Text);
            }

            if (!double.TryParse(CaptureIntervalText.Text, out var intervalSeconds) || intervalSeconds <= 0)
            {
                CaptureLogText.AppendText("截屏间隔必须是大于 0 的数字。\n");
                return;
            }

            _captureTimer.Interval = TimeSpan.FromSeconds(intervalSeconds);
            StartCaptureButton.IsEnabled = false;
            StopCaptureButton.IsEnabled = true;
            CaptureCurrentScreen();
            _captureTimer.Start();
            CaptureLogText.AppendText($"已开始定时截屏：间隔 {intervalSeconds} 秒\n");
        }

        private void StopCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            _captureTimer.Stop();
            StartCaptureButton.IsEnabled = true;
            StopCaptureButton.IsEnabled = false;
            CaptureLogText.AppendText("已停止定时截屏。\n");
        }

        private void CaptureTimer_Tick(object? sender, EventArgs e)
        {
            CaptureCurrentScreen();
        }

        private void CaptureCurrentScreen()
        {
            var folder = CaptureFolderText.Text.Trim();
            if (string.IsNullOrWhiteSpace(folder))
            {
                CaptureLogText.AppendText("保存文件夹为空，无法截屏。\n");
                return;
            }

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var screen = GetSelectedScreen();
            if (screen == null)
            {
                CaptureLogText.AppendText("未选择有效屏幕。\n");
                return;
            }

            try
            {
                using var bitmap = new Bitmap(screen.Bounds.Width, screen.Bounds.Height, PixelFormat.Format32bppArgb);
                using var graphics = Graphics.FromImage(bitmap);
                graphics.CopyFromScreen(screen.Bounds.Left, screen.Bounds.Top, 0, 0, screen.Bounds.Size, CopyPixelOperation.SourceCopy);

                if (SkipUnchangedCaptureCheckBox.IsChecked == true && _lastCapturedBitmap != null)
                {
                    if (AreBitmapsEquivalent(_lastCapturedBitmap, bitmap))
                    {
                        CaptureLogText.AppendText("检测到屏幕无变化，跳过保存当前截图。\n");
                        return;
                    }
                }

                var selectedFormat = CaptureFormatComboBox.SelectedItem?.ToString() ?? "PNG";
                var extension = selectedFormat.ToUpperInvariant() switch
                {
                    "PNG" => ".png",
                    "JPG" => ".jpg",
                    "BMP" => ".bmp",
                    _ => ".png"
                };

                var fileName = $"{DateTime.Now:yyyyMMdd_HHmmssfff}{extension}";
                var filePath = Path.Combine(folder, fileName);

                var qualityValue = GetCaptureQuality();
                if (selectedFormat.Equals("JPG", StringComparison.OrdinalIgnoreCase))
                {
                    SaveJpeg(bitmap, filePath, qualityValue);
                }
                else
                {
                    bitmap.Save(filePath, GetImageFormat(selectedFormat));
                }

                _lastCapturedBitmap?.Dispose();
                _lastCapturedBitmap = new Bitmap(bitmap);

                CaptureLogText.AppendText($"已保存：{filePath}\n");
            }
            catch (Exception ex)
            {
                CaptureLogText.AppendText($"截屏失败：{ex.Message}\n");
            }
        }

        private int GetCaptureQuality()
        {
            var selected = CaptureQualityComboBox.SelectedItem?.ToString();
            return selected switch
            {
                "低质量(50)" => 50,
                "中等质量(75)" => 75,
                "高质量(90)" => 90,
                "极高质量(100)" => 100,
                _ => 90
            };
        }

        private static ImageFormat GetImageFormat(string formatName)
        {
            return formatName.ToUpperInvariant() switch
            {
                "PNG" => ImageFormat.Png,
                "JPG" => ImageFormat.Jpeg,
                "BMP" => ImageFormat.Bmp,
                _ => ImageFormat.Png
            };
        }

        private static void SaveJpeg(Bitmap bitmap, string filePath, int quality)
        {
            var qualityParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            var encoderParams = new EncoderParameters(1)
            {
                Param = new[] { qualityParam }
            };

            var codecInfo = ImageCodecInfo.GetImageEncoders()
                .FirstOrDefault(codec => codec.FormatID == ImageFormat.Jpeg.Guid) ??
                ImageCodecInfo.GetImageEncoders().First();

            bitmap.Save(filePath, codecInfo, encoderParams);
        }

        private static void OpenFolder(string folderPath, string emptyMessage)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                System.Windows.MessageBox.Show(emptyMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!Directory.Exists(folderPath))
            {
                System.Windows.MessageBox.Show("当前文件夹不存在：" + folderPath, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true
            });
        }

        private static bool AreBitmapsEquivalent(Bitmap firstBitmap, Bitmap secondBitmap)
        {
            if (firstBitmap.Width != secondBitmap.Width || firstBitmap.Height != secondBitmap.Height)
            {
                return false;
            }

            var width = firstBitmap.Width;
            var height = firstBitmap.Height;
            var maxDimension = 64;
            var resizeWidth = Math.Min(width, maxDimension);
            var resizeHeight = Math.Min(height, maxDimension);

            using var firstSample = new Bitmap(firstBitmap, resizeWidth, resizeHeight);
            using var secondSample = new Bitmap(secondBitmap, resizeWidth, resizeHeight);

            long totalDifference = 0;
            long maxDifference = (long)(resizeWidth * resizeHeight * 3 * 255);

            for (var y = 0; y < resizeHeight; y++)
            {
                for (var x = 0; x < resizeWidth; x++)
                {
                    var firstColor = firstSample.GetPixel(x, y);
                    var secondColor = secondSample.GetPixel(x, y);
                    totalDifference += Math.Abs(firstColor.R - secondColor.R);
                    totalDifference += Math.Abs(firstColor.G - secondColor.G);
                    totalDifference += Math.Abs(firstColor.B - secondColor.B);
                }
            }

            if (maxDifference == 0)
            {
                return true;
            }

            var normalizedDifference = (double)totalDifference / maxDifference;
            return normalizedDifference < 0.01;
        }

        private void ChooseDedupeFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "选择要去重的图片目录",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DedupeFolderText.Text = dialog.SelectedPath;
            }
        }

        private void OpenDedupeFolderButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFolder(DedupeFolderText.Text, "图片文件夹为空，无法打开。");
        }

        private void RemoveDuplicatesButton_Click(object sender, RoutedEventArgs e)
        {
            var folder = DedupeFolderText.Text.Trim();
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                DedupeLogText.AppendText("请先选择存在的图片文件夹。\n");
                return;
            }

            var files = GetSupportedImageFiles(folder).OrderBy(file => file, StringComparer.OrdinalIgnoreCase).ToList();
            if (files.Count == 0)
            {
                DedupeLogText.AppendText("该文件夹中没有可处理的图片文件。\n");
                return;
            }

            int kept = 0;
            int deleted = 0;
            string? previousKeptFile = null;

            foreach (var file in files)
            {
                if (previousKeptFile == null || !AreImagesEquivalent(previousKeptFile, file))
                {
                    previousKeptFile = file;
                    kept++;
                    continue;
                }

                try
                {
                    File.Delete(file);
                    deleted++;
                    DedupeLogText.AppendText($"已删除重复图片：{Path.GetFileName(file)}\n");
                }
                catch (Exception ex)
                {
                    DedupeLogText.AppendText($"删除失败：{Path.GetFileName(file)}，原因：{ex.Message}\n");
                }
            }

            DedupeLogText.AppendText($"处理完成：保留 {kept} 张，删除 {deleted} 张重复图片。\n");
        }

        private static List<string> GetSupportedImageFiles(string folder)
        {
            var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png",
                ".jpg",
                ".jpeg",
                ".bmp",
                ".gif",
                ".tif",
                ".tiff"
            };

            return Directory.EnumerateFiles(folder)
                .Where(file => supportedExtensions.Contains(Path.GetExtension(file)))
                .ToList();
        }

        private static bool AreImagesEquivalent(string firstFile, string secondFile)
        {
            try
            {
                using var firstImage = new Bitmap(firstFile);
                using var secondImage = new Bitmap(secondFile);

                if (firstImage.Width != secondImage.Width || firstImage.Height != secondImage.Height)
                {
                    return false;
                }

                var width = firstImage.Width;
                var height = firstImage.Height;
                var maxDimension = 64;
                var resizeWidth = Math.Min(width, maxDimension);
                var resizeHeight = Math.Min(height, maxDimension);

                using var firstSample = new Bitmap(firstImage, resizeWidth, resizeHeight);
                using var secondSample = new Bitmap(secondImage, resizeWidth, resizeHeight);

                long totalDifference = 0;
                long maxDifference = (long)(resizeWidth * resizeHeight * 3 * 255);

                for (var y = 0; y < resizeHeight; y++)
                {
                    for (var x = 0; x < resizeWidth; x++)
                    {
                        var firstColor = firstSample.GetPixel(x, y);
                        var secondColor = secondSample.GetPixel(x, y);

                        totalDifference += Math.Abs(firstColor.R - secondColor.R);
                        totalDifference += Math.Abs(firstColor.G - secondColor.G);
                        totalDifference += Math.Abs(firstColor.B - secondColor.B);
                    }
                }

                if (maxDifference == 0)
                {
                    return true;
                }

                var normalizedDifference = (double)totalDifference / maxDifference;
                return normalizedDifference < 0.01;
            }
            catch
            {
                return false;
            }
        }

        private void ChooseVideoFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "选择要合成视频的图片目录",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                VideoFolderText.Text = dialog.SelectedPath;
            }
        }

        private void OpenVideoFolderButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFolder(VideoFolderText.Text, "图片文件夹为空，无法打开。");
        }

        private void ChooseFfmpegFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "ffmpeg 可执行文件|ffmpeg.exe|所有文件|*.*",
                Title = "选择 ffmpeg.exe",
                CheckFileExists = true,
                FileName = "ffmpeg.exe"
            };

            if (dialog.ShowDialog() == true)
            {
                VideoFfmpegPathText.Text = dialog.FileName;
            }
        }

        private void ChooseOutputFileButton_Click(object sender, RoutedEventArgs e)
        {
            var defaultFileName = string.IsNullOrWhiteSpace(VideoOutputText.Text)
                ? "timelapse.mp4"
                : VideoOutputText.Text.Trim();

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "MP4 视频|*.mp4|AVI 视频|*.avi|MKV 视频|*.mkv",
                DefaultExt = ".mp4",
                FileName = defaultFileName
            };

            if (dialog.ShowDialog() == true)
            {
                VideoOutputText.Text = Path.GetFileName(dialog.FileName);
                VideoOutputDirectoryText.Text = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
                VideoLogText.AppendText($"已设置输出文件：{dialog.FileName}\n");
            }
        }

        private void ChooseOutputDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "选择输出视频目录",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                VideoOutputDirectoryText.Text = dialog.SelectedPath;
                if (string.IsNullOrWhiteSpace(VideoOutputText.Text))
                {
                    VideoOutputText.Text = "timelapse.mp4";
                }
                VideoLogText.AppendText($"已选择输出目录：{dialog.SelectedPath}\n");
            }
        }

        private void BuildVideoButton_Click(object sender, RoutedEventArgs e)
        {
            VideoLogText.AppendText("开始导出视频...\n");

            var folder = VideoFolderText.Text.Trim();
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                VideoLogText.AppendText("错误：请选择存在的图片文件夹。\n");
                return;
            }

            if (!double.TryParse(VideoFpsText.Text, out var fps) || fps <= 0)
            {
                VideoLogText.AppendText("错误：视频帧率必须大于 0。\n");
                return;
            }

            var files = GetSupportedImageFiles(folder).OrderBy(file => file, StringComparer.OrdinalIgnoreCase).ToList();
            if (files.Count == 0)
            {
                VideoLogText.AppendText("错误：指定目录中没有可合成的视频图片。\n");
                return;
            }

            var outputDirectory = VideoOutputDirectoryText.Text.Trim();
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                outputDirectory = folder;
                VideoLogText.AppendText("未选择输出目录，默认使用图片目录：" + outputDirectory + "\n");
            }

            var outputFileName = VideoOutputText.Text.Trim();
            if (string.IsNullOrWhiteSpace(outputFileName))
            {
                outputFileName = "timelapse.mp4";
                VideoLogText.AppendText("未填写输出文件名，默认使用：timelapse.mp4\n");
            }

            var outputPath = Path.Combine(outputDirectory, outputFileName);
            if (!Path.HasExtension(outputPath))
            {
                outputPath += ".mp4";
            }

            Directory.CreateDirectory(outputDirectory);

            var ffmpegPath = GetFfmpegExecutablePath();
            if (string.IsNullOrWhiteSpace(ffmpegPath))
            {
                VideoLogText.AppendText("错误：未找到 ffmpeg，可在系统 PATH 中或手动选择 ffmpeg.exe。\n");
                return;
            }

            var sequenceFolder = CreateImageSequence(files);
            var arguments = $"-y -framerate {fps:0.##} -i \"{Path.Combine(sequenceFolder, "%04d.png")}\" -c:v libx264 -pix_fmt yuv420p \"{outputPath}\"";

            try
            {
                VideoLogText.AppendText($"输出路径：{outputPath}\n");
                VideoLogText.AppendText($"ffmpeg 路径：{ffmpegPath}\n");
                VideoLogText.AppendText($"图片序列目录：{sequenceFolder}\n");
                VideoLogText.AppendText("说明：已按顺序生成图片序列，确保所有帧都纳入视频而不是只保留最后一张。\n");

                var processInfo = new ProcessStartInfo(ffmpegPath)
                {
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    VideoLogText.AppendText("错误：启动 ffmpeg 失败。\n");
                    return;
                }

                var standardError = process.StandardError.ReadToEnd();
                var standardOutput = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    VideoLogText.AppendText($"导出成功：{outputPath}\n");
                    VideoLogText.AppendText($"已处理 {files.Count} 张图片。\n");
                }
                else
                {
                    VideoLogText.AppendText("错误：视频生成失败。\n");
                    VideoLogText.AppendText($"ffmpeg stderr：{standardError}\n");
                    VideoLogText.AppendText($"ffmpeg stdout：{standardOutput}\n");
                }
            }
            catch (Exception ex)
            {
                VideoLogText.AppendText($"错误：视频生成过程中出现异常：{ex.Message}\n");
            }
        }

        private static string CreateImageSequence(IList<string> files)
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "TimeLapseScreenRecorder", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            for (var i = 0; i < files.Count; i++)
            {
                var source = files[i];
                var target = Path.Combine(tempDirectory, $"{(i + 1):D4}.png");
                File.Copy(source, target, overwrite: true);
            }

            return tempDirectory;
        }

        private string GetFfmpegExecutablePath()
        {
            var manualPath = VideoFfmpegPathText.Text.Trim();
            if (!string.IsNullOrWhiteSpace(manualPath) && File.Exists(manualPath))
            {
                return manualPath;
            }

            return FindFfmpegExecutable() ?? string.Empty;
        }

        private static string? FindFfmpegExecutable()
        {
            var candidates = new List<string>
            {
                "ffmpeg",
                "ffmpeg.exe",
                "C:\\ffmpeg\\bin\\ffmpeg.exe",
                "C:\\Program Files\\ffmpeg\\bin\\ffmpeg.exe",
                "C:\\Program Files (x86)\\ffmpeg\\bin\\ffmpeg.exe"
            };

            foreach (var path in Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? Array.Empty<string>())
            {
                candidates.Add(Path.Combine(path, "ffmpeg.exe"));
            }

            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                if (candidate.Contains("\\") && File.Exists(candidate))
                {
                    return candidate;
                }

                try
                {
                    var process = Process.Start(new ProcessStartInfo(candidate)
                    {
                        UseShellExecute = true,
                        CreateNoWindow = true
                    });
                    process?.Close();
                    return candidate;
                }
                catch
                {
                    // ignored
                }
            }

            return null;
        }

        private sealed class ScreenInfo
        {
            public ScreenInfo(string name, Rectangle bounds)
            {
                Name = name;
                Bounds = bounds;
            }

            public string Name { get; }
            public Rectangle Bounds { get; }
        }

        private sealed class AppConfig
        {
            public string? CaptureFolder { get; set; }
            public string? CaptureIntervalSeconds { get; set; }
            public string? CaptureFormat { get; set; }
            public string? CaptureQuality { get; set; }
            public bool SkipUnchangedCapture { get; set; } = true;
            public int SelectedScreenIndex { get; set; }
            public string? DedupeFolder { get; set; }
            public string? VideoFolder { get; set; }
            public string? VideoFps { get; set; }
            public string? VideoFfmpegPath { get; set; }
            public string? VideoOutputDirectory { get; set; }
            public string? VideoOutputFileName { get; set; }
        }
    }
}
