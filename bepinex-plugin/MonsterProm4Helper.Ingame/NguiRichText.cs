using System.Text;

namespace MonsterProm4Helper.Ingame;

/// <summary>IMGUI-style tags to NGUI [b]/[color] markup.</summary>
internal static class NguiRichText
{
    public static string FromOverlayText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var sb = new StringBuilder(text.Length + 32);
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '<' && TryReadTag(text, ref i, out var tag, out var arg))
            {
                switch (tag)
                {
                    case "b":
                        sb.Append("[b]");
                        break;
                    case "/b":
                        sb.Append("[/b]");
                        break;
                    case "color":
                        if (MonoUtil.HasText(arg) && arg![0] == '#')
                            sb.Append('[').Append(arg.Substring(1)).Append(']');
                        break;
                    case "/color":
                        sb.Append("[-]");
                        break;
                }
                continue;
            }

            sb.Append(text[i]);
            i++;
        }

        return sb.ToString();
    }

    private static bool TryReadTag(string text, ref int i, out string tag, out string? arg)
    {
        tag = "";
        arg = null;
        var start = i;
        i++;
        var end = text.IndexOf('>', i);
        if (end < 0)
        {
            i = start + 1;
            return false;
        }

        var inner = text.Substring(i, end - i).Trim();
        i = end + 1;

        var eq = inner.IndexOf('=');
        if (eq >= 0)
        {
            tag = inner.Substring(0, eq).Trim().ToLowerInvariant();
            arg = inner.Substring(eq + 1).Trim();
            return true;
        }

        tag = inner.ToLowerInvariant();
        return true;
    }
}
