using System.Globalization;
using System.Text;

namespace CoLearnX.Server.Domain;

public static class ApplicantStatementRules
{
    public const int MaxWords = 300;
    public const int MaxChars = 2400;

    public static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var count = 0;
        var inLatin = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsHan(rune))
            {
                if (inLatin)
                {
                    count++;
                    inLatin = false;
                }

                count++;
            }
            else if (Rune.IsWhiteSpace(rune))
            {
                if (inLatin)
                {
                    count++;
                    inLatin = false;
                }
            }
            else if (IsWordChar(rune))
            {
                inLatin = true;
            }
        }

        if (inLatin)
            count++;

        return count;
    }

    public static string Normalize(string? text)
        => string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();

    static bool IsHan(Rune rune)
    {
        var value = rune.Value;
        return value is >= 0x3400 and <= 0x4DBF
            or >= 0x4E00 and <= 0x9FFF
            or >= 0xF900 and <= 0xFAFF
            or >= 0x20000 and <= 0x2CEAF;
    }

    static bool IsWordChar(Rune rune)
        => Rune.GetUnicodeCategory(rune) is UnicodeCategory.LowercaseLetter
            or UnicodeCategory.UppercaseLetter
            or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.OtherLetter
            or UnicodeCategory.DecimalDigitNumber;
}
