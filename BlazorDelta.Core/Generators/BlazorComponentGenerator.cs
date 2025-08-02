using BlazorDelta.Core.Helpers;
using BlazorDelta.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BlazorDelta.Core.Generators;

[Generator]
public class BlazorComponentGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        try
        {
            // Find all classes that inherit from DeltaComponentBase
            var componentClasses = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsClassDeclaration(s),
                    transform: static (ctx, _) => GetClassDeclaration(ctx))
                .Where(static m => m is not null);

            // Combine with compilation for semantic analysis
            var compilationAndClasses = context.CompilationProvider.Combine(componentClasses.Collect());

            context.RegisterSourceOutput(compilationAndClasses,
                static (spc, source) => Execute(source.Left, source.Right, spc));
        }
        catch (Exception ex)
        {
            // Register a diagnostic if initialization fails
            context.RegisterPostInitializationOutput(ctx =>
            {
                var source = $@"
                    // Source generator initialization failed: {ex.Message}
                    // Stack trace: {ex.StackTrace}
";
                ctx.AddSource("SourceGeneratorError.cs", source);
            });
        }
    }

    private static bool IsClassDeclaration(SyntaxNode node)
    {
        try
        {
            return node is ClassDeclarationSyntax classDecl && classDecl.BaseList != null;
        }
        catch
        {
            return false;
        }
    }

    private static ClassDeclarationSyntax? GetClassDeclaration(GeneratorSyntaxContext context)
    {
        try
        {
            if (context.Node is not ClassDeclarationSyntax classDeclaration)
                return null;

            // Check if it's partial (safer way)
            var isPartial = false;
            foreach (var modifier in classDeclaration.Modifiers)
            {
                if (modifier.ValueText == "partial")
                {
                    isPartial = true;
                    break;
                }
            }
            if (!isPartial)
                return null;

            // Use semantic model to check inheritance hierarchy
            var semanticModel = context.SemanticModel;
            var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

            if (classSymbol == null)
                return null;

            // Check if class inherits from DeltaComponentBase anywhere in the hierarchy
            if (Helper.InheritsFromDeltaComponentBase(classSymbol))
                return classDeclaration;

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax?> classes, SourceProductionContext context)
    {
        if (classes.IsDefaultOrEmpty)
            return;

        var processedClasses = new HashSet<string>();

        foreach (var classDeclaration in classes)
        {
            if (classDeclaration is null)
                continue;

            var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);
            var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);

            if (classSymbol is null)
                continue;

            // Create unique identifier for this class
            var classId = classSymbol.ToDisplayString();

            // Skip if we've already processed this class
            if (!processedClasses.Add(classId))
            {
                var duplicateDescriptor = new DiagnosticDescriptor(
                    "BSG101",
                    "Duplicate class detected",
                    "Skipping duplicate generation for class {0}",
                    "BlazorDelta",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true);

                context.ReportDiagnostic(Diagnostic.Create(duplicateDescriptor,
                    classDeclaration.GetLocation(),
                    classId));
                continue;
            }

            // Verify this actually inherits from DeltaComponentBase
            if (!Helper.InheritsFromDeltaComponentBase(classSymbol))
                continue;

            try
            {
                var componentInfo = AnalyzeComponent(classSymbol);
                var source = GenerateSource(componentInfo);

                // Use a different suffix to avoid Razor compiler conflicts
                var fileName = $"{classSymbol.Name}.g.cs";
                context.AddSource(fileName, source);

                // Add diagnostic to see what's being generated
                var generatedDescriptor = new DiagnosticDescriptor(
                    "BSG100",
                    "Generated source file",
                    "Generated {0} for class {1}",
                    "BlazorDelta",
                    DiagnosticSeverity.Info,
                    isEnabledByDefault: true);

                context.ReportDiagnostic(Diagnostic.Create(generatedDescriptor,
                    classDeclaration.GetLocation(),
                    fileName,
                    classSymbol.ToDisplayString()));
            }
            catch (Exception ex)
            {
                // Add diagnostic for debugging
                var descriptor = new DiagnosticDescriptor(
                    "BSG001",
                    "Source generation error",
                    "Error generating source for {0}: {1}",
                    "BlazorDelta",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true);

                context.ReportDiagnostic(Diagnostic.Create(descriptor,
                    classDeclaration.GetLocation(),
                    classSymbol.Name,
                    ex.Message));
            }
        }
    }

    private static ComponentInfo AnalyzeComponent(INamedTypeSymbol classSymbol)
    {
        var parameters = new List<ParameterInfo>();
        var handlers = new List<HandlerInfo>();
        ParameterInfo? captureUnmatchedValuesParameter = null;

        // Walk up the inheritance chain to find all parameters
        var current = classSymbol;
        while (current != null && current.Name != "DeltaComponentBase")
        {
            // Find parameters in this type
            foreach (var member in current.GetMembers().OfType<IPropertySymbol>())
            {
                var parameterAttribute = member.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == "ParameterAttribute");

                var cascadingParameterAttribute = member.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == "CascadingParameterAttribute");

                if (parameterAttribute != null || cascadingParameterAttribute != null)
                {
                    var parameterInfo = new ParameterInfo
                    {
                        Name = member.Name,
                        Type = member.Type,
                        HasSetOnce = HasAttribute(member, "SetOnceAttribute"),
                        HasNoRender = HasAttribute(member, "NoRenderAttribute"),
                        HasUpdatesCss = HasAttribute(member, "UpdatesCssAttribute"),
                        IsCascading = cascadingParameterAttribute != null
                    };

                    // Handle CaptureUnmatchedValues (only for regular parameters, not cascading)
                    if (parameterAttribute != null)
                    {
                        parameterInfo.CaptureUnmatchedValues = GetCaptureUnmatchedValues(parameterAttribute);

                        if (parameterInfo.CaptureUnmatchedValues)
                        {
                            captureUnmatchedValuesParameter = parameterInfo;
                        }
                        else
                        {
                            parameters.Add(parameterInfo);
                        }
                    }
                    else
                    {
                        // Cascading parameter
                        parameters.Add(parameterInfo);
                    }
                }
            }

            // Find parameter change handlers in this type
            foreach (var member in current.GetMembers().OfType<IMethodSymbol>())
            {
                var handlerAttribute = member.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == "OnParameterChangedAttribute");

                if (handlerAttribute != null && handlerAttribute.ConstructorArguments.Length > 0)
                {
                    var parameterName = handlerAttribute.ConstructorArguments[0].Value?.ToString();
                    if (!string.IsNullOrEmpty(parameterName))
                    {
                        var handlerInfo = new HandlerInfo
                        {
                            ParameterName = parameterName!,
                            MethodName = member.Name,
                            IsAsync = member.ReturnType.Name == "Task"
                        };

                        handlers.Add(handlerInfo);
                    }
                }
            }

            current = current.BaseType;
        }

        var (className, constraintsText) = GenerateClassNameAndConstraints(classSymbol);

        return new ComponentInfo
        {
            ClassName = className,  // e.g., "FormComponent<T>"
            ClassConstraints = constraintsText, // e.g., "where T : class"
            Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
            Parameters = parameters,
            Handlers = handlers,
            CaptureUnmatchedValuesParameter = captureUnmatchedValuesParameter
        };
    }

    // Helper method that returns both class name with generics and constraints
    private static (string className, string constraints) GenerateClassNameAndConstraints(INamedTypeSymbol classSymbol)
    {
        string className = classSymbol.Name;
        string constraints = string.Empty;

        if (classSymbol.IsGenericType)
        {
            var typeParams = string.Join(", ", classSymbol.TypeParameters.Select(tp => tp.Name));
            className = $"{classSymbol.Name}<{typeParams}>";

            // Generate constraints
            var constraintsList = new List<string>();
            foreach (var typeParam in classSymbol.TypeParameters)
            {
                var paramConstraints = new List<string>();

                if (typeParam.HasValueTypeConstraint)
                    paramConstraints.Add("struct");
                if (typeParam.HasReferenceTypeConstraint)
                    paramConstraints.Add("class");
                if (typeParam.HasConstructorConstraint)
                    paramConstraints.Add("new()");

                foreach (var constraintType in typeParam.ConstraintTypes)
                    paramConstraints.Add(constraintType.ToDisplayString());

                if (paramConstraints.Any())
                    constraintsList.Add($"where {typeParam.Name} : {string.Join(", ", paramConstraints)}");
            }

            if (constraintsList.Any())
                constraints = string.Join("\n    ", constraintsList);
        }

        return (className, constraints);
    }

    private static bool HasAttribute(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes().Any(a => a.AttributeClass?.Name == attributeName);
    }

    private static bool GetCaptureUnmatchedValues(AttributeData parameterAttribute)
    {
        // Look for CaptureUnmatchedValues = true in the Parameter attribute
        var captureUnmatchedValues = parameterAttribute.NamedArguments
            .FirstOrDefault(na => na.Key == "CaptureUnmatchedValues");

        return captureUnmatchedValues.Value.Value is bool boolValue && boolValue;
    }

    private static string GenerateSource(ComponentInfo componentInfo)
    {
        var sb = new StringBuilder();

        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine($"namespace {componentInfo.Namespace};");
        sb.AppendLine();

        sb.AppendLine($"partial class {componentInfo.ClassName}");
        if (!string.IsNullOrEmpty(componentInfo.ClassConstraints))
        {
            sb.Append($"    {componentInfo.ClassConstraints}");
        }

        sb.AppendLine();
        sb.AppendLine("{");

        GeneratePreviousValueFields(sb, componentInfo);
        GenerateSetParametersFromSourceMethod(sb, componentInfo);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GeneratePreviousValueFields(StringBuilder sb, ComponentInfo componentInfo)
    {
        // Generate previous value fields only for SetOnce parameters
        foreach (var parameter in componentInfo.Parameters.Where(p => p.HasSetOnce))
        {
            var fieldName = $"_hasSet{parameter.Name}";
            sb.AppendLine($"    private bool {fieldName};");
        }

        if (HasBindingCapability(componentInfo))
        {
            sb.AppendLine("    private string? _currentEventType;");
        }
    }

    private static void GenerateSetParametersFromSourceMethod(StringBuilder sb, ComponentInfo componentInfo)
    {
        sb.AppendLine("    public override bool SetParametersFromSource(Microsoft.AspNetCore.Components.ParameterView parameters)");
        sb.AppendLine("    {");
        sb.AppendLine("        var hasChanges = false;");
        sb.AppendLine();
        sb.AppendLine("        foreach (var parameter in parameters)");
        sb.AppendLine("        {");
        sb.AppendLine("            switch (parameter.Name)");
        sb.AppendLine("            {");

        foreach (var parameter in componentInfo.Parameters)
        {
            GenerateParameterCase(sb, parameter, componentInfo.Handlers);
        }

        // Generate default case - only check for event types if component has binding capability
        sb.AppendLine("                default:");

        bool hasBindingCapability = HasBindingCapability(componentInfo);

        if (hasBindingCapability)
        {
            // First check for known event types (for @bind-x:event scenarios)
            sb.AppendLine("                    // Check for known event types (for @bind-x:event scenarios)");
            sb.AppendLine("                    if (BlazorDelta.Abstractions.Helpers.ParameterHelper.IsKnownEventType(parameter.Name))");
            sb.AppendLine("                    {");
            sb.AppendLine("                        // Cache the detected event type for binding");
            sb.AppendLine("                        if (_currentEventType != parameter.Name)");
            sb.AppendLine("                        {");
            sb.AppendLine("                            _currentEventType = parameter.Name;");
            sb.AppendLine("                            hasChanges = true;");
            sb.AppendLine("                        }");
            sb.AppendLine("                        break;");
            sb.AppendLine("                    }");
            sb.AppendLine();
        }

        // Then handle CaptureUnmatchedValues for non-event parameters
        if (componentInfo.CaptureUnmatchedValuesParameter != null)
        {
            if (hasBindingCapability)
            {
                sb.AppendLine("                    // Handle CaptureUnmatchedValues - only for HTML attribute-safe types");
            }
            else
            {
                sb.AppendLine("                    // Handle CaptureUnmatchedValues - only for HTML attribute-safe types");
            }
            GenerateUnmatchedValuesLogic(sb, componentInfo.CaptureUnmatchedValuesParameter);
        }

        sb.AppendLine("                    break;");

        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("        return hasChanges;");
        sb.AppendLine("    }");
    }
    private static void GenerateParameterCase(StringBuilder sb, ParameterInfo parameter, List<HandlerInfo> handlers)
    {
        sb.AppendLine($"                case \"{parameter.Name}\":");

        // Add comment for cascading parameters
        if (parameter.IsCascading)
        {
            sb.AppendLine("                    // Cascading parameter");
        }

        var typeDisplayString = GetTypeDisplayStringCore(parameter.Type);
        var comparisonStrategy = GetComparisonStrategy(parameter.Type);
        var handler = handlers.FirstOrDefault(h => h.ParameterName == parameter.Name);

        if (parameter.HasSetOnce)
        {
            // SetOnce logic with casting for performance
            sb.AppendLine($"                    var new{parameter.Name} = ({typeDisplayString})parameter.Value;");
            sb.AppendLine($"                    if (!_hasSet{parameter.Name})");
            sb.AppendLine("                    {");
            sb.AppendLine($"                        {parameter.Name} = new{parameter.Name};");
            sb.AppendLine($"                        _hasSet{parameter.Name} = true;");

            if (!parameter.HasNoRender)
            {
                sb.AppendLine("                        hasChanges = true;");
            }

            if (parameter.HasUpdatesCss)
            {
                sb.AppendLine("                        DirtyCss = true;");
            }

            if (handler != null)
            {
                if (handler.IsAsync)
                {
                    sb.AppendLine($"                        _ = {handler.MethodName}();");
                }
                else
                {
                    sb.AppendLine($"                        {handler.MethodName}();");
                }
            }

            sb.AppendLine("                    }");
        }
        else
        {
            // Normal parameter logic - use casting for performance
            string? comparisonCode = comparisonStrategy switch
            {
                ComparisonStrategy.Value => $"{parameter.Name} != new{parameter.Name}",
                ComparisonStrategy.Reference => $"!ReferenceEquals({parameter.Name}, new{parameter.Name})",
                ComparisonStrategy.Generic => $"!EqualityComparer<{GetTypeDisplayStringCore(parameter.Type)}>.Default.Equals({parameter.Name}, new{parameter.Name})",
                ComparisonStrategy.EventCallback => $"!{parameter.Name}.Equals(new{parameter.Name})",
                ComparisonStrategy.Never => null, // Always assign, no comparison needed
                _ => $"{parameter.Name} != new{parameter.Name}"
            };

            // Use direct casting - more performant than pattern matching for nullable value types
            sb.AppendLine($"                    var new{parameter.Name} = ({typeDisplayString})parameter.Value;");

            if (comparisonCode != null)
            {
                sb.AppendLine($"                    if ({comparisonCode})");
                sb.AppendLine("                    {");
            }

            sb.AppendLine($"                        {parameter.Name} = new{parameter.Name};");

            if (!parameter.HasNoRender)
            {
                sb.AppendLine("                        hasChanges = true;");
            }

            if (parameter.HasUpdatesCss)
            {
                sb.AppendLine("                        DirtyCss = true;");
            }

            if (handler != null)
            {
                if (handler.IsAsync)
                {
                    sb.AppendLine($"                        _ = {handler.MethodName}();");
                }
                else
                {
                    sb.AppendLine($"                        {handler.MethodName}();");
                }
            }

            if (comparisonCode != null)
            {
                sb.AppendLine("                    }");
            }
        }

        sb.AppendLine("                    break;");
        sb.AppendLine();
    }

    private static void GenerateUnmatchedValuesLogic(StringBuilder sb, ParameterInfo unmatchedParameter)
    {
        sb.AppendLine("                    if (BlazorDelta.Abstractions.Helpers.ParameterHelper.IsHtmlAttributeSafeType(parameter.Value))");
        sb.AppendLine("                    {");
        sb.AppendLine($"                        if ({unmatchedParameter.Name} == null)");
        sb.AppendLine("                        {");
        sb.AppendLine($"                            {unmatchedParameter.Name} = new Dictionary<string, object>();");
        sb.AppendLine("                            hasChanges = true;");
        sb.AppendLine("                        }");
        sb.AppendLine();
        sb.AppendLine($"                        if ({unmatchedParameter.Name}.TryGetValue(parameter.Name, out var existingValue))");
        sb.AppendLine("                        {");
        sb.AppendLine("                            if (!Equals(parameter.Value, existingValue))");
        sb.AppendLine("                            {");

        // Check if it's a readonly interface vs mutable dictionary
        var typeString = unmatchedParameter.Type.ToDisplayString();
        if (typeString.Contains("IReadOnlyDictionary"))
        {
            // Immutable pattern for IReadOnlyDictionary
            sb.AppendLine($"                                var newDict = new Dictionary<string, object?>({unmatchedParameter.Name});");
            sb.AppendLine("                                newDict[parameter.Name] = parameter.Value;");
            sb.AppendLine($"                                {unmatchedParameter.Name} = newDict;");
        }
        else
        {
            // Direct mutation for Dictionary
            sb.AppendLine($"                                {unmatchedParameter.Name}[parameter.Name] = parameter.Value;");
        }

        sb.AppendLine("                                hasChanges = true;");
        sb.AppendLine("                                // Special case: class attribute affects CSS");
        sb.AppendLine("                                if (parameter.Name == \"class\")");
        sb.AppendLine("                                {");
        sb.AppendLine("                                    DirtyCss = true;");
        sb.AppendLine("                                }");
        sb.AppendLine("                            }");
        sb.AppendLine("                        }");
        sb.AppendLine("                        else");
        sb.AppendLine("                        {");

        if (typeString.Contains("IReadOnlyDictionary"))
        {
            // Immutable pattern for IReadOnlyDictionary
            sb.AppendLine($"                            var newDict = new Dictionary<string, object?>({unmatchedParameter.Name});");
            sb.AppendLine("                            newDict.Add(parameter.Name, parameter.Value);");
            sb.AppendLine($"                            {unmatchedParameter.Name} = newDict;");
        }
        else
        {
            // Direct mutation for Dictionary
            sb.AppendLine($"                            {unmatchedParameter.Name}.Add(parameter.Name, parameter.Value);");
        }

        sb.AppendLine("                            hasChanges = true;");
        sb.AppendLine("                            // Special case: class attribute affects CSS");
        sb.AppendLine("                            if (parameter.Name == \"class\")");
        sb.AppendLine("                            {");
        sb.AppendLine("                                DirtyCss = true;");
        sb.AppendLine("                            }");
        sb.AppendLine("                        }");
        sb.AppendLine("                    }");
        sb.AppendLine("                    // Non-HTML-safe types (EventCallback, complex objects, etc.) are ignored");
    }

    private static ComparisonStrategy GetComparisonStrategy(ITypeSymbol type)
    {
        var typeName = type.Name;

        // Handle generic type parameters
        if (type.TypeKind == TypeKind.TypeParameter)
        {
            var typeParameter = (ITypeParameterSymbol)type;

            // Check constraints to determine comparison strategy
            if (typeParameter.HasValueTypeConstraint)
            {
                return ComparisonStrategy.Value;
            }
            else if (typeParameter.HasReferenceTypeConstraint)
            {
                return ComparisonStrategy.Reference;
            }
            else
            {
                // No constraint - use generic comparison that works for both
                return ComparisonStrategy.Generic;
            }
        }

        // EventCallback - struct, but doesn't support != operator, use .Equals()
        if (typeName.StartsWith("EventCallback"))
            return ComparisonStrategy.EventCallback;

        // Value types (including structs) - use value comparison
        if (type.IsValueType)
            return ComparisonStrategy.Value;

        // Func, Action, Expression, RenderFragment - reference comparison
        if (typeName.StartsWith("Func") ||
            typeName.StartsWith("Action") ||
            typeName.StartsWith("Expression") ||
            typeName.StartsWith("RenderFragment"))
            return ComparisonStrategy.Reference;

        // Collection types - reference comparison
        if (IsCollectionType(type))
            return ComparisonStrategy.Reference;

        // Everything else - value comparison (includes string, custom classes)
        return ComparisonStrategy.Value;
    }

    // Add this new helper method for pattern matching type strings
    private static string GetPatternMatchingTypeString(ITypeSymbol type)
    {
        // For nullable value types (int?, bool?, etc.), we need special handling
        // because they box as either the underlying type or null, never as Nullable<T>
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            var underlyingType = ((INamedTypeSymbol)type).TypeArguments[0];
            return GetTypeDisplayStringCore(underlyingType); // Return int instead of int?
        }

        if (type.TypeKind == TypeKind.TypeParameter)
        {
            return type.Name;
        }

        // For generic types (like EventCallback<string?>), preserve the exact generic arguments
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            return GetTypeDisplayStringCore(type); // Keep exact generic arguments
        }

        // For simple nullable reference types (like string?), use non-nullable for pattern matching
        if (type.NullableAnnotation == NullableAnnotation.Annotated && !type.IsValueType)
        {
            var nonNullableType = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
            return GetTypeDisplayStringCore(nonNullableType);
        }

        return GetTypeDisplayStringCore(type);
    }


    // Also update the GetTypeDisplayString method to use this core version
    private static string GetTypeDisplayStringCore(ITypeSymbol type)
    {
        // Handle nullable value types with shorthand syntax (int? instead of System.Nullable<int>)
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
        {
            var underlyingType = ((INamedTypeSymbol)type).TypeArguments[0];
            var underlyingTypeName = GetTypeDisplayStringCore(underlyingType);
            return $"{underlyingTypeName}?";
        }

        // For generic type parameters, just use the simple name
        if (type.TypeKind == TypeKind.TypeParameter)
        {
            return type.Name;
        }

        // For generic types containing type parameters, handle carefully and preserve nullability
        if (type is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var typeArguments = namedType.TypeArguments;

            // Get the base type name without generic arguments
            var baseTypeName = namedType.ConstructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // Clean up global:: prefix
            if (baseTypeName.StartsWith("global::"))
            {
                baseTypeName = baseTypeName.Substring(8);
            }

            // Remove existing type arguments
            var openBracket = baseTypeName.IndexOf('<');
            if (openBracket >= 0)
            {
                baseTypeName = baseTypeName.Substring(0, openBracket);
            }

            // Build type arguments - GetTypeDisplayStringCore already handles nullability
            var args = string.Join(", ", typeArguments.Select(GetTypeDisplayStringCore));

            return $"{baseTypeName}<{args}>";
        }

        // Use built-in display format that preserves nullability
        var format = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                                 SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                                 SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        );

        var fullName = type.ToDisplayString(format);

        // Clean up global:: prefix that sometimes appears
        if (fullName.StartsWith("global::"))
        {
            fullName = fullName.Substring(8);
        }

        return fullName;
    }

    private static bool HasBindingCapability(ComponentInfo componentInfo)
    {
        // Check if component has any X + XChanged pairs (binding capability)
        var parameterNames = new HashSet<string>(componentInfo.Parameters.Select(p => p.Name));

        return componentInfo.Parameters.Any(param =>
        {
            // Look for XChanged parameters that are EventCallback<T>
            if (param.Name.EndsWith("Changed") &&
                param.Type.Name.StartsWith("EventCallback"))
            {
                // Extract the base name (remove "Changed" suffix)
                var baseName = param.Name.Substring(0, param.Name.Length - 7);

                // Check if we have the corresponding value parameter
                return parameterNames.Contains(baseName);
            }
            return false;
        });
    }


    private static bool IsCollectionType(ITypeSymbol type)
    {
        if (type.Name == "String") // String implements IEnumerable but isn't a collection
            return false;

        return type.AllInterfaces.Any(i =>
            i.Name == "IEnumerable" ||
            i.Name == "ICollection" ||
            i.Name == "IList");
    }
}
