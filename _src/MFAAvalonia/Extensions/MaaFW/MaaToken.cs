using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace MFAAvalonia.Extensions.MaaFW;

public class MaaToken
{
    private List<Dictionary<string, JToken>> Tokens = [];


    public void Merge(Dictionary<string, JToken> token)
    {
        var cloned = CloneTokenDictionary(token);
        foreach (var existing in Tokens)
        {
            string? mergedKey = null;
            foreach (var pair in cloned)
            {
                if (!existing.TryGetValue(pair.Key, out var current)
                    || current is not JObject currentObject
                    || pair.Value is not JObject incomingObject)
                {
                    continue;
                }

                MergeObject(currentObject, incomingObject);
                mergedKey = pair.Key;
                break;
            }

            if (mergedKey != null)
                cloned.Remove(mergedKey);

            if (cloned.Count == 0)
                break;
        }

        if (cloned.Count > 0)
            Tokens.Add(cloned);
    }

    public void CopyAliases(string source, params string[] targets)
    {
        foreach (var token in Tokens)
        {
            if (!token.TryGetValue(source, out var value))
                continue;

            foreach (var target in targets)
                token[target] = value.DeepClone();
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

    /// <summary>递归合并同一个 node 的覆盖字段，保留多个复选项产生的 action 参数。</summary>
    private static void MergeObject(JObject target, JObject source)
    {
        foreach (var property in source.Properties())
        {
            if (property.Value is JObject sourceObject
                && target[property.Name] is JObject targetObject)
            {
                MergeObject(targetObject, sourceObject);
            }
            else
            {
                target[property.Name] = property.Value.DeepClone();
            }
        }
    }
}
