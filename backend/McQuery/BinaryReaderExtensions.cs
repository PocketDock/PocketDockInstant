public static class BinaryReaderExtensions
{
    public static string ReadNullTerminatedString(this System.IO.BinaryReader stream)
    {
        string str = "";
        char ch;
        while ((ch = stream.ReadChar()) != (char)0)
            str += ch;
        return str;
    }
}