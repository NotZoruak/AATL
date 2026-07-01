using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace MFAAvalonia.Helper.Converters;

/// <summary>
/// 支持单个对象或对象数组的泛型转换器
/// （兼容 Newtonsoft.Json 的高级动态处理特性）[1](@ref)
/// </summary>
/// <typeparam name="T">目标类型</typeparam>
[JsonObject(ItemRequired = Required.AllowNull)]
public class GenericSingleOrListConverter<T> : JsonConverter
{
    public override bool CanConvert(Type objectType) =>
        objectType == typeof(T) || objectType == typeof(IEnumerable<T>);

    public override object? ReadJson(JsonReader reader,
        Type objectType,
        object existingValue,
        JsonSerializer serializer)
    {
        var token = JToken.Load(reader);

        return token switch
        {
            JObject obj => [obj.ToObject<T>(serializer)], // 对象转换为单元素列表
            JArray arr => arr.ToObject<List<T>>(serializer), // 数组直接转换为列表
            JValue { Type: JTokenType.Null } => null, // 空值处理
            _ => HandlePrimitive(token, serializer)
        };
    }

    private List<T> HandlePrimitive(JToken token, JsonSerializer serializer)
    {
        if (typeof(T) == typeof(JToken))
        {
            return [(T)(object)token ];
        }

        // 特殊处理字符串类型
        if (typeof(T) == typeof(string))
        {
            return [  (T)(object)token.ToString()];
        }

        if (typeof(T).IsPrimitive || IsSupportedValueType())
        {
            // 修复：先获取JToken的值，再转换为目标类型
            object value = token.Type switch
            {
                JTokenType.Integer => token.Value<long>(),
                JTokenType.Float => token.Value<double>(),
                JTokenType.Boolean => token.Value<bool>(),
                JTokenType.String => token.Value<string>(),
                JTokenType.Null => null,
                _ => token.ToString()
            };

            if (value == null && typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null)
            {
                throw new JsonSerializationException($"无法将null转换为值类型 {typeof(T)}");
            }

            try
            {
                if (value == null)
                {
                    return [default];
                }

                // 尝试将值转换为目标类型
                if (value is IConvertible convertible)
                {
                    var converted = Convert.ChangeType(convertible, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
                    return [(T)converted];
                }

                // 如果无法转换，尝试使用JSON序列化器
                return [token.ToObject<T>(serializer)];
            }
            catch (Exception ex)
            {
                throw new JsonSerializationException($"无法将值 '{value}' 转换为类型 {typeof(T)}", ex);
            }
        }

        throw new JsonSerializationException($"类型 {typeof(T)} 不支持基础值转换");

        bool IsSupportedValueType() =>
            typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var list = value switch
        {
            IEnumerable<T> collection => collection,
            T single => new List<T>
            {
                single
            },
            _ => throw new JsonException("不支持的序列化类型")
        };

        serializer.Serialize(writer, list);
    }
}
