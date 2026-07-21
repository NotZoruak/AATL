using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace MFAAvalonia.Extensions.MaaFW;

public class MaaToken
{
    private List<Dictionary<string, JToken>> Tokens = [];

    /// <summary>
    /// 将一组节点覆盖合并到管线参数中。
    /// 同一节点的后续覆盖会深度合并到现有条目中（后续值覆盖先前值），
    /// 而不是追加为新数组元素，避免跨层同名键的覆盖顺序问题。
    /// </summary>
    public void Merge(Dictionary<string, JToken> token)
    {
        if (Tokens.Count == 0)
        {
            Tokens.Add(CloneTokenDictionary(token));
            return;
        }

        var target = Tokens[0];
        foreach (var kv in token)
        {
            if (target.TryGetValue(kv.Key, out var existing)
                && existing is JObject existingObj
                && kv.Value is JObject newObj)
            {
                // 节点级别深度合并：新属性覆盖同名的旧属性
                foreach (var prop in newObj.Properties())
                {
                    existingObj[prop.Name] = prop.Value.DeepClone();
                }
            }
            else
            {
                target[kv.Key] = kv.Value.DeepClone();
            }
        }
    }

    public static MaaToken FromDictionary(Dictionary<string, JToken> token)
    {
        MaaToken result = new MaaToken();
        result.Merge(token);
        return result;
    }

    public override string ToString()
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };
        return JsonConvert.SerializeObject(Tokens, settings);
    }

    private static Dictionary<string, JToken> CloneTokenDictionary(Dictionary<string, JToken> token)
    {
        return token.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.DeepClone());
    }
}
