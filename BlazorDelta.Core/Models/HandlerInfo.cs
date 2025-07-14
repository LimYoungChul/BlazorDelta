namespace BlazorDelta.Core.Models;

public class HandlerInfo
{
    public string ParameterName { get; set; } = "";
    public string MethodName { get; set; } = "";
    public bool IsAsync { get; set; }
}