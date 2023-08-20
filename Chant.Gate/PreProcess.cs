using System.Text.RegularExpressions;

namespace Chant.Gate;

public partial class Gate
{
    private static readonly string[] IgnoredStrings =
    {
        "「",
        "」",
        "【",
        "】",
        "）",
        "（",
        " ",
        "、",
        "１",
        "２",
        "３",
        "４",
        "５",
        "６",
        "７",
        "８",
        "９",
        "０",
    };

    [GeneratedRegex(@"[\x21-\x7e\s]")]
    private partial Regex AsciiRegex();

    private string PreProcess(string input)
    {
        input = input.Trim();
        input = IgnoredStrings.Aggregate(input, (current, s) => current.Replace(s, string.Empty));
        input = input.Replace(".", "。").Replace(Environment.NewLine, "");
        input = AsciiRegex().Replace(input, "");

        return input.Last() == '。' ? input : input + "。";
    }
}
