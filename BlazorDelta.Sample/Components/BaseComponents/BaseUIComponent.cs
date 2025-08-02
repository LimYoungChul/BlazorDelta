using BlazorDelta.Abstractions;
using BlazorDelta.Attributes;
using BlazorDelta.Sample.Helpers;
using Microsoft.AspNetCore.Components;

namespace BlazorDelta.Sample.Components.BaseComponents
{
    public partial class BaseUIComponent : DeltaComponentBase
    {
        [Parameter, SetOnce, NoRender]
        public string Id { get; set; } = Generator.GenrateId();

        [Parameter, UpdatesCss]
        public string? Class { get; set; }

        [Parameter]
        public string? Style { get; set; }

        [Parameter(CaptureUnmatchedValues = true)]
        public Dictionary<string, object>? AdditionalAttributes { get; set; }

    }
}
