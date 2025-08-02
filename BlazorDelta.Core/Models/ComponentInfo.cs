using System.Collections.Generic;

namespace BlazorDelta.Core.Models;

public class ComponentInfo
{
    public string ClassName { get; set; } = string.Empty;
    public string ClassConstraints { get; set; } = string.Empty; // ADD THIS
    public string Namespace { get; set; } = string.Empty;
    public List<ParameterInfo> Parameters { get; set; } = new();
    public List<HandlerInfo> Handlers { get; set; } = new();
    public ParameterInfo? CaptureUnmatchedValuesParameter { get; set; }
}
