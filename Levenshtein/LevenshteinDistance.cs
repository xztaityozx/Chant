namespace Levenshtein;

/// <summary>
/// レーベンシュタイン距離を計算するやつ
/// </summary>
/// <param name="ReplaceCost">置換にかかるコスト</param>
public record LevenshteinDistance(int ReplaceCost = 1)
{
    /// <summary>
    /// 二つの文字列のレーベンシュタイン距離を計算して返す
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public int Walk(string a, string b)
    {
        var aLength = a.Length;
        var bLength = b.Length;

        var dp = new List<int[]>();
        for (var i = 0; i < aLength + 1; i++)
            dp.Add(new int[bLength + 1]);

        for (var i = 0; i < aLength + 1; i++)
            dp[i][0] = i;
        for (var i = 0; i < bLength + 1; i++)
            dp[0][i] = i;

        for (var i = 1; i <= aLength; i++)
        {
            for (var k = 1; k <= bLength; k++)
            {
                var cost = ReplaceCost;
                if (a[i - 1] == b[k - 1])
                    cost = 0;

                dp[i][k] = new[]
                {
                    dp[i - 1][k] + 1,
                    dp[i][k - 1] + 1,
                    dp[i - 1][k - 1] + cost
                }.Min();
            }
        }

        return dp[aLength][bLength];
    }
}
