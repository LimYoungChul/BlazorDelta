using BlazorDelta.Sample.Components.BaseComponents;
using Microsoft.AspNetCore.Components;

namespace BlazorDelta.Sample.Components.FormComponents
{
    public partial class QuickText : FormComponent<string?>
    {
        [Parameter]
        public int? MaxLength { get; set; } 

    }
}