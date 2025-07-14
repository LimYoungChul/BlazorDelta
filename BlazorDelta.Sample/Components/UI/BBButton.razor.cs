using BlazorDelta.Abstractions;
using BlazorDelta.Attributes;
using BlazorDelta.Sample.Components.Enums;
using Microsoft.AspNetCore.Components;

namespace BlazorDelta.Sample.Components.UI
{
    public partial class BBButton : DeltaComponentBase
    {


        [Parameter]
        public string? Text { get; set; }

        [Parameter]
        public string? Icon { get; set; }

        [Parameter, UpdatesCss]
        public ButtonColor Color { get; set; } = ButtonColor.Primary;

        [Parameter, UpdatesCss]
        public ButtonStyle Style { get; set; } = ButtonStyle.Filled;

        [Parameter, UpdatesCss]
        public ButtonSize Size { get; set; } = ButtonSize.Medium;

        [Parameter]
        public EventCallback OnClick { get; set; }

        [Parameter, UpdatesCss]
        public bool Disabled { get; set; }

        [Parameter]
        public ButtonType Type { get; set; } = ButtonType.Button;

        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object?>? Attributes { get; set; }

        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        private string? _btnType;

        [OnParameterChanged(nameof(Type))]
        public void SetButtonType() => _btnType = Type.ToValue();

        protected override void OnInitialized()
        {
            base.OnInitialized();
            _btnType = Type.ToValue();
        }

        private string? _btnClass = string.Empty;
        protected override void UpdateCssClasses()
        {
            _btnClass = $"btn {Size.ToCss()} btn{Style.ToCss()}{Color.ToCss()}";
        }

    }
}