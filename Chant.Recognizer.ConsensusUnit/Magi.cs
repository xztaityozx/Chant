using System.Text;
using Chant.Recognizer.ConsensusUnit.Models;
using Chant.Recognizer.Shared;
using Microsoft.Extensions.Logging;

namespace Chant.Recognizer.ConsensusUnit;

public partial class Magi
{
    private readonly ILogger<Magi> logger;
    private readonly List<IRecognizer> recognizers = new();
    private readonly Config config = Config.Default;
    private readonly Gate.Gate gate;
    private readonly YukiChant.Data yukichantData;

    public Magi(ILogger<Magi> logger, Gate.Gate gate, YukiChant.Data yukichantData)
    {
        this.logger = logger;
        this.gate = gate;
        this.yukichantData = yukichantData;
    }

    public void AddRecognizer(IRecognizer recognizer) => recognizers.Add(recognizer);

    public async Task<MagiAnswer> GetAnswerAsync(string imageFile, Direction direction)
    {
        var results = await Task.WhenAll(
            recognizers.Select(async recognizer =>
            {
                var guideResult = gate.Guide(
                    recognizer.RecognizerName,
                    await recognizer.RecognizeAsync(imageFile, direction)
                );
                return (
                    name: recognizer.RecognizerName,
                    text: guideResult.Result,
                    queue: new Queue<char>(guideResult.Result),
                    history: guideResult.ReRecognizeHistory
                );
            })
        );

        var chant = new StringBuilder();

        while (results.Any(r => r.queue.Count > 0))
        {
            // 多数決の実装はここ。それぞれの信頼度を文字に投票していく感じ
            var peeks = new Dictionary<char, decimal>();
            foreach (var (name, _, queue, _) in results)
            {
                if (!queue.Any())
                    continue;

                // 設定に信頼度がない場合は1.0とする
                if (!config.RecognizerReliability.TryGetValue(name, out var reliability))
                {
                    reliability = 1.0M;
                }

                var peek = queue.Peek();
                var point = yukichantData.Contains(peek) ? reliability : 0.0M;
                if (peeks.ContainsKey(peek))
                {
                    peeks[peek] += point;
                }
                else
                {
                    peeks.Add(peek, point);
                }
            }

            // 最多得点な文字を選んで結果に詰める
            var c = peeks.MaxBy(p => p.Value).Key;
            chant.Append(c);

            // 長さを補正してるところ
            var minLength = results.Min(r => r.queue.Count);
            foreach (var (_, _, queue, _) in results)
            {
                if (!queue.Any())
                    continue;
                if (queue.Peek() == c)
                    queue.Dequeue();
                else if (minLength <= queue.Count)
                    // `。`の位置とかでずらしたりできればいいんだけどめちゃ難しいので短いものにあわせている感じ
                    queue.Dequeue();
            }
        }

        //logger.LogDebug("Magiが多数決の結果を、誤認識テーブルと編集距離を使って誤認識を修正しています");
        //var result = gate.Guide("Magi", chant.ToString());

        return new MagiAnswer(chant.ToString(), results.Select(r => (r.name, r.text, r.history)));
    }
}
