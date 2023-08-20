using System.Text;
using System.Text.Json;
using Chant.Gate.Models;
using Levenshtein;
using Microsoft.Extensions.Logging;

namespace Chant.Gate;

public partial class Gate
{
    private readonly ILogger<Gate> logger;
    private readonly LevenshteinDistance levenshteinDistance = new();
    private readonly YukiChant.Data yukichantData = new();
    private readonly Dictionary<string, string[]> misrecognitionTable;

    public Gate(ILogger<Gate> logger)
    {
        this.logger = logger;

        using var stream = new StreamReader(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "assets",
                "misrecognition-table.json"
            )
        );
        misrecognitionTable =
            JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream.BaseStream)
            ?? throw new FileLoadException("misrecognition-table.json が読み込めませんでした");
    }

    /// <summary>
    /// 誤認識テーブルとレーベンシュタイン距離を使って入力を補正して返す
    /// </summary>
    /// <param name="input">補正したい文字列</param>
    /// <param name="initiator">呼び出し元の名前。ログ用なのでなんでもいい</param>
    /// <returns></returns>
    public GuideResult Guide(string input, string initiator = "none")
    {
        input = PreProcess(input);
        var original = input;

        logger.LogDebug("Chant.Gate は {initiator} から「{input}」を受け取っています", initiator, input);

        var history = new List<ReRecognizeResult>();
        var sb = new StringBuilder();
        while (input.Length > 0)
        {
            var result = ReRecognize(input);
            history.Add(result);

            // 補正候補が見つからなかった場合。int.MaxValue だとマジックナンバーっぽさある
            if (result.Distance == int.MaxValue)
            {
                logger.LogDebug("{initiator}からの修正リクエストが失敗しました。中断して元の文字列で続行します。", initiator);
                sb.Clear();
                sb.Append(original);
                break;
            }

            input = input[result.Consumed..];
            sb.Append(result.Result);
        }

        return new GuideResult(original, sb.ToString(), history);
    }
}
