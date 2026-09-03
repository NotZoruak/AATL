namespace MFAAvalonia.Services;

/// <summary>刀帐列的拥有状态筛选。</summary>
public enum SwordBookFilter
{
    All,
    Owned,
    Unowned,
}

/// <summary>刀帐拥有状态筛选匹配器。</summary>
public static class SwordBookFilterMatcher
{
    /// <summary>判断一条记录是否符合指定筛选。</summary>
    public static bool Matches(bool owned, SwordBookFilter filter) => filter switch
    {
        SwordBookFilter.Owned => owned,
        SwordBookFilter.Unowned => !owned,
        _ => true,
    };
}
