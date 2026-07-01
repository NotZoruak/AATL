using System.Reflection;
var asm = Assembly.LoadFrom(@"bin\AnyCPU\Release\MaaFramework.Binding.dll");
foreach (var t in asm.GetExportedTypes().OrderBy(x => x.FullName))
{
    if (t.Name.Contains("Toolkit") || t.Name.Contains("Ocr"))
    {
        Console.WriteLine($"\n=== {t.FullName} ===");
        foreach (var m in t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            Console.WriteLine($"  {m.MemberType}: {m.Name}");
    }
}
