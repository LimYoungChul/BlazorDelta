using Microsoft.CodeAnalysis;

namespace BlazorDelta.Core.Models;

public class ParameterInfo
{
    public string Name { get; set; } = "";
    public ITypeSymbol Type { get; set; } = null!;
    public bool HasSetOnce { get; set; }
    public bool HasNoRender { get; set; }
    public bool HasUpdatesCss { get; set; }
    public bool CaptureUnmatchedValues { get; set; }
    public bool IsCascading { get; set; }
}
