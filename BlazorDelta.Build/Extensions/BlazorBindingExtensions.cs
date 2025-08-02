using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace BlazorDelta.Build.Extensions
{
    public static class BlazorBindingExtensions
    {
        /// <summary>
        /// Helper method to extract bindings without MSBuild (for testing)
        /// </summary>
        public static Dictionary<string, Dictionary<string, string>> ExtractBindingsFromDirectory(string razorDirectory)
        {
            var allBindings = new Dictionary<string, Dictionary<string, string>>();

            if (!Directory.Exists(razorDirectory))
                return allBindings;

            var razorFiles = Directory.GetFiles(razorDirectory, "*.razor", SearchOption.AllDirectories);

            foreach (var filePath in razorFiles)
            {
                var componentName = Path.GetFileNameWithoutExtension(filePath);
                var content = File.ReadAllText(filePath);

                var bindEventPattern = new Regex(
                    @"@bind-(\w+):event=[""'](\w+)[""']",
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);

                var matches = bindEventPattern.Matches(content);
                var bindings = new Dictionary<string, string>();

                foreach (Match match in matches)
                {
                    var propertyName = match.Groups[1].Value;
                    var eventName = match.Groups[2].Value;

                    if (!string.IsNullOrEmpty(propertyName))
                    {
                        propertyName = char.ToUpper(propertyName[0]) + propertyName.Substring(1);
                        var changedParameterName = $"{propertyName}Changed";
                        bindings[changedParameterName] = eventName;
                    }
                }

                if (bindings.Any())
                {
                    allBindings[componentName] = bindings;
                }
            }

            return allBindings;
        }
    }
}