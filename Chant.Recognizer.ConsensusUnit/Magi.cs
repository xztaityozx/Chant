using System.Collections;
using System.Text;
using Chant.Gate.Models;
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

    public async Task<MagiAnswer> GetAnswerFromRecognizeResultTextList(IEnumerable<(string RecognizerName, string Result)> recognizeResultList) {
        var guideResults = await Task.WhenAll(recognizeResultList.Select(
            r => {
                return Task.Run(() => {
                    var g = gate.Guide(r.RecognizerName, r.Result);
                    return (r.RecognizerName, Text: r.Result, Queue: new Queue<char>(g.Result),
                        History: g.ReRecognizeHistory);
                });
            })
        );

        return GetAnswer(guideResults);
    }

    private MagiAnswer GetAnswer(IReadOnlyList<(string RecognizerName, string Text, Queue<char> Queue, IEnumerable<ReRecognizeResult> Histroy)> guideResults) {
        var chant = new StringBuilder();

        while(guideResults.Any(r => r.Queue.Count > 0))
        {
            // 多数決の実装
            var peeks = new Dictionary<char, decimal>();
            foreach (var (name, _, queue, _) in guideResults) {
                if(queue.Count == 0) continue;

                // 設定からそのリコグナイザーの信頼度を取ってくる。ない場合は1.0とする
                if(!config.RecognizerReliability.TryGetValue(name, out var reliability)) {
                    reliability = 1.0M;
                }

                var peek = queue.Peek();
                var point = yukichantData.Contains(peek) ? reliability : peek == '。' ? reliability : 0.0M;
                if(peeks.TryGetValue(peek, out var currentPoint))
                {
                    peeks[peek] = currentPoint + point;
                } else {
                    peeks.Add(peek, point);
                }
            }

            // 最多得点な文字を選んで結果に詰める
            var c = peeks.MaxBy(p => p.Value).Key;
            chant.Append(c);

            // 長さを補正してるところ
            // 長さでグルーピングして、一番多い長さを残りの文字数として設定
            var minLength = guideResults.Select(r => r.Queue.Count).GroupBy(i => i)
                .MaxBy(i => i.Count())?.Key ?? 0;

            foreach(var (_, _, queue, _) in guideResults)
            {
                if(queue.Count == 0) continue;
                if(queue.Peek() == c) {
                    queue.Dequeue();
                    continue;
                }
                if(queue.Count == minLength)
                {
                    queue.Dequeue();
                    continue;
                }

                // 残りの文字数より多い
                while(queue.Count > 0)
                {
                    queue.Dequeue();
                    if (queue.Peek() != c && queue.Count != minLength) continue;
                    queue.Dequeue();
                    break;
                }
            }
        }

        return new MagiAnswer(
            chant.ToString(),
            guideResults.Select(
                r => (r.RecognizerName, r.Text, r.Histroy)
            )
        );
    }

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

        return GetAnswer(results);
    }
}
