using System;

namespace MFAAvalonia.Models;

/// <summary>刀帐中的一条刀剑记录。</summary>
public sealed class SwordBookEntry
{
    public SwordBookEntry(string number, string type, string name)
    {
        Number = number;
        Type = type;
        Name = name;
    }

    public string Number { get; }
    public string Type { get; }
    public string Name { get; }
    public bool Owned { get; set; }
    public bool Wounded { get; set; }
    public bool TrueSword { get; set; }
    public bool InnerCare { get; set; }
    public bool Casual { get; set; }

    public SwordBookEntry Clone() => new(Number, Type, Name)
    {
        Owned = Owned,
        Wounded = Wounded,
        TrueSword = TrueSword,
        InnerCare = InnerCare,
        Casual = Casual,
    };
}

public enum SwordPortraitType
{
    Wounded,
    TrueSword,
    InnerCare,
    Casual,
}
