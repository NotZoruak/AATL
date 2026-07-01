using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using MFAAvalonia.ViewModels.Pages;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace MFAAvalonia.Extensions;

public class ServiceProviderExtension : MarkupExtension
{
    public Type ServiceType { get; set; }


    // 关键修改：使用框架提供的 IServiceProvider，而非硬编码 App.Services
    public override object ProvideValue(IServiceProvider serviceProvider)
    {

        // 预览时的降级处理：返回模拟实例（避免报错）
        if (Design.IsDesignMode)
        {
            return CreateDesignTimeInstance(ServiceType);
        }

        return App.Services.GetRequiredService(ServiceType);
    }

    // 预览时创建模拟实例（避免预览报错，可根据需要自定义）
    private object CreateDesignTimeInstance(Type serviceType)
    {
        // 这里可以返回模拟数据的实例，比如用 Activator.CreateInstance（前提是服务有默认构造函数）
        try
        {
            return Activator.CreateInstance(serviceType)!;
        }
        catch
        {
            // 若没有默认构造函数，可手动创建模拟实例（示例）
            throw new InvalidOperationException($"未为 {serviceType.Name} 配置设计时实例");
        }
    }
}
