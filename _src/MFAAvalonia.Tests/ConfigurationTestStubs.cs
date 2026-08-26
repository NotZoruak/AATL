using MFAAvalonia.Helper.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;

namespace MFAAvalonia.Configuration
{
    /// <summary>测试使用的最小全局配置替身，提供 InstanceConfiguration 所需的读取接口。</summary>
    public static class ConfigurationManager
    {
        public static MFAConfiguration Current { get; } = new();
    }

    public sealed class MFAConfiguration
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

        public bool ContainsKey(string key) => _values.ContainsKey(key);

        public T GetValue<T>(string key, T fallback)
        {
            return TryConvertValue(key, out T? value) ? value! : fallback;
        }

        public T GetValue<T>(string key, T defaultValue, Dictionary<object, T>? options)
        {
            if (_values.TryGetValue(key, out var value))
            {
                if (options != null && options.TryGetValue(value!, out var mapped))
                    return mapped;

                if (TryConvertValue(key, out T? converted))
                    return converted!;
            }

            return defaultValue;
        }

        public T GetValue<T>(string key, T defaultValue, T? noValue = default, params JsonConverter[] valueConverters)
        {
            if (!TryDeserializeValue(key, valueConverters, out T? value))
                return defaultValue;

            if (value == null || EqualityComparer<T>.Default.Equals(value, noValue!))
                return defaultValue;

            return value;
        }

        public T GetValue<T>(string key, T defaultValue, List<T>? noValue = null, params JsonConverter[] valueConverters)
        {
            if (!TryDeserializeValue(key, valueConverters, out T? value))
                return defaultValue;

            if (value == null)
                return defaultValue;

            if (noValue != null && noValue.Contains(value))
                return defaultValue;

            return value;
        }

        public void SetValue(string key, object? value)
        {
            if (value == null)
                return;

            _values[key] = value;
        }

        public bool TryGetValue<T>(string key, out T output, params JsonConverter[] valueConverters)
        {
            if (TryDeserializeValue(key, valueConverters, out T? value) && value != null)
            {
                output = value;
                return true;
            }

            output = default!;
            return false;
        }

        public void Reset() => _values.Clear();

        private bool TryConvertValue<T>(string key, out T? value)
        {
            if (!_values.TryGetValue(key, out var rawValue) || rawValue == null)
            {
                value = default;
                return false;
            }

            if (rawValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            if (rawValue is JToken token)
            {
                value = token.ToObject<T>();
                return value != null;
            }

            value = default;
            return false;
        }

        private bool TryDeserializeValue<T>(string key, JsonConverter[] valueConverters, out T? value)
        {
            if (!_values.TryGetValue(key, out var rawValue) || rawValue == null)
            {
                value = default;
                return false;
            }

            if (rawValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            var settings = new JsonSerializerSettings();
            foreach (var converter in valueConverters)
                settings.Converters.Add(converter);

            value = JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(rawValue), settings);
            return value != null;
        }
    }
}

namespace MFAAvalonia.ViewModels.Pages
{
    /// <summary>刀帐自动识别写入配置时使用的最小状态结构。</summary>
    public sealed record SwordBookPortraitState(
        string Number,
        bool Owned,
        bool Wounded,
        bool TrueSword,
        bool InnerCare,
        bool Casual);
}

namespace MFAAvalonia.Helper
{
    /// <summary>测试中为真实 InstanceConfiguration 提供可控的实例配置目录。</summary>
    public static class AppPaths
    {
        public static string InstancesDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "matr-tests", "instances");
    }

    /// <summary>测试环境中的最小日志替身。</summary>
    public static class LoggerHelper
    {
        public static void Error(string _, Exception? __ = null)
        {
        }
    }

    /// <summary>测试环境中的最小 JSON 读写帮助器。</summary>
    public static class JsonHelper
    {
        public static T LoadJson<T>(string path, T defaultValue)
        {
            if (!File.Exists(path))
                return defaultValue;

            var content = File.ReadAllText(path);
            using var stringReader = new StringReader(content);
            using var jsonReader = new JsonTextReader(stringReader)
            {
                DateParseHandling = DateParseHandling.None,
            };
            var token = JToken.ReadFrom(jsonReader);
            if (token == null)
                return defaultValue;

            var normalized = NormalizeToken(token);
            if (normalized is T typedValue)
                return typedValue;

            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(normalized)) ?? defaultValue;
        }

        public static void SaveJson(string path, object data, params JsonConverter[] converters)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
            };

            foreach (var converter in converters)
                settings.Converters.Add(converter);

            File.WriteAllText(path, JsonConvert.SerializeObject(data, settings));
        }

        private static object? NormalizeToken(JToken token) => token switch
        {
            JObject obj => obj.Properties()
                .ToDictionary(property => property.Name, property => NormalizeToken(property.Value), StringComparer.Ordinal),
            JArray array => array.Select(NormalizeToken).ToList(),
            JValue value => value.Value,
            _ => null,
        };
    }
}

namespace MFAAvalonia.Helper.Converters
{
    /// <summary>测试环境中的空转换器，占位以满足真实 InstanceConfiguration 的编译依赖。</summary>
    public sealed class MaaInterfaceSelectAdvancedConverter : JsonConverter
    {
        public MaaInterfaceSelectAdvancedConverter(bool useCache)
        {
            _ = useCache;
        }

        public override bool CanConvert(Type objectType) => false;

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) =>
            throw new NotSupportedException();

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) =>
            throw new NotSupportedException();
    }

    /// <summary>测试环境中的空转换器，占位以满足真实 InstanceConfiguration 的编译依赖。</summary>
    public sealed class MaaInterfaceSelectOptionConverter : JsonConverter
    {
        public MaaInterfaceSelectOptionConverter(bool useCache)
        {
            _ = useCache;
        }

        public override bool CanConvert(Type objectType) => false;

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) =>
            throw new NotSupportedException();

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) =>
            throw new NotSupportedException();
    }
}
