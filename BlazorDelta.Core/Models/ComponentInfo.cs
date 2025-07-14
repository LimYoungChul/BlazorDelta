using System.Collections.Generic;

namespace BlazorDelta.Core.Models;

public class ComponentInfo
{
    public string ClassName { get; set; } = "";
    public string Namespace { get; set; } = "";
    public List<ParameterInfo> Parameters { get; set; } = new();
    public List<HandlerInfo> Handlers { get; set; } = new();
    public ParameterInfo? CaptureUnmatchedValuesParameter { get; set; }
}
