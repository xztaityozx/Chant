using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Chant.Recognizer.Shared;
using Microsoft.Extensions.Logging;

namespace Chant.Recognizer.Tesseract;

/// <summary>
/// TesseractによるOCRを実行するクラス
/// </summary>
public class Tesseract : IRecognizer
{
    private readonly ILogger<Tesseract> logger;
    private readonly LaunchConfig launchConfig;

    public Tesseract(ILogger<Tesseract> logger, LaunchConfig launchConfig)
    {
        this.logger = logger;
        this.launchConfig = launchConfig;
    }

    /// <summary>
    /// TesseractによるOCRを実行して、読み取った文字列を返す。
    /// </summary>
    /// <param name="filePath">画像ファイルへのパス。実行パスからの相対パスでも絶対パスでも良い</param>
    /// <param name="direction">つかってないです。方向指定はLaunchConfigのPsmパラメーターで行います</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">LaunchConfigが有効でないときに投げられる</exception>
    /// <exception cref="FailedToRecognizeException">Tesseractの終了コードが0以外だと投げられる</exception>
    public async Task<string> RecognizeAsync(string filePath, Direction direction)
    {
        if (launchConfig.Status.IsInvalid)
        {
            throw new InvalidOperationException(launchConfig.Status.ErrorMessage);
        }

        var process = new Process
        {
            StartInfo = new ProcessStartInfo(launchConfig.ExecutionFilePath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                ArgumentList =
                {
                    filePath,
                    "-",
                    "-l",
                    launchConfig.Language,
                    "--psm",
                    $"{launchConfig.Psm}"
                },
                WorkingDirectory = Directory.GetCurrentDirectory()
            }
        };

        logger.LogDebug("ExecutionFilePath: {path}", launchConfig.ExecutionFilePath);
        logger.LogDebug("Language: {language}", launchConfig.Language);
        logger.LogDebug("Psm: {psm}", launchConfig.Psm);
        logger.LogDebug("WorkingDirectory: {path}", process.StartInfo.WorkingDirectory);
        logger.LogDebug(
            "Argument List: {argument}",
            JsonSerializer.Serialize(process.StartInfo.ArgumentList)
        );

        logger.LogDebug("Start Tesseract process");
        process.Start();
        await process.WaitForExitAsync();
        logger.LogDebug("Tesseract process exited. exit code: {code}", process.ExitCode);

        if (process.ExitCode != 0)
        {
            throw new FailedToRecognizeException(await process.StandardError.ReadToEndAsync());
        }

        return await process.StandardOutput.ReadToEndAsync();
    }

    public string RecognizerName => "Tesseract";
}

public record LaunchConfig(string ExecutionFilePath, string Language = "jpn", int Psm = 6)
{
    /// <summary>
    /// このコンフィグが有効かどうかを取得して返す。有効でない場合はエラーメッセージも返す。
    /// </summary>
    public (bool IsInvalid, string ErrorMessage) Status
    {
        get
        {
            // 実行ファイルが存在しないんじゃなんも実行できないので…
            if (!File.Exists(ExecutionFilePath))
            {
                return (true, "ExecutionFilePath is not exists");
            }

            // psmの範囲は0~13
            if (Psm is < 0 or > 13)
            {
                return (true, "Psm is invalid");
            }

            // 指定言語が空文字列だとTesseractが起動できない。正直jpn固定でいい
            return string.IsNullOrEmpty(Language)
                ? (true, "Language is null or empty")
                : (false, "");
        }
    }
}
