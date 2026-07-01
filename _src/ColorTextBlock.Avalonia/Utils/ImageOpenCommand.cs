using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ColorTextBlock.Avalonia.Utils;

public class ImageOpenCommand : ICommand
{
    public event EventHandler? CanExecuteChanged;

    // 存储生成的临时文件路径，用于后续清理
    private readonly List<string> _tempFiles = [];

    // 新的临时文件夹路径：AppContext.BaseDirectory/temp
    private readonly string _appTempDir;

    public ImageOpenCommand()
    {
        // 初始化应用内临时目录
        _appTempDir = Path.Combine(AppContext.BaseDirectory, "temp");
        // 确保临时目录存在（不存在则创建）
        EnsureTempDirectoryExists();
    }

    // 确保应用内临时目录存在，同时设置全平台权限
    private void EnsureTempDirectoryExists()
    {
        // if (!Directory.Exists(_appTempDir))
        // {
        //     Directory.CreateDirectory(_appTempDir);
        //     // 非Windows系统设置目录权限（确保读写）
        //     if (!OperatingSystem.IsWindows())
        //     {
        //         File.SetUnixFileMode(_appTempDir,
        //             UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
        //     }
        // }
    }

    public bool CanExecute(object? parameter)
    {
        return parameter is string or CImage;
    }

    public void Execute(object? parameter)
    {
        if (parameter is CImage cImage)
        {
            OpenImageFromCImage(cImage);
        }
        // else if (parameter is string source)
        // {
        //     _ = OpenImage(source);
        // }
    }

    async private Task OpenImage(string source)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
        {
            switch (uri.Scheme)
            {
                case "http":
                case "https":
                    var tempFile = await DownloadToTempFile(uri);
                    if (tempFile != null)
                        await OpenLocalFileAndCleanup(tempFile);
                    break;
                case "file":
                    OpenLocalFile(uri.LocalPath);
                    break;
                case "avares":
                    var resFile = await ExtractResourceToTempFile(uri);
                    if (resFile != null)
                        await OpenLocalFileAndCleanup(resFile);
                    break;
            }
        }
        else
        {
            if (File.Exists(source))
                OpenLocalFile(source);
        }
    }

    private void OpenImageFromCImage(CImage cImage)
    {
        if (cImage.Image == null)
        {
            Debug.WriteLine("CImage 中未加载图片");
            return;
        }
        var image = cImage.Image;
        var pixelSize = new PixelSize((int)image.Size.Width, (int)image.Size.Height);
        var renderTarget = new RenderTargetBitmap(pixelSize, new Vector(96, 96));

        using (var context = renderTarget.CreateDrawingContext())
        {
            context.DrawImage(image, new Rect(image.Size), new Rect(0, 0, pixelSize.Width, pixelSize.Height));
        }

        var view = new SukiImageBrowser();
        view.SetImage(renderTarget);
        view.Show();
    }

    // 保存图片到应用内临时目录（修改后）
    async private Task<string?> SaveImageToTempFile(IImage image)
    {
        try
        {
            // 确保临时目录存在（双重保险）
            EnsureTempDirectoryExists();

            // 生成唯一临时文件名（Guid+PNG扩展名）
            var fileName = $"{Guid.NewGuid()}.png";
            var tempPath = Path.Combine(_appTempDir, fileName);

            if (image is Bitmap bitmap)
            {
                using (var stream = File.Create(tempPath))
                {
                    bitmap.Save(stream);
                    stream.Flush();
                    stream.Position = 0;
                }
            }
            else
            {
                var pixelSize = new PixelSize((int)image.Size.Width, (int)image.Size.Height);
                var renderTarget = new RenderTargetBitmap(pixelSize, new Vector(96, 96));

                using (var context = renderTarget.CreateDrawingContext())
                {
                    context.DrawImage(image, new Rect(image.Size), new Rect(0, 0, pixelSize.Width, pixelSize.Height));
                }

                await using var stream = File.Create(tempPath);
                renderTarget.Save(stream);
                stream.Flush();
            }

            // 非Windows系统设置文件权限（可读）
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);
            }

            lock (_tempFiles)
            {
                _tempFiles.Add(tempPath);
            }
            return tempPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"保存图片到临时文件失败：{ex.Message}");
            return null;
        }
    }

    async private Task OpenLocalFileAndCleanup(string path)
    {
        Process? process = null;
        try
        {
            // 非Windows系统增加延迟，确保文件写入完成
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                await Task.Delay(200);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                process = Process.Start(new ProcessStartInfo(path)
                {
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                process = Process.Start(new ProcessStartInfo("xdg-open")
                {
                    Arguments = $"\"{path}\"",
                    UseShellExecute = false
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                process = Process.Start(new ProcessStartInfo("open")
                {
                    Arguments = $"\"{path}\"",
                    UseShellExecute = false
                });
            }

            if (process != null)
            {
                await process.WaitForExitAsync();
                await Task.Delay(100);
                DeleteTempFile(path);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"打开/清理临时文件失败：{ex.Message}");
            DeleteTempFile(path);
        }
    }

    private void OpenLocalFile(string path)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(path)
                {
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start(new ProcessStartInfo("xdg-open")
                {
                    Arguments = $"\"{path}\"",
                    UseShellExecute = false
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start(new ProcessStartInfo("open")
                {
                    Arguments = $"\"{path}\"",
                    UseShellExecute = false
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"打开本地文件失败：{ex.Message}");
        }
    }

    // 下载网络图片到应用内临时目录（修改后）
    async private Task<string?> DownloadToTempFile(Uri uri)
    {
        try
        {
            EnsureTempDirectoryExists();

            using var httpClient = new HttpClient();
            var bytes = await httpClient.GetByteArrayAsync(uri);
            var ext = Path.GetExtension(uri.LocalPath) ?? ".png";
            var fileName = $"{Guid.NewGuid()}{ext}";
            var tempPath = Path.Combine(_appTempDir, fileName);

            await File.WriteAllBytesAsync(tempPath, bytes);

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);
            }

            lock (_tempFiles)
            {
                _tempFiles.Add(tempPath);
            }
            return tempPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"下载网络图片失败：{ex.Message}");
            return null;
        }
    }

    // 提取资源图片到应用内临时目录（修改后）
    async private Task<string?> ExtractResourceToTempFile(Uri uri)
    {
        try
        {
            EnsureTempDirectoryExists();

            await using var stream = AssetLoader.Open(uri);
            var fileName = $"{Guid.NewGuid()}.png";
            var tempPath = Path.Combine(_appTempDir, fileName);

            await using var fileStream = File.Create(tempPath);
            await stream.CopyToAsync(fileStream);

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);
            }

            lock (_tempFiles)
            {
                _tempFiles.Add(tempPath);
            }
            return tempPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"提取资源图片失败：{ex.Message}");
            return null;
        }
    }

    private void DeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                Debug.WriteLine($"临时文件已删除：{path}");
            }
            lock (_tempFiles)
            {
                _tempFiles.Remove(path);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"删除临时文件失败：{ex.Message}");
        }
    }

    // 析构函数：清理剩余临时文件（适配新目录）
    ~ImageOpenCommand()
    {
        lock (_tempFiles)
        {
            foreach (var path in _tempFiles)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        Debug.WriteLine($"析构函数清理临时文件：{path}");
                    }
                }
                catch
                {
                    // 忽略删除失败
                }
            }
            _tempFiles.Clear();
        }

        // 尝试删除空的临时目录（可选）
        try
        {
            if (Directory.Exists(_appTempDir) && Directory.GetFiles(_appTempDir).Length == 0)
            {
                Directory.Delete(_appTempDir);
                Debug.WriteLine($"析构函数清理临时目录：{_appTempDir}");
            }
        }
        catch
        {
            // 忽略目录删除失败（如被占用）
        }
    }
}

// 【独立的临时文件夹清理方法】- 可提取至任意位置使用
public static class TempCleanupHelper
{
    /// <summary>
    /// 清理应用内临时文件夹（AppContext.BaseDirectory/temp）
    /// 适配全平台，跳过被占用的文件，无异常抛出
    /// </summary>
    /// <returns>清理结果：成功/失败</returns>
}
