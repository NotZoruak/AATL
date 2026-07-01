namespace MFAAvalonia.Helper.ValueType;

/// <summary>
/// 用于存储自定义类实例的包装类
/// </summary>
/// <typeparam name="T">实例类型</typeparam>
public class CustomValue<T>
{
    /// <summary>
    /// 自定义类的名称（通常是文件名，不含扩展名）
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 自定义类的实例
    /// </summary>
    public T Value { get; }

    /// <summary>
    /// 创建一个新的 CustomValue 实例
    /// </summary>
    /// <param name="name">自定义类的名称</param>
    /// <param name="value">自定义类的实例</param>
    public CustomValue(string name, T value)
    {
        Name = name;
        Value = value;
    }
}