namespace BlazorDelta.Sample.Helpers
{
    public static class Generator
    {
        public static string GenrateId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}
