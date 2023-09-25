using System.Text;
using Chant.MediaProcessor;
using Chant.Recognizer.Shared;
using Microsoft.Extensions.Logging;

namespace Chant.Recognizer.ConsensusUnit.VMagi;

public class VMagi
{
    private readonly IRecognizerFactory recognizerFactory;
    private readonly ILogger<VMagi> logger;
    private readonly PreProcessor preProcessor;
    private readonly Gate.Gate gate;

    public VMagi(
        IRecognizerFactory recognizerFactory,
        ILogger<VMagi> logger,
        Gate.Gate gate,
        PreProcessor preProcessor
    )
    {
        this.recognizerFactory = recognizerFactory;
        this.gate = gate;
        this.logger = logger;
        this.preProcessor = preProcessor;
    }

    /// <summary>
    /// 動画ファイルから取り出したフレームをそれぞれOCRし、その結果を集計して最終的な結果を返す
    /// </summary>
    /// <param name="VideoFileInfo">動画ファイルのFileInfo</param>
    /// <param name="Direction">縦書きか横書きか</param>
    /// <param name="ChosenCount">動画から何枚の画像を取り出すか。取り出すフレームはランダム</param>
    public record RecognizeRequestParameter(
        FileInfo VideoFileInfo,
        Direction Direction,
        int ChosenCount
    );

    /// <summary>
    /// VMagiの結果をまとめたやつ
    /// </summary>
    /// <param name="Result">読み取り結果の文字列</param>
    /// <param name="RecognizerResults">多数決に使ったOCRのオリジナルの読み取り結果</param>
    public record VMagiResultSummary(
        string Result,
        IEnumerable<(string Original, string Guided, int Frame)> RecognizerResults
    );

    /// <summary>
    /// 動画ファイルをOCRして結果を返す。ChosenCount枚の画像をランダムに選択してOCRし、Magiによる多数決を取った結果が返される
    /// </summary>
    /// <param name="parameter"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<VMagiResultSummary> RecognizeAsync(
        RecognizeRequestParameter parameter,
        CancellationToken token
    )
    {
        if (parameter.ChosenCount <= 0)
        {
            throw new ArgumentException("動画から取り出すべき画像が0枚だと指定されました", nameof(parameter));
        }
        logger.LogDebug("動画ファイル:{video} からフレームを取り出しています", parameter.VideoFileInfo.FullName);

        var workDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(workDir);
        logger.LogDebug("作業ディレクトリは {dir}です", workDir);

        try
        {
            await preProcessor.EnsureBuildImageMagickImage(token);

            var frames = await preProcessor.ExtractFrameImageAsync(
                parameter.VideoFileInfo,
                workDir,
                token
            );

            logger.LogDebug("フレームを取り出しました。フレーム数:{frameCount}", frames.Length);

            var selectedImages = frames.OrderBy(_ => Guid.NewGuid()).Take(parameter.ChosenCount);
            var lengthConsensus = new Dictionary<int, int>();
            var consensus = new List<Dictionary<char, int>>();

            var tasks = selectedImages.Select(image => Worker(new FileInfo(image), workDir, token));
            var consensusSources = await Task.WhenAll(tasks);

            var recognizerResults = new List<(string Original, string Guided, int Frame)>();
            foreach (var (frameNumber, recognizedText) in consensusSources)
            {
                // Gateも並列化したかったけど、参照している辞書リソースが共通なので
                // そんなに並列化の恩恵がない気がする。知らんけど
                logger.LogDebug("Chant.Gateで誤認識修正を試みます");
                var gateResult = gate.Guide("VMagi", recognizedText);
                logger.LogDebug("誤認識修正後のテキストは「{text}」です", gateResult.Result);

                var length = gateResult.Result.Length;
                // 読み取り結果の 長さ の投票をする
                if (lengthConsensus.ContainsKey(length))
                {
                    lengthConsensus[length]++;
                }
                else
                {
                    lengthConsensus.Add(length, 1);
                }

                // 各位置の文字について投票する
                // すべての読み取り結果は同じOCRエンジンを用いているので配点は同じで良い
                for (var i = 0; i < length; i++)
                {
                    if (consensus.Count <= i)
                    {
                        consensus.Add(new Dictionary<char, int>());
                    }

                    var c = gateResult.Result[i];
                    if (consensus[i].ContainsKey(c))
                    {
                        consensus[i][c]++;
                    }
                    else
                    {
                        consensus[i].Add(c, 1);
                    }
                }

                recognizerResults.Add((recognizedText, gateResult.Result, frameNumber));
            }

            // 長さの合議を取る
            var lengthConsensusResult = lengthConsensus.MaxBy(x => x.Value).Key;
            // 各位置の合議を取り、最終的な結果を組み立てていく
            var stringBuilder = new StringBuilder();
            for (var i = 0; i < lengthConsensusResult && i < consensus.Count; i++)
            {
                var c = consensus[i].MaxBy(x => x.Value).Key;
                stringBuilder.Append(c);
            }

            return new VMagiResultSummary(stringBuilder.ToString(), recognizerResults);
        }
        finally
        {
            logger.LogDebug("作業ディレクトリ {dir} を削除しています", workDir);
            Directory.Delete(workDir, true);
            logger.LogDebug("作業ディレクトリ {dir} を削除しました", workDir);
        }
    }

    private async Task<(int FrameNumber, string RecognizedText)> Worker(
        FileSystemInfo imageFileInfo,
        string workDir,
        CancellationToken token
    )
    {
        if (token.IsCancellationRequested)
        {
            logger.LogWarning("キャンセルがリクエストされていたので、読み取りを中断しています");
            return default;
        }
        var parseResult = int.TryParse(
            Path.GetFileNameWithoutExtension(imageFileInfo.Name),
            out var frameNumber
        );

        if (!parseResult)
        {
            logger.LogWarning(
                "フレーム番号がファイル名: {frame} から読み取れませんでした。このファイルは無視されます",
                imageFileInfo.FullName
            );
            return default;
        }
        logger.LogDebug(
            "画像ファイル: {image} からフレーム番号: {frame} を取り出しました",
            imageFileInfo.FullName,
            frameNumber
        );

        logger.LogDebug("画像ファイルの二値化を開始しています");
        var binarizedImage = await preProcessor.BinarizeAsync(
            imageFileInfo.FullName,
            workDir,
            20000,
            token
        );
        logger.LogDebug("画像ファイルの二値化が完了しました。出力ファイル: {image}", binarizedImage);

        var recognizer = recognizerFactory.Create();
        logger.LogDebug("使用するOCRエンジンは {r} です", recognizer.RecognizerName);
        logger.LogDebug("フレーム番号 {f} を {r} で読み取っています", frameNumber, recognizer.RecognizerName);
        var recognizedText = await recognizer.RecognizeAsync(binarizedImage, Direction.Vertical);
        logger.LogDebug("フレーム番号 {f} の 読み取り結果は「{r}」でした", frameNumber, recognizedText);

        return (frameNumber, recognizedText);
    }
}
