using MFAAvalonia.Helper;
using MFAAvalonia.Helper.ValueType;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;

namespace MFAAvalonia.Extensions.MaaFW;

/// <summary>
/// 自定义类加载器，用于动态编译和加载 C# 代码文件
/// </summary>
public static class CustomClassLoader
{
    private static List<MetadataReference>? _metadataReferences;
    private static bool _shouldLoadCustomClasses = true;
    private static FileSystemWatcher? _watcher;
    private static IEnumerable<CustomValue<object>>? _customClasses;

    /// <summary>
    /// 获取当前应用程序域中所有程序集的元数据引用
    /// </summary>
    private static List<MetadataReference> GetMetadataReferences()
    {
        if (_metadataReferences == null)
        {
            var domainAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            _metadataReferences = new List<MetadataReference>();

            foreach (var assembly in domainAssemblies)
            {
                if (!assembly.IsDynamic)
                {
                    try
                    {
                        unsafe
                        {
                            if (assembly.TryGetRawMetadata(out byte* blob, out int length))
                            {
                                var moduleMetadata = ModuleMetadata.CreateFromMetadata((IntPtr)blob, length);
                                var assemblyMetadata = AssemblyMetadata.Create(moduleMetadata);
                                var metadataReference = assemblyMetadata.GetReference();
                                _metadataReferences.Add(metadataReference);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggerHelper.Warning($"获取程序集元数据失败：程序集={assembly.FullName}，原因={ex.Message}");
                    }
                }
            }

            // 添加 System.Linq.Expressions 程序集引用
            try
            {
                unsafe
                {
                    if (typeof(System.Linq.Expressions.Expression).Assembly.TryGetRawMetadata(out byte* blob, out int length))
                    {
                        _metadataReferences.Add(AssemblyMetadata.Create(ModuleMetadata.CreateFromMetadata((IntPtr)blob, length)).GetReference());
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerHelper.Warning($"添加 System.Linq.Expressions 元数据引用失败：原因={ex.Message}");
            }
        }
        return _metadataReferences;
    }

    /// <summary>
    /// 加载并实例化指定目录中实现了指定接口的自定义类
    /// </summary>
    /// <param name="directory">包含 .cs 文件的目录路径</param>
    /// <param name="interfacesToImplement">要实现的接口名称数组</param>
    /// <returns>自定义类实例的集合</returns>
    private static IEnumerable<CustomValue<object>> LoadAndInstantiateCustomClasses(string directory, string[] interfacesToImplement)
    {
        var customClasses = new List<CustomValue<object>>();

        if (!Directory.Exists(directory))
        {
            LoggerHelper.Info($"自定义类目录不存在：目录={directory}");
            return customClasses;
        }

        // 设置文件监视器
        if (_watcher == null)
        {
            try
            {
                _watcher = new FileSystemWatcher(directory)
                {
                    Filter = "*.cs",
                    EnableRaisingEvents = true
                };
                _watcher.Changed += OnFileChanged;
                _watcher.Created += OnFileChanged;
                _watcher.Deleted += OnFileChanged;
                _watcher.Renamed += OnFileChanged;
                LoggerHelper.Info($"已启动自定义类目录监听：目录={directory}");
            }
            catch (Exception ex)
            {
                LoggerHelper.Warning($"创建自定义类目录监听失败：目录={directory}，原因={ex.Message}");
            }
        }

        var csFiles = Directory.GetFiles(directory, "*.cs");
        if (csFiles.Length == 0)
        {
            LoggerHelper.Info($"自定义类目录中未找到 .cs 文件：目录={directory}");
            return customClasses;
        }

        var references = GetMetadataReferences();

        foreach (var filePath in csFiles)
        {
            try
            {
                var name = Path.GetFileNameWithoutExtension(filePath);
                LoggerHelper.Info($"开始解析自定义类：名称={name}，文件={filePath}");

                var code = File.ReadAllText(filePath);
                var codeLines = code.Split(new[]
                {
                    '\n'
                }, StringSplitOptions.RemoveEmptyEntries).ToList();

                var syntaxTree = CSharpSyntaxTree.ParseText(code);
                var compilation = CSharpCompilation.Create($"DynamicAssembly_{name}_{Guid.NewGuid():N}")
                    .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithAllowUnsafe(true)
                        .WithOptimizationLevel(OptimizationLevel.Release))
                    .AddSyntaxTrees(syntaxTree)
                    .AddReferences(references);

                using var ms = new MemoryStream();
                var result = compilation.Emit(ms);

                if (!result.Success)
                {
                    var failures = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
                    foreach (var diagnostic in failures)
                    {
                        var lineInfo = diagnostic.Location.GetLineSpan().StartLinePosition;
                        var lineNumber = lineInfo.Line + 1;
                        var errorLine = lineNumber <= codeLines.Count
                            ? codeLines[lineNumber - 1].Trim()
                            : "无法获取对应代码行（行号超出范围）";
                        LoggerHelper.Error($"{diagnostic.Id}: {diagnostic.GetMessage()}  [错误行号: {lineNumber}]  [错误代码行: {errorLine}]");
                    }
                    continue;
                }

                ms.Seek(0, SeekOrigin.Begin);
                var assembly = Assembly.Load(ms.ToArray());

                var instances =
                    from type in assembly.GetTypes()
                    from iface in interfacesToImplement
                    where type.GetInterfaces().Any(i => i.Name == iface)
                    let instance = CreateInstance(type)
                    where instance != null
                    select new CustomValue<object>(name, instance);

                customClasses.AddRange(instances);
                LoggerHelper.Info($"已成功加载自定义类：名称={name}，文件={filePath}");
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"加载自定义类失败：文件={filePath}，原因={ex.Message}", ex);
            }
        }

        _shouldLoadCustomClasses = false;
        return customClasses;
    }

    /// <summary>
    /// 安全地创建类型实例
    /// </summary>
    private static object? CreateInstance(Type type)
    {
        try
        {
            return Activator.CreateInstance(type);
        }
        catch (Exception ex)
        {
            LoggerHelper.Warning($"创建自定义类实例失败：类型={type.FullName}，原因={ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 文件变化事件处理
    /// </summary>
    private static void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        LoggerHelper.Info($"自定义类文件发生变化：文件={e.FullPath}，变更类型={e.ChangeType}");
        _shouldLoadCustomClasses = true;
        _customClasses = null;
    }

    /// <summary>
    /// 获取自定义类实例（带缓存）
    /// </summary>
    /// <param name="directory">包含 .cs 文件的目录路径</param>
    /// <param name="interfacesToImplement">要实现的接口名称数组</param>
    /// <returns>自定义类实例的集合</returns>
    public static IEnumerable<CustomValue<object>> GetCustomClasses(string directory, string[] interfacesToImplement)
    {
        if (_customClasses == null || _shouldLoadCustomClasses)
        {
            _customClasses = LoadAndInstantiateCustomClasses(directory, interfacesToImplement);
        }
        else
        {
            foreach (var value in _customClasses)
            {
                LoggerHelper.Info($"使用缓存中的自定义类：名称={value.Name}");
            }
        }
        return _customClasses;
    }

    /// <summary>
    /// 强制重新加载自定义类
    /// </summary>
    public static void ForceReload()
    {
        _shouldLoadCustomClasses = true;
        _customClasses = null;
        LoggerHelper.Info("已标记自定义类缓存失效，将在下次访问时重新加载。");
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    public static void Dispose()
    {
        try
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnFileChanged;
                _watcher.Created -= OnFileChanged;
                _watcher.Deleted -= OnFileChanged;
                _watcher.Renamed -= OnFileChanged;
                _watcher.Dispose();
                _watcher = null;
                LoggerHelper.Info("已释放自定义类目录监听器。");
            }
            _customClasses = null;
            _metadataReferences = null;
        }
        catch (Exception ex)
        {
            //忽略清理过程中的异常，这可能是由于 Microsoft.CodeAnalysis 程序集未加载导致的
            LoggerHelper.Warning($"释放自定义类加载器资源时出现异常：原因={ex.Message}");
        }
    }
}
