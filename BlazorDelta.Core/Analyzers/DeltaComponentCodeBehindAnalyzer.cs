using BlazorDelta.Core.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Diagnostics;
using System.Linq;


[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DeltaComponentCodeBehindAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "DELTA001";



    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "DeltaComponent with parameters should use code-behind file",
        "Component '{0}' should have a code-behind file, create '{0}.razor.cs' (can be empty) or use 'Extract block to code behind' quick action",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Components with [Parameter] attributes benefit from having a code-behind file (even if empty) when using BlazorDelta source generators. You can create an empty .razor.cs file and continue using @code blocks in the .razor file.");


    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

        // Use Microsoft's pattern!
        context.RegisterCompilationStartAction(context =>
        {
            context.RegisterSymbolStartAction(context =>
            {
                var type = (INamedTypeSymbol)context.Symbol;

                // Check if it inherits from DeltaComponentBase
                if (!Helper.InheritsFromDeltaComponentBase(type))
                    return;

                // Check if it has code-behind
                if (HasCodeBehindFile(type, context.Compilation))
                    return;

                // Look for [Parameter] properties
                var parameterProperties = new List<IPropertySymbol>();
                foreach (var member in type.GetMembers())
                {
                    if (member is IPropertySymbol property && HasParameterAttribute(property))
                    {
                        parameterProperties.Add(property);
                    }
                }

                if (parameterProperties.Count == 0)
                    return;


                foreach (var property in parameterProperties)
                {
                    var propertyLocation = property.Locations.FirstOrDefault();
                    if (propertyLocation == null)
                    {
                        continue;
                    }

                    context.RegisterSymbolEndAction(context =>
                    {
                        var diagnostic = Diagnostic.Create(Rule, propertyLocation, type.Name);
                        context.ReportDiagnostic(diagnostic);
                    });
                    break;
                }



            }, SymbolKind.NamedType);
        });
    }

    private bool HasParameterAttribute(IPropertySymbol property)
    {
        return property.GetAttributes().Any(attr =>
            attr.AttributeClass?.Name == "ParameterAttribute" ||
            attr.AttributeClass?.Name == "Parameter");
    }

    private bool HasCodeBehindFile(INamedTypeSymbol type, Compilation compilation)
    {
        // Check if there are partial declarations in .razor.cs files
        foreach (var location in type.Locations)
        {
            var filePath = location.SourceTree?.FilePath;
            if (filePath != null &&
                (filePath.EndsWith(".razor.cs") ||
                 (filePath.EndsWith(".cs") && !filePath.Contains(".g.cs"))))
            {
                return true;
            }
        }
        return false;
    }
}