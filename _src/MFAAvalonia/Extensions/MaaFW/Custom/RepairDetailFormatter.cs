using System.Collections.Generic;
using System.Linq;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public static class RepairDetailFormatter
{
    public static string Format(string name, IReadOnlyList<int> costs)
    {
        var hasCost = costs.Any(cost => cost >= 0);
        if (string.IsNullOrWhiteSpace(name) && !hasCost)
            return string.Empty;

        var costText = string.Join('/', costs.Select(cost => cost >= 0 ? cost.ToString() : "未识别"));
        return string.IsNullOrWhiteSpace(name) ? costText : $"{name} {costText}";
    }
}
