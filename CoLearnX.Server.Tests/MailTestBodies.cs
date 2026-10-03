using System.Text;
using System.Text.RegularExpressions;

namespace CoLearnX.Server.Tests;

// Decode the captured wire representation before asserting user-visible text.
internal static class MailTestBodies
{
    internal static string Decode(string eml)
        => string.Join("\n", Regex.Matches(eml,
            @"Content-Transfer-Encoding:\s*(\S+)\r?\n\r?\n([\s\S]*?)(?=\r?\n--|\z)",
            RegexOptions.IgnoreCase).Select(match =>
            {
                var body = match.Groups[2].Value;
                return match.Groups[1].Value.ToLowerInvariant() switch
                {
                    "base64" => Encoding.UTF8.GetString(Convert.FromBase64String(body)),
                    "quoted-printable" => DecodeQuotedPrintable(body),
                    _ => body,
                };
            }));

    private static string DecodeQuotedPrintable(string value)
    {
        value = value.Replace("=\r\n", "").Replace("=\n", "");
        var bytes = new List<byte>();
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '=' && index + 2 < value.Length
                && byte.TryParse(value.AsSpan(index + 1, 2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out var escaped))
            { bytes.Add(escaped); index += 2; }
            else bytes.Add((byte)value[index]);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }
}
