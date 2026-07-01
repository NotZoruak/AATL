using System;

namespace MFAAvalonia.Utilities.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public class MaaJsonPropertyAttribute(string name) : Attribute
{
    public string Name => name;

}