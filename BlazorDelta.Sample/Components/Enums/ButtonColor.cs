namespace BlazorDelta.Sample.Components.Enums
{
    public enum ButtonColor
    {
        Primary = 0,
        Secondary = 1,
        Info = 2,
        Success = 3,
        Warning = 4,
        Light = 5,
        Danger = 6,
        Dark = 7
    }

    public static class ButtonColorExtensions
    {
        public static string ToCss(this ButtonColor size)
        {
            return size switch
            {
                ButtonColor.Primary => "-primary",
                ButtonColor.Secondary => "-secondary",
                ButtonColor.Info => "-info",
                ButtonColor.Success => "-success",
                ButtonColor.Warning => "-warning",
                ButtonColor.Danger => "-danger",
                ButtonColor.Light => "-light",
                ButtonColor.Dark => "-dark",
                _ => "-primary"
            };
        }
    }

}
