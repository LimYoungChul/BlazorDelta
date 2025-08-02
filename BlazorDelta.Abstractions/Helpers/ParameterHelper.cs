using System;
using System.Collections.Generic;

namespace BlazorDelta.Abstractions.Helpers
{
    public static class ParameterHelper
    {
        public static bool IsHtmlAttributeSafeType(object? value)
        {
            if (value == null) return true;

            var type = value.GetType();

            // Handle nullable types
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                type = Nullable.GetUnderlyingType(type) ?? type;
            }

            // Check for HTML attribute-safe types
            return type == typeof(string) ||
                   type == typeof(bool) ||
                   type == typeof(int) ||
                   type == typeof(long) ||
                   type == typeof(short) ||
                   type == typeof(byte) ||
                   type == typeof(uint) ||
                   type == typeof(ulong) ||
                   type == typeof(ushort) ||
                   type == typeof(sbyte) ||
                   type == typeof(float) ||
                   type == typeof(double) ||
                   type == typeof(decimal) ||
                   type == typeof(DateTime) ||
                   type == typeof(DateTimeOffset) ||
                   type == typeof(TimeSpan) ||
                   type == typeof(Guid) ||
                   type.IsEnum;
        }

        public static bool IsKnownEventType(string parameterName)
        {
            return parameterName == "oninput" ||
                   parameterName == "onchange" ||
                   parameterName == "onblur" ||
                   parameterName == "onfocus";
        }

    }
}