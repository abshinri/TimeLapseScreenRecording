using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;

namespace TimeLapseScreenRecorder
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _captureTimer = new();
        private readonly List<ScreenInfo> _screens = new();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            _captureTimer.Tick += CaptureTimer_Tick;
            CaptureIntervalText.Text = "10";
            VideoFpsText.Text = "10";
            StopCaptureButton.IsEnabled = false;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            PopulateMonitorList();
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

                var fileName = $"{DateTime.Now:yyyyMMdd_HHmmssfff}.png";
                var filePath = Path.Combine(folder, fileName);
                bitmap.Save(filePath, ImageFormat.Png);

                CaptureLogText.AppendText($"已保存：{filePath}\n");
            }
            catch (Exception ex)
            {
                CaptureLogText.AppendText($"截屏失败：{ex.Message}\n");
            }
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
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "MP4 视频|*.mp4|AVI 视频|*.avi|MKV 视频|*.mkv",
                DefaultExt = ".mp4",
                FileName = "timelapse.mp4"
            };

            if (dialog.ShowDialog() == true)
            {
                VideoOutputText.Text = dialog.FileName;
            }
        }

        private void BuildVideoButton_Click(object sender, RoutedEventArgs e)
        {
            var folder = VideoFolderText.Text.Trim();
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                VideoLogText.AppendText("请先选择存在的图片文件夹。\n");
                return;
            }

            if (!double.TryParse(VideoFpsText.Text, out var fps) || fps <= 0)
            {
                VideoLogText.AppendText("视频帧率必须大于 0。\n");
                return;
            }

            var files = GetSupportedImageFiles(folder).OrderBy(file => file, StringComparer.OrdinalIgnoreCase).ToList();
            if (files.Count == 0)
            {
                VideoLogText.AppendText("指定目录中没有可合成的视频图片。\n");
                return;
            }

            var outputPath = VideoOutputText.Text.Trim();
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.Combine(folder, "timelapse.mp4");
            }

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var extension = Path.GetExtension(files[0]).ToLowerInvariant();
            var ffmpegPath = GetFfmpegExecutablePath();
            if (string.IsNullOrWhiteSpace(ffmpegPath))
            {
                VideoLogText.AppendText("未找到 ffmpeg，可在系统 PATH 中或手动选择 ffmpeg.exe。\n");
                return;
            }

            var pattern = Path.Combine(folder, $"*{extension}");
            var arguments = $"-y -framerate {fps:0.##} -pattern_type glob -i \"{pattern}\" -c:v libx264 -pix_fmt yuv420p \"{outputPath}\"";

            try
            {
                VideoLogText.AppendText($"正在生成视频：{outputPath}\n");
                VideoLogText.AppendText($"使用 ffmpeg：{ffmpegPath}\n");

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
                    VideoLogText.AppendText("启动 ffmpeg 失败。\n");
                    return;
                }

                var standardError = process.StandardError.ReadToEnd();
                var standardOutput = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    VideoLogText.AppendText($"视频已生成：{outputPath}\n");
                    VideoLogText.AppendText($"共处理 {files.Count} 张图片。\n");
                }
                else
                {
                    VideoLogText.AppendText($"视频生成失败：{standardError}\n{standardOutput}\n");
                }
            }
            catch (Exception ex)
            {
                VideoLogText.AppendText($"视频生成过程中出现异常：{ex.Message}\n");
            }
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
    }
}
