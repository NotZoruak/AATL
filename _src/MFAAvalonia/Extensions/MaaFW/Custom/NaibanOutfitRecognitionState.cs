using System;
using System.Collections.Generic;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

/// <summary>
/// 管理一次内番安排中的内番服识别结果，避免对话动画重复命中产生重复日志。
/// </summary>
public sealed class NaibanOutfitRecognitionState
{
    private const int MaxSwordCount = 2;
    private readonly HashSet<string> _swordNames = new(StringComparer.Ordinal);
    private bool _isFinished;

    /// <summary>
    /// 本轮已确认的刀剑名称。
    /// </summary>
    public IReadOnlyCollection<string> SwordNames => _swordNames;

    /// <summary>
    /// 本轮结束时是否需要记录未显示内番服立绘。
    /// </summary>
    public bool ShouldLogMissingOutfit => _swordNames.Count == 0;

    /// <summary>
    /// 开始新一轮内番服识别。
    /// </summary>
    public void Begin()
    {
        _swordNames.Clear();
        _isFinished = false;
    }

    /// <summary>
    /// 尝试记录一把刀剑。相同刀剑只记录一次，单轮最多记录两把。
    /// </summary>
    public bool TryRecord(string swordName)
    {
        if (string.IsNullOrWhiteSpace(swordName) || _swordNames.Count >= MaxSwordCount)
            return false;

        return _swordNames.Add(swordName);
    }

    /// <summary>
    /// 结束本轮识别。仅当未识别到内番服且此前未结算时返回 true。
    /// </summary>
    public bool TryFinishMissingOutfit()
    {
        if (_isFinished)
            return false;

        _isFinished = true;
        return ShouldLogMissingOutfit;
    }
}
