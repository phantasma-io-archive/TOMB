namespace Phantasma.Tomb.Compiler
{
    public static class Extensions
    {
        public static string UppercaseFirst(this string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            char[] a = s.ToCharArray();
            a[0] = char.ToUpper(a[0]);
            return new string(a);
        }
        public static bool IsLogicalOperator(this OperatorKind op)
        {
            return op != OperatorKind.Unknown && op < OperatorKind.Addition;
        }
    }
}
