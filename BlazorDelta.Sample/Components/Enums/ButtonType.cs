namespace BlazorDelta.Sample.Components.Enums
{
    public enum ButtonType
    {
        Button = 0,
        Submit = 1,
        Reset = 2
    }


    public static class ButtonTypeExtensions
    {
        public static string ToValue(this ButtonType buttonType)
        {
            return buttonType switch
            {
                ButtonType.Button => "button",
                ButtonType.Submit => "submit",
                ButtonType.Reset => "reset",
                _ => "button"
            };
        }
    }

}
