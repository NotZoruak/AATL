using System;
using System.Collections.Generic;

namespace MFAAvalonia.Helper;

// ServiceRegistry.cs
public static class ServiceRegistry
{
    public static List<Type> RegisteredTypes { get; } = new();
    
    public static void Register(Type type) => RegisteredTypes.Add(type);
}