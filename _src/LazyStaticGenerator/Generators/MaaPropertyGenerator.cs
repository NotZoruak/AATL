using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace LazyStaticGenerator.Generators;

[Generator]
public class MaaPropertyGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. 筛选带有 [MaaProperty] 标记的类
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: (node, _) => IsValidClassSyntax(node),
                transform: (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
            .Where(cls => cls != null)
            .Collect();

        // 2. 注册生成逻辑
        context.RegisterSourceOutput(classDeclarations, GenerateCode);
    }

    // 判断类是否带有 [MaaProperty] 标记
    private static bool IsValidClassSyntax(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax cls
            && cls.AttributeLists.Any(attrList =>
                attrList.Attributes.Any(attr =>
                    attr.Name.ToString() == "MaaProperty"));
    }

    private void GenerateCode(
        SourceProductionContext context,
        ImmutableArray<ClassDeclarationSyntax> classes)
    {
        foreach (var classSyntax in classes)
        {
            var sourceBuilder = new StringBuilder();

            // 1. 提取 namespace 和 class 名称
            string namespaceName = GetNamespace(classSyntax);

            // 2. 收集源文件中的所有 using 指令
            var usingDirectives = classSyntax.SyntaxTree
                .GetRoot()
                .DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Select(u => u.ToFullString().Trim())
                .Distinct()
                .ToList();
            var usingBlock = string.Join("\n", usingDirectives);

            var className = classSyntax.Identifier.Text;

            // 3. 遍历类的所有字段
            foreach (var field in classSyntax.Members.OfType<FieldDeclarationSyntax>())
            {
                // 筛选以 _ 开头的私有字段
                if (!IsValidFieldSyntax(field)) continue;

                // 提取字段的属性和类型
                var attributes = field.AttributeLists
                    .SelectMany(attrList => attrList.Attributes)
                    .Select(attr =>
                    {
                        // 将 [MaaJsonProperty] 替换为 [JsonProperty]
                        if (attr.Name.ToString() == "MaaJsonProperty")
                        {
                            var argument = attr.ArgumentList?.Arguments.FirstOrDefault();
                            if (argument != null)
                            {
                                return $"[JsonProperty({argument.ToFullString().Trim()})]";
                            }
                        }
                        return $"[{attr.ToFullString().Trim()}]";
                    })
                    .ToList();

                var fieldType = field.Declaration.Type.ToString();
                var fieldName = field.Declaration.Variables.First().Identifier.Text;
                var propertyName = fieldName.TrimStart('_');
                propertyName = char.ToUpper(propertyName[0]) + propertyName.Substring(1);

                // 4. 生成属性代码
                sourceBuilder.AppendLine($"        {string.Join("\n", attributes)}");
                sourceBuilder.AppendLine($"        public {fieldType} {propertyName}");
                sourceBuilder.AppendLine("        {");
                sourceBuilder.AppendLine($"            get => {fieldName};");
                sourceBuilder.AppendLine($"            set => SetNewProperty(ref {fieldName}, value);");
                sourceBuilder.AppendLine("        }");
            }

            // 5. 生成完整的类代码
            var source = $@"
#pragma warning disable
#nullable enable
{usingBlock}

namespace {namespaceName}
{{
    {GetClassModifiers(classSyntax)}class {classSyntax.Identifier.Text}
    {{
    {sourceBuilder}
    }}
}}";
            context.AddSource(
                $"{className}_Generated.g.cs",
                SourceText.From(source, Encoding.UTF8));
        }
    }

    // 判断字段是否有效
    private static bool IsValidFieldSyntax(FieldDeclarationSyntax field)
    {
        return field.Modifiers.Any(SyntaxKind.PrivateKeyword)
            && field.Declaration.Variables.Any(v => v.Identifier.Text.StartsWith("_"));
    }

    private static string GetNamespace(ClassDeclarationSyntax classSyntax)
        => (classSyntax.Parent as NamespaceDeclarationSyntax)?.Name.ToString()
            ?? "MFAAvalonia.Extensions.MaaFW";

    private static string GetClassModifiers(ClassDeclarationSyntax classSyntax)
        => string.Join(" ", classSyntax.Modifiers.Select(m => m.Text)) + " ";
}
