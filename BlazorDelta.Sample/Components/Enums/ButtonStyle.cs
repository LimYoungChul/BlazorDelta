namespace BlazorDelta.Sample.Components.Enums
{
    public enum ButtonStyle
    {
        Filled = 0,
        Outline = 1,
        Text = 2
    }

    public static class ButtonStyleExtensions
    {
        public static string ToCss(this ButtonStyle size)
        {
            return size switch
            {
                ButtonStyle.Filled => string.Empty,
                ButtonStyle.Outline => "-outline",
                ButtonStyle.Text => string.Empty,
                _ => string.Empty
            };
        }
    }

}
