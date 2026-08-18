# TimeLapse Screen Recorder

这是一个 Windows 桌面软件，基于 WPF + .NET 6 + ffmpeg 构建，提供 3 个核心功能：

1. 定时截屏：选择保存目录、选择屏幕、设置截屏间隔，自动按时间戳保存图片。
2. 图片去重：扫描指定目录中的图片，按文件名顺序比较相邻图片，仅保留有效帧。
3. 生成视频：读取图片目录并使用 ffmpeg 按配置帧率合成 mp4/avi/mkv 视频。

## 运行方式

在 Windows 环境中执行：

```powershell
dotnet build
```

然后启动：

```powershell
dotnet run --project TimeLapseScreenRecorder\TimeLapseScreenRecorder.csproj
```

## 依赖说明

- .NET 6 SDK（Windows 桌面 SDK）
- ffmpeg（需要在 PATH 中可见，或者放置在常见安装路径中）

## 生成视频需要 ffmpeg

如果系统未安装 ffmpeg，需自行安装并确保可执行文件在 PATH 中：

```powershell
ffmpeg -version
```

## 功能说明

- 捕获器默认输出 PNG 图片，文件名格式：yyyyMMdd_HHmmssfff.png
- 去重逻辑会按文件名排序，逐个比较相邻图片是否相似
- 视频生成使用 ffmpeg 的 glob 模式读取图片目录中的同扩展名图像
