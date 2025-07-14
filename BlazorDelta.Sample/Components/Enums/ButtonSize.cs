namespace BlazorDelta.Sample.Components.Enums
{
    public enum ButtonSize
    {
        Small = 0,
        Medium = 1,
        Large = 2
    }


    public static class ButtonSizeExtensions
    {
        public static string ToCss(this ButtonSize size)
        {
            return size switch
            {
                ButtonSize.Small => "btn-sm",
                ButtonSize.Medium => "",
                ButtonSize.Large => "btn-lg",
                _ => ""
            };
        }
    }
}
