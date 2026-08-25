# TimeLapse Screen Recorder

这是一个 Windows 桌面软件，基于 WPF + .NET 6 + ffmpeg 构建，提供 3 个核心功能：

1. 定时截屏：选择保存目录、选择屏幕、设置截屏间隔，自动按时间戳保存图片。
2. 图片去重：扫描指定目录中的图片，按文件名顺序比较相邻图片，仅保留有效帧。
3. 生成视频：读取图片目录并使用 ffmpeg 按配置帧率合成 mp4/avi/mkv 视频。

## 运行方式

### 1）直接运行源码

在 Windows 环境中执行：

```powershell
dotnet build
```

然后启动：

```powershell
dotnet run --project TimeLapseScreenRecorder\TimeLapseScreenRecorder.csproj
```

### 2）编译成可直接执行的 exe

在项目根目录执行：

```powershell
dotnet publish .\TimeLapseScreenRecorder\TimeLapseScreenRecorder.csproj -c Release -r win-x64 --self-contained false -p:UseAppHost=true -p:PublishSingleFile=false
```

生成的可执行文件在：

```text
TimeLapseScreenRecorder\bin\Release\net6.0-windows\win-x64\publish\TimeLapseScreenRecorder.exe
```

如果想输出单文件 exe，可使用：

```powershell
dotnet publish .\TimeLapseScreenRecorder\TimeLapseScreenRecorder.csproj -c Release -r win-x64 --self-contained false -p:UseAppHost=true -p:PublishSingleFile=true
```

### 3）双击运行

发布完成后，直接双击下面这个程序即可启动：

```text
TimeLapseScreenRecorder\bin\Release\net6.0-windows\win-x64\publish\TimeLapseScreenRecorder.exe
```

## 依赖说明

- .NET 6 SDK（Windows 桌面 SDK）
- ffmpeg（可在 PATH 中自动搜索，也可在界面中手动选择 ffmpeg.exe）

## ffmpeg 配置

程序支持手动指定 ffmpeg 路径：

- 若“ffmpeg 路径”为空：程序会自动在当前环境变量 PATH 和常见目录中查找 ffmpeg
- 若填写了路径：程序优先使用你指定的 ffmpeg.exe

如果系统未安装 ffmpeg，可自行安装，然后在软件界面中手动选择：

```powershell
ffmpeg -version
```

## 图标说明

程序已经配置了默认图标：

```text
TimeLapseScreenRecorder\assets\app.ico
```

该图标会在编译和发布时被打包进 exe 中。

## 功能说明

- 捕获器默认输出 PNG 图片，文件名格式：yyyyMMdd_HHmmssfff.png
- 去重逻辑会按文件名排序，逐个比较相邻图片是否相似
- 视频生成使用 ffmpeg 的 glob 模式读取图片目录中的同扩展名图像
