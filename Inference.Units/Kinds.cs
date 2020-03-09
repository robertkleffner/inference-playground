namespace Inference.Units
{
    public enum Kind
    {
        Value,
        Unit
    }

    public static class KindExt
    {
        public static string Pretty(this Kind k) =>
            k switch
            {
                Kind.Unit => "#",
                Kind.Value => "*",
                _ => "?",
            };
    }
}
