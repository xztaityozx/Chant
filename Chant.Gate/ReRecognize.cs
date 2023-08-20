using Chant.Gate.Models;

namespace Chant.Gate;

public partial class Gate
{
    private IEnumerable<string> GetCandidates(string original)
    {
        if (!misrecognitionTable.ContainsKey(original))
        {
            foreach (var (key, value) in misrecognitionTable)
            {
                foreach (var str in value.Where(original.Contains))
                {
                    yield return original.Replace(str, key);
                }
            }
        }

        yield return original;
    }

    // 入力を切り出す長さの順番。個数が少ない順
    private static readonly int[] SliceOrder = { 6, 5, 4, 2, 3 };

    /// <summary>
    /// 誤認識テーブルを使って入力を補正。一番それっぽい奴を探して返す
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    private ReRecognizeResult ReRecognize(string input)
    {
        if (input[0] == '。')
            return new ReRecognizeResult(0, "。", "。", "。", 1);

        var min = ReRecognizeResult.CreateMax();

        foreach (var length in SliceOrder)
        {
            if (input.Length < length)
                continue;

            var sub = input[..length];

            foreach (var candidate in GetCandidates(sub))
            {
                // 候補として上がってきた文字列が、yukichantの辞書に存在するなら補正できたとして終了
                if (yukichantData.LengthDictionary[length].Contains(candidate))
                    return new ReRecognizeResult(0, candidate, candidate, candidate, length);

                // 候補として上がってきた文字列が、yukichantの辞書に存在しないなら
                // yukichantの辞書から同じ長さの単語を取り出し、それらとのレーベンシュタイン距離を計算
                // その中で一番距離が近いもののグループを探す
                var group = yukichantData.LengthDictionary[length]
                    .Select(
                        word =>
                            new ReRecognizeResult(
                                levenshteinDistance.Walk(candidate, word),
                                word,
                                sub,
                                candidate,
                                length
                            )
                    )
                    .GroupBy(r => r.Distance)
                    .MinBy(g => g.Key);

                if (group is null)
                    continue;

                // グループの中から漢字の一致度などからどれぐらい候補として優秀かを計算し、一番いいものを選ぶ
                // 一旦グループを選んでいるのは Potential の計算コスト節約のためだけど、そんなに変わらんかも
                var result = group.Count() == 1 ? group.First() : group.MaxBy(r => r.Potential)!;

                if (result.Distance == 0)
                    return min;

                if (min.NormalizedDistance >= result.NormalizedDistance)
                    min = result;
            }
        }

        return min;
    }
}
