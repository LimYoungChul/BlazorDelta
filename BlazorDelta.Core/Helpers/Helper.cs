using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace BlazorDelta.Core.Helpers
{
    internal static class Helper
    {
        private const string DeltaComponent = "DeltaComponentBase";
        private const string DotDeltaComponent = "." + DeltaComponent;

        internal static bool InheritsFromDeltaComponentBase(INamedTypeSymbol classSymbol)
        {
            // First check if the class itself IS DeltaComponentBase
            if (classSymbol.Name == DeltaComponent ||
                classSymbol.ToDisplayString() == DeltaComponent ||
                classSymbol.ToDisplayString().EndsWith(DotDeltaComponent))
            {
                return true;
            }

            // Then check the inheritance chain
            var currentType = classSymbol.BaseType;
            while (currentType != null)
            {
                if (currentType.Name == DeltaComponent ||
                    currentType.ToDisplayString() == DeltaComponent ||
                    currentType.ToDisplayString().EndsWith(DotDeltaComponent))
                {
                    return true;
                }
                currentType = currentType.BaseType;
            }
            return false;
        }

        internal static bool IsEventCallbackType(ITypeSymbol type)
        {
            return type.Name.StartsWith("EventCallback");
        }

        internal static bool AreEventCallbackTypesCompatible(ITypeSymbol eventType, ITypeSymbol changedType)
        {
            // Both should be EventCallback types
            if (!IsEventCallbackType(eventType) || !IsEventCallbackType(changedType))
                return false;

            // Get the generic type arguments if they exist
            if (eventType is INamedTypeSymbol eventNamedType && changedType is INamedTypeSymbol changedNamedType)
            {
                // For EventCallback (non-generic), both should have no type arguments
                if (eventNamedType.TypeArguments.Length == 0 && changedNamedType.TypeArguments.Length == 0)
                    return true;

                // For EventCallback<T>, the type arguments should be compatible
                if (eventNamedType.TypeArguments.Length == 1 && changedNamedType.TypeArguments.Length == 1)
                {
                    var eventArgType = eventNamedType.TypeArguments[0];
                    var changedArgType = changedNamedType.TypeArguments[0];

                    // Check if the types are the same or compatible
                    return SymbolEqualityComparer.Default.Equals(eventArgType, changedArgType);
                }
            }

            return false;
        }


    }
}
