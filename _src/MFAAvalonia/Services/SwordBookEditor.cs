using MFAAvalonia.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MFAAvalonia.Services;

/// <summary>管理刀帐编辑状态与已保存状态。</summary>
public sealed class SwordBookEditor
{
    private readonly Dictionary<string, SwordBookEntry> _savedEntries;

    public SwordBookEditor(IEnumerable<SwordBookEntry> entries)
    {
        _savedEntries = entries.ToDictionary(entry => entry.Number, entry => entry.Clone(), StringComparer.Ordinal);
        Entries = _savedEntries.Values.Select(entry => entry.Clone()).ToList();
    }

    public IReadOnlyList<SwordBookEntry> Entries { get; private set; }

    public void SetOwned(string number, SwordPortraitType portrait, bool owned)
    {
        var entry = Entries.FirstOrDefault(item => item.Number == number);
        if (entry == null)
            return;

        switch (portrait)
        {
            case SwordPortraitType.Wounded:
                entry.Wounded = owned;
                break;
            case SwordPortraitType.TrueSword:
                entry.TrueSword = owned;
                break;
            case SwordPortraitType.InnerCare:
                entry.InnerCare = owned;
                break;
            case SwordPortraitType.Casual:
                entry.Casual = owned;
                break;
        }
    }

    public void SetSwordOwned(string number, bool owned)
    {
        var entry = Entries.FirstOrDefault(item => item.Number == number);
        if (entry != null)
            entry.Owned = owned;
    }

    public void Save()
    {
        _savedEntries.Clear();
        foreach (var entry in Entries)
            _savedEntries[entry.Number] = entry.Clone();
    }

    public void Revert()
    {
        Entries = _savedEntries.Values.Select(entry => entry.Clone()).ToList();
    }
}
