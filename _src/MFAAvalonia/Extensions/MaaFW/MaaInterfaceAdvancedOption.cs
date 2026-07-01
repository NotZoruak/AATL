using MFAAvalonia.Helper;
using MFAAvalonia.Helper.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MFAAvalonia.Extensions.MaaFW;

/// <summary>
/// Advanced 高级配置项定义（支持通过 UI 输入框让用户自行编辑 pipeline_override）
/// </summary>
public class MaaInterfaceAdvancedOption
{
    /// <summary>配置项唯一名称标识符</summary>
    [JsonIgnore]
    public string? Name { get; set; }

    /// <summary>字段名列表（支持单个字符串或数组）</summary>
    [JsonConverter(typeof(GenericSingleOrListConverter<string>))]
    [JsonProperty("field")]
    public List<string>? Field;

    /// <summary>字段类型列表: "string", "int", "float", "bool"</summary>
    [JsonConverter(typeof(GenericSingleOrListConverter<string>))]
    [JsonProperty("type")]
    public List<string>? Type;

    /// <summary>默认值列表</summary>
    [JsonConverter(typeof(GenericSingleOrListConverter<JToken>))]
    [JsonProperty("default")]
    public List<JToken>? Default;

    /// <summary>管道覆盖配置（支持 {field} 变量替换）</summary>
    [JsonProperty("pipeline_override")]
    public Dictionary<string, Dictionary<string, JToken>>? PipelineOverride;

    /// <summary>显示标签，支持国际化（以$开头）</summary>
    [JsonProperty("label")]
    public string? Label { get; set; }

    /// <summary>详细描述，支持 Markdown</summary>
    [JsonProperty("description")]
    public string? Description { get; set; }

    /// <summary>图标文件路径</summary>
    [JsonProperty("icon")]
    public string? Icon { get; set; }

    /// <summary>文档说明（旧版兼容）</summary>
    [JsonProperty("doc")]
    [JsonConverter(typeof(GenericSingleOrListConverter<string>))]
    public List<string>? Document { get; set; }

    /// <summary>正则表达式验证规则（按字段名索引）</summary>
    [JsonProperty("verify")]
    [JsonConverter(typeof(GenericSingleOrListConverter<string>))]
    public List<string>? Verify { get; set; }

    /// <summary>验证失败提示信息（按字段名索引）</summary>
    [JsonProperty("pattern_msg")]
    [JsonConverter(typeof(GenericSingleOrListConverter<string>))]
    public List<string>? PatternMsg { get; set; }

    /// <summary>获取显示名称（优先 Label，否则 Name）</summary>
    [JsonIgnore]
    public string DisplayName => Label ?? Name ?? string.Empty;
    private Dictionary<string, Type> GetTypeMap()
    {
        var typeMap = new Dictionary<string, Type>();
        if (Field == null || Type == null) return typeMap;
        for (int i = 0; i < Field.Count; i++)
        {
            var type = Type.Count > 0 ? (i >= Type.Count ? Type[0] : Type[i]) : "string";
            var typeName = type.ToLower();
            typeMap[Field[i]] = typeName switch
            {
                "int" => typeof(long),
                "float" => typeof(float),
                "bool" => typeof(bool),
                _ => typeof(string)
            };
        }
        return typeMap;
    }
// 内置的占位符替换方法
    public string GenerateProcessedPipeline(Dictionary<string, string> inputValues)
    {
        if (PipelineOverride == null) return "{}";
// 深拷贝原始数据
        var cloned = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, JToken>>>(
            JsonConvert.SerializeObject(PipelineOverride)
        );
        var typeMap = GetTypeMap();
        var regex = new Regex(@"{([^{}]+)}", RegexOptions.Compiled);
        foreach (var preset in cloned.Values)
        {
            foreach (var key in preset.Keys.ToList())
            {
                var jToken = preset[key];
                var newToken = ProcessToken(jToken, regex, inputValues, typeMap);
                if (newToken != null)
                {
                    preset[key] = newToken;
                }
            }
        }
        var result = JsonConvert.SerializeObject(cloned, Formatting.Indented);
       // Console.WriteLine(result);
        return result;
    }
// 统一处理各种类型的 Token，返回处理后的新 Token
    private JToken? ProcessToken(JToken? token, Regex regex, Dictionary<string, string> inputValues, Dictionary<string, Type> typeMap)
    {
        if (token == null) return null;
        switch (token.Type)
        {
            case JTokenType.String:
                return ProcessStringToken(token, regex, inputValues, typeMap);
            case JTokenType.Array:
                return ProcessArrayToken(token, regex, inputValues, typeMap);
            case JTokenType.Object:
                return ProcessObjectToken(token, regex, inputValues, typeMap);
            default:
                return token; // 其他类型直接返回原值
        }
    }
// 处理字符串类型的 Token
    private JToken? ProcessStringToken(JToken token, Regex regex, Dictionary<string, string> inputValues, Dictionary<string, Type> typeMap)
    {
        var strVal = token.Value<string>();
        string currentPlaceholder = null;
        bool isExplicitNull = false;
        var newVal = regex.Replace(strVal, match =>
        {
            currentPlaceholder = match.Groups[1].Value;
            // 首先尝试从输入值获取
            if (inputValues.TryGetValue(currentPlaceholder, out var inputStr))
            {
                // 检查是否是显式 null 标记
                if (inputStr == MaaInterface.MaaInterfaceOption.ExplicitNullMarker)
                {
                    isExplicitNull = true;
                    return string.Empty; // 临时返回空字符串，后面会处理
                }
                return ApplyTypeConversion(inputStr, currentPlaceholder, typeMap);
            }
            // 输入值不存在，尝试从默认值获取
            return GetDefaultValue(currentPlaceholder, typeMap);
        });

        // 如果是显式 null，返回 JValue.CreateNull()
        if (isExplicitNull)
        {
            return JValue.CreateNull();
        }

        if (newVal != strVal && currentPlaceholder != null)
        {
            try
            {
                // 根据类型重建 JToken
                object convertedValue = newVal;
                if (typeMap.TryGetValue(currentPlaceholder, out var targetType) && targetType != typeof(string))
                {
                    convertedValue = Convert.ChangeType(newVal, targetType);
                }
                return JToken.FromObject(convertedValue);
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"类型转换失败，尝试使用默认值处理：{ex.Message}");

                // 异常处理逻辑修改：先尝试转换默认值
                if (typeMap.TryGetValue(currentPlaceholder, out var targetType))
                {
                    try
                    {
                        // 获取默认值并尝试转换
                        string defaultValue = GetDefaultValue(currentPlaceholder, typeMap);
                        var convertedValue = Convert.ChangeType(defaultValue, targetType);
                        return JToken.FromObject(convertedValue);
                    }
                    catch (Exception defaultEx)
                    {
                        LoggerHelper.Error($"默认值转换失败，使用类型默认值：{defaultEx.Message}");
                        // 返回目标类型的默认值
                        return JToken.FromObject(targetType.IsValueType ? Activator.CreateInstance(targetType) : null);
                    }
                }
            }
        }
        return token; // 未发生变化或处理失败时返回原值
    }
    
// 应用类型转换
    private string ApplyTypeConversion(string inputStr, string placeholder, Dictionary<string, Type> typeMap)
    {
        if (typeMap.TryGetValue(placeholder, out var targetType))
        {
            try
            {
                return Convert.ChangeType(inputStr, targetType).ToString();
            }
            catch
            {
// 类型转换失败，返回默认值
                return GetDefaultValue(placeholder, typeMap);
            }
        }
        return inputStr;
    }
// 获取默认值
    private string GetDefaultValue(string placeholder, Dictionary<string, Type> typeMap)
    {
        // 从 Default 列表获取默认值
        var fieldIndex = Field?.IndexOf(placeholder) ?? -1;
        
        if (fieldIndex >= 0 && Default != null && fieldIndex < Default.Count)
        {
            var defaultToken = Default[fieldIndex];
            
            // 处理单个默认值
            if (defaultToken.Type != JTokenType.Array)
            {
                string defaultValue = defaultToken.ToString();
                
                // 如果有类型映射，尝试将默认值转换为目标类型
                if (typeMap.TryGetValue(placeholder, out var targetType) && targetType != typeof(string))
                {
                    try
                    {
                        return Convert.ChangeType(defaultValue, targetType).ToString();
                    }
                    catch
                    {
                        // 转换失败，返回原始默认值
                        return defaultValue;
                    }
                }
                
                return defaultValue;
            }
            
            // 处理多个默认值 - 取第一个
            var defaultArray = defaultToken as JArray;
            if (defaultArray != null && defaultArray.Count > 0)
            {
                string defaultValue = defaultArray[0].ToString();
                
                // 如果有类型映射，尝试将默认值转换为目标类型
                if (typeMap.TryGetValue(placeholder, out var targetType) && targetType != typeof(string))
                {
                    try
                    {
                        return Convert.ChangeType(defaultValue, targetType).ToString();
                    }
                    catch
                    {
                        // 转换失败，返回原始默认值
                        return defaultValue;
                    }
                }
                
                return defaultValue;
            }
        }
        
        // 无默认值，保持占位符
        return $"{{{placeholder}}}";
    }
    
// 处理数组类型的 Token
    private JToken ProcessArrayToken(JToken token, Regex regex, Dictionary<string, string> inputValues, Dictionary<string, Type> typeMap)
    {
        var arr = (JArray)token;
        var newArr = new JArray();
        foreach (var item in arr)
        {
            var processedItem = ProcessToken(item, regex, inputValues, typeMap);
            if (processedItem != null)
            {
                newArr.Add(processedItem);
            }
        }
        return newArr;
    }
// 处理对象类型的 Token
    private JToken ProcessObjectToken(JToken token, Regex regex, Dictionary<string, string> inputValues, Dictionary<string, Type> typeMap)
    {
        var obj = (JObject)token;
        var newObj = new JObject();
        foreach (var property in obj.Properties())
        {
            var processedValue = ProcessToken(property.Value, regex, inputValues, typeMap);
            if (processedValue != null)
            {
                newObj[property.Name] = processedValue;
            }
        }
        return newObj;
    }

    /// <summary>
    /// 验证用户输入是否合法
    /// </summary>
    /// <param name="fieldName">字段名</param>
    /// <param name="value">用户输入值</param>
    /// <returns>验证结果和错误消息</returns>
    public (bool IsValid, string? ErrorMessage) ValidateInput(string fieldName, string value)
    {
        if (Field == null) return (true, null);

        var fieldIndex = Field.IndexOf(fieldName);
        if (fieldIndex < 0) return (true, null);

        // 获取验证规则
        string? verifyPattern = null;
        if (Verify != null && fieldIndex < Verify.Count)
        {
            verifyPattern = Verify[fieldIndex];
        }
        else if (Verify != null && Verify.Count > 0)
        {
            verifyPattern = Verify[0]; // 如果只有一个，应用于所有字段
        }

        if (string.IsNullOrEmpty(verifyPattern)) return (true, null);

        try
        {
            var regex = new Regex(verifyPattern);
            if (!regex.IsMatch(value))
            {
                // 获取错误消息
                string? errorMsg = null;
                if (PatternMsg != null && fieldIndex < PatternMsg.Count)
                {
                    errorMsg = PatternMsg[fieldIndex];
                }
                else if (PatternMsg != null && PatternMsg.Count > 0)
                {
                    errorMsg = PatternMsg[0];
                }

                return (false, errorMsg ?? $"字段 {fieldName} 输入格式不正确");
            }
        }
        catch (Exception ex)
        {
            LoggerHelper.Error($"正则表达式验证失败: {ex.Message}");
            return (true, null); // 正则出错时放行
        }

        return (true, null);
    }

    /// <summary>
    /// 获取字段的默认值
    /// </summary>
    public string? GetDefaultValue(string fieldName)
    {
        if (Field == null || Default == null) return null;

        var fieldIndex = Field.IndexOf(fieldName);
        if (fieldIndex < 0 || fieldIndex >= Default.Count) return null;

        var defaultToken = Default[fieldIndex];
        return defaultToken?.ToString();
    }

    /// <summary>
    /// 获取字段的类型
    /// </summary>
    public string GetFieldType(string fieldName)
    {
        if (Field == null || Type == null) return "string";

        var fieldIndex = Field.IndexOf(fieldName);
        if (fieldIndex < 0) return "string";

        if (fieldIndex < Type.Count)
        {
            return Type[fieldIndex]?.ToLower() ?? "string";
        }
        
        // 如果只有一个类型，应用于所有字段
        return Type.Count > 0 ? Type[0]?.ToLower() ?? "string" : "string";
    }
}
