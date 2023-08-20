using System.Text.RegularExpressions;

namespace Chant.Gate.Models;

public partial record ReRecognizeResult(
    int Distance,
    string Result,
    string Original,
    string FixMisrecognitionResult,
    int Consumed
)
{
    internal static ReRecognizeResult CreateMax() => new(int.MaxValue, "", "", "", 0);

    [GeneratedRegex(@"[ぁ-ん]")]
    private partial Regex HiraganaRegex();

    /// <summary>
    /// 文字列の長さで距離を割った値。こうやって正規化しておくと、文字の長さによる影響が少なくなる
    /// </summary>
    public decimal NormalizedDistance => Consumed == 0 ? Distance : Distance / (decimal)Consumed;

    private static int GetDeltaSum(IEnumerable<char> a, IEnumerable<char> b, int delta) =>
        a.Zip(b, (x, y) => x == y ? delta : 0).Sum();

    private (char[], char[]) GetHanAndHiraganaList(string str)
    {
        var dict = str.GroupBy(c => HiraganaRegex().IsMatch($"{c}"))
            .ToDictionary(g => g.Key, g => g.ToArray());

        dict.TryGetValue(false, out var han);
        dict.TryGetValue(true, out var hiragana);

        return (han ?? Array.Empty<char>(), hiragana ?? Array.Empty<char>());
    }

    public decimal Potential
    {
        get
        {
            var original = GetHanAndHiraganaList(Original);
            var result = GetHanAndHiraganaList(Result);

            return NormalizedDistance
                // 漢字の一致はコスト2で計算する。値はテキトー
                + GetDeltaSum(original.Item1, result.Item1, 2)
                // ひらがなの一致はコスト1で計算する。漢字の一致よりは低くしたいため、2以下ならなんでもいい
                + GetDeltaSum(original.Item2, result.Item2, 1);
        }
    }
}
