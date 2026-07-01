using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System;

namespace LazyStaticGenerator.Generators
{
    [Generator]
    public class LazyStaticGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // 1. 仅通过语法树筛选标记 [LazyStatic] 的类
            var classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate:  (node, _) => IsValidClassSyntax(node),
                    transform:  (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
                .Where( cls => cls != null)
                .Collect();

            // 2. 直接注册生成逻辑（无需结合 Compilation）
            context.RegisterSourceOutput(classDeclarations, GenerateCode);
        }

        private static bool IsValidClassSyntax(SyntaxNode node)
        {
            return node is ClassDeclarationSyntax cls
                && cls.AttributeLists.Any(attrList =>
                    attrList.Attributes.Any(attr =>
                        attr.Name.ToString() == "LazyStatic"));
        }

        private void GenerateCode(
            SourceProductionContext context,
            ImmutableArray<ClassDeclarationSyntax> classes)
        {
            foreach (var classSyntax in classes)
            {
                var sourceBuilder = new StringBuilder();
                string namespaceName = GetNamespace(classSyntax);

                // 1. 收集源文件中的所有 using 指令
                var usingDirectives = classSyntax.SyntaxTree
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<UsingDirectiveSyntax>()
                    .Select(u => u.ToFullString().Trim()) // 保留完整格式（含注释）
                    .Distinct()
                    .ToList();
                // 2. 生成 using 指令块
                var usingBlock = string.Join("\n", usingDirectives);

                var resolverCode = @"";
                sourceBuilder.AppendLine(resolverCode);
                // 3. 通过纯语法分析处理字段
                foreach (var field in classSyntax.Members.OfType<FieldDeclarationSyntax>())
                {
                    if (!IsValidField(field)) continue;

                    foreach (var variable in field.Declaration.Variables)
                    {
                        GeneratePropertyCode(variable, sourceBuilder);
                    }
                }

                if (sourceBuilder.Length > 0)
                {
                    var source = $@"
#pragma warning disable
#nullable enable
{usingBlock}

namespace {namespaceName};

{GetClassModifiers(classSyntax)}class {classSyntax.Identifier.Text}
{{
{sourceBuilder}
}}";
                    context.AddSource(
                        $"{classSyntax.Identifier.Text}_LazyStatic.g.cs",
                        SourceText.From(source, Encoding.UTF8));
                }
            }
        }

        // 4. 字段验证（纯语法树判断）
        private static bool IsValidField(FieldDeclarationSyntax field)
        {
            return field.Modifiers.Any(SyntaxKind.PrivateKeyword)
                && field.Modifiers.Any(SyntaxKind.StaticKeyword)
                && field.Declaration.Variables.Any(v =>
                    v.Identifier.Text.StartsWith("_"));
        }

        // 5. 生成属性逻辑（无语义模型依赖）
        private static void GeneratePropertyCode(
            VariableDeclaratorSyntax variable,
            StringBuilder sourceBuilder)
        {
            var fieldName = variable.Identifier.Text;
            var propertyName = fieldName.TrimStart('_');
            if (propertyName.Length == 0 || fieldName == "ServiceCache") return; // 排除 ServiceCache 字段

            propertyName = char.ToUpper(propertyName[0]) + propertyName.Substring(1);

            // 分步获取父节点，并添加 null 检查
            var parent = variable.Parent; // VariableDeclarationSyntax
            if (parent == null)
                throw new InvalidOperationException("Variable has no parent node.");

            var grandParent = parent.Parent; // FieldDeclarationSyntax
            if (grandParent == null)
                throw new InvalidOperationException("Variable's parent has no grandparent node.");

// 显式类型转换（替代直接强制转换）
            var fieldDeclaration = grandParent as FieldDeclarationSyntax;
            if (fieldDeclaration == null)
                throw new InvalidOperationException("Variable's grandparent is not a FieldDeclarationSyntax.");

            var fieldType = fieldDeclaration.Declaration.Type;
            // sourceBuilder.AppendLine($@"
            //     public static Lazy<{fieldType}> {propertyName} {{ get; }} 
            //         = new Lazy<{fieldType}>(() => new {fieldType}());");
            sourceBuilder.AppendLine($@"    public static {fieldType} {propertyName} => Resolve<{fieldType}>();");

        }

        // 辅助方法保持静态化
        private static string GetNamespace(ClassDeclarationSyntax classSyntax)
            => (classSyntax.Parent as NamespaceDeclarationSyntax)?.Name.ToString()
                ?? "MFAAvalonia.Helper";

        private static string GetClassModifiers(ClassDeclarationSyntax classSyntax)
            => string.Join(" ", classSyntax.Modifiers.Select(m => m.Text)) + " ";
    }
}
