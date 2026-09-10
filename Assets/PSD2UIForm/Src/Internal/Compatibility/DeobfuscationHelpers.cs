internal static class __DeobHelpers
{
    public static uint ComputeStringHash(string s)
    {
        uint num = 2166136261u;
        if (s != null)
        {
            for (int i = 0; i < s.Length; i++)
            {
                num = (s[i] ^ num) * 16777619;
            }
        }
        return num;
    }
}
