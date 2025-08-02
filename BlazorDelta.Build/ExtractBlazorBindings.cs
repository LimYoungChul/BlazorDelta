using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace BlazorDelta.Build
{
    /// <summary>
    /// MSBuild task that extracts binding patterns from .razor files
    /// </summary>
    public class ExtractBlazorBindings : Task
    {
        [Required]
        public ITaskItem[] RazorFiles { get; set; } = Array.Empty<ITaskItem>();

        [Required]
        public string OutputFile { get; set; } = string.Empty;

        public override bool Execute()
        {
            try
            {
                var allBindings = new Dictionary<string, Dictionary<string, string>>();

                foreach (var razorFile in RazorFiles)
                {
                    var filePath = razorFile.ItemSpec;
                    var componentName = Path.GetFileNameWithoutExtension(filePath);

                    Log.LogMessage(MessageImportance.Low, $"Analyzing {componentName} for binding patterns...");

                    var bindings = ExtractBindingsFromFile(filePath);
                    if (bindings.Any())
                    {
                        allBindings[componentName] = bindings;
                        Log.LogMessage(MessageImportance.Normal,
                            $"Found {bindings.Count} binding patterns in {componentName}: {string.Join(", ", bindings.Select(kvp => $"{kvp.Key}:{kvp.Value}"))}");
                    }
                }

                // Ensure output directory exists
                var outputDir = Path.GetDirectoryName(OutputFile);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Write JSON output
                var json = SerializeToJson(allBindings);

                File.WriteAllText(OutputFile, json);
                Log.LogMessage(MessageImportance.Normal, $"Binding patterns written to {OutputFile}");

                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Error extracting Blazor bindings: {ex.Message}");
                return false;
            }
        }

        private static Dictionary<string, string> ExtractBindingsFromFile(string filePath)
        {
            var bindings = new Dictionary<string, string>();

            if (!File.Exists(filePath))
                return bindings;

            try
            {
                var content = File.ReadAllText(filePath);
                var extractedBindings = ExtractBindingPatterns(content);

                foreach (var binding in extractedBindings)
                {
                    bindings[binding.ChangedParameterName] = binding.EventName;
                }
            }
            catch (Exception)
            {
                // Ignore file read errors for individual files
            }

            return bindings;
        }

        private static List<BindingPattern> ExtractBindingPatterns(string razorContent)
        {
            var patterns = new List<BindingPattern>();

            // Pattern 1: @bind-PropertyName:event="eventname"
            // Example: @bind-value:event="oninput"
            var bindEventPattern = new Regex(
                @"@bind-(\w+):event=[""'](\w+)[""']",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);

            var matches = bindEventPattern.Matches(razorContent);
            foreach (Match match in matches)
            {
                var propertyName = match.Groups[1].Value;
                var eventName = match.Groups[2].Value;

                // Capitalize first letter for consistency (value -> Value)
                if (!string.IsNullOrEmpty(propertyName))
                {
                    propertyName = char.ToUpper(propertyName[0]) + propertyName.Substring(1);
                    var changedParameterName = $"{propertyName}Changed";

                    patterns.Add(new BindingPattern
                    {
                        PropertyName = propertyName,
                        ChangedParameterName = changedParameterName,
                        EventName = eventName
                    });
                }
            }

            // Pattern 2: @bind-PropertyName:get and :set with :event
            // More complex pattern for future extension
            var complexBindPattern = new Regex(
                @"@bind-(\w+):get[^@]*@bind-\1:set[^@]*@bind-\1:event=[""'](\w+)[""']",
                RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline);

            var complexMatches = complexBindPattern.Matches(razorContent);
            foreach (Match match in complexMatches)
            {
                var propertyName = match.Groups[1].Value;
                var eventName = match.Groups[2].Value;

                if (!string.IsNullOrEmpty(propertyName))
                {
                    propertyName = char.ToUpper(propertyName[0]) + propertyName.Substring(1);
                    var changedParameterName = $"{propertyName}Changed";

                    // Avoid duplicates
                    if (!patterns.Any(p => p.ChangedParameterName == changedParameterName))
                    {
                        patterns.Add(new BindingPattern
                        {
                            PropertyName = propertyName,
                            ChangedParameterName = changedParameterName,
                            EventName = eventName
                        });
                    }
                }
            }

            return patterns;
        }

        private static string SerializeToJson(Dictionary<string, Dictionary<string, string>> data)
        {
            // Simple manual JSON serialization - no external dependencies
            var sb = new StringBuilder();
            sb.AppendLine("{");

            var componentIndex = 0;
            foreach (var component in data)
            {
                if (componentIndex > 0) sb.AppendLine(",");

                sb.Append($"  \"{EscapeJsonString(component.Key)}\": {{");

                var propertyIndex = 0;
                foreach (var property in component.Value)
                {
                    if (propertyIndex > 0) sb.Append(", ");
                    sb.Append($"\"{EscapeJsonString(property.Key)}\": \"{EscapeJsonString(property.Value)}\"");
                    propertyIndex++;
                }

                sb.Append("}");
                componentIndex++;
            }

            sb.AppendLine();
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string EscapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return input
                .Replace("\\", "\\\\")  // Escape backslashes
                .Replace("\"", "\\\"")  // Escape quotes
                .Replace("\n", "\\n")   // Escape newlines
                .Replace("\r", "\\r")   // Escape carriage returns
                .Replace("\t", "\\t");  // Escape tabs
        }

        private class BindingPattern
        {
            public string PropertyName { get; set; } = string.Empty;
            public string ChangedParameterName { get; set; } = string.Empty;
            public string EventName { get; set; } = string.Empty;
        }
    }
}
