using System.CommandLine;
using System.Data;
using System.Diagnostics;
using System.Runtime.Serialization;
using Chant.Gate;
using Chant.Recognizer.ConsensusUnit;
using Chant.Recognizer.Google;
using Chant.Recognizer.Shared;
using Chant.Recognizer.Tesseract;
using Chant.Recognizer.Windows;
using Chant.YukiChant.Bridge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

var rootCommand = new RootCommand();

var imageFilePathArgument = new Argument<FileInfo>(
    name: "chantPath",
    description: "呪文が書かれたファイルへのパス"
);

var debugOption = new Option<bool>(
    name: "--debug",
    description: "デバッグログをONにします",
    getDefaultValue: () => false
);

var verboseOption = new Option<bool>(
    name: "--verbose",
    description: "変換過程などの詳細ログをONにします",
    getDefaultValue: () => false
);

var ocrEngineListOption = new Option<OcrEngine[]>(
    name: "--ocr-engines",
    description: "使用するOCRエンジンを指定します。カンマ区切りです",
    getDefaultValue: () => new[] { OcrEngine.Tesseract, OcrEngine.Windows }
);

rootCommand.AddArgument(imageFilePathArgument);
rootCommand.AddOption(debugOption);
rootCommand.AddOption(verboseOption);
rootCommand.AddOption(ocrEngineListOption);

rootCommand.SetHandler(
    async (imageFile, ocrEngines, debug, verbose) =>
    {
        try
        {
            await Handler(imageFile, ocrEngines, debug, verbose);
        }
        catch (FailedToRecognizeException e)
        {
            AnsiConsole.MarkupLine("[red]Recognizerがエラーを返しました[/]");
            AnsiConsole.MarkupLine($"[red]{e.OriginalError}[/]");
        }
    },
    imageFilePathArgument,
    ocrEngineListOption,
    debugOption,
    verboseOption
);

await rootCommand.InvokeAsync(args);

async Task Handler(FileInfo imageFile, OcrEngine[] ocrEngines, bool debug, bool verbose)
{
    var colorMap = new Dictionary<string, Color>
    {
        ["Tesseract"] = Color.Green,
        ["WindowsOcr"] = Color.Blue,
        ["VisionApi"] = Color.Red,
        ["Magi"] = Color.Pink1
    };
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddTransient<LaunchConfig>(
        _ =>
            new LaunchConfig(
                OperatingSystem.IsWindows()
                    ? Path.Join(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Programs",
                        "Tesseract-OCR",
                        "tesseract.exe"
                    )
                    // Unixはまぁパス通ってるでしょｗ
                    : "tesseract"
            )
    );

    serviceCollection.AddSingleton<IYukiChantBridge, Chant.YukiChant.Bridge.Docker>();
    serviceCollection.AddSingleton<Gate>();
    serviceCollection.AddSingleton<Chant.YukiChant.Data>();
    serviceCollection.AddSingleton<Tesseract>();
    serviceCollection.AddSingleton<Magi>();
    serviceCollection.AddSingleton<Native>();
    serviceCollection.AddSingleton<VisionApi>();
    serviceCollection.AddLogging(configure =>
    {
        configure.AddConsole();
        configure.SetMinimumLevel(debug ? LogLevel.Debug : LogLevel.Error);
    });
    await using var provider = serviceCollection.BuildServiceProvider();
    var logger = provider.GetService<ILogger<Program>>() ?? throw new NoNullAllowedException();

    var magi = provider.GetService<Magi>() ?? throw new NoNullAllowedException();
    foreach (var ocrEngine in ocrEngines)
    {
        magi.AddRecognizer(
            ocrEngine switch
            {
                OcrEngine.Tesseract
                    => provider.GetService<Tesseract>() ?? throw new NoNullAllowedException(),
                OcrEngine.Windows
                    => provider.GetService<Native>() ?? throw new NoNullAllowedException(),
                OcrEngine.VisionApi
                    => provider.GetService<VisionApi>() ?? throw new NoNullAllowedException(),
                _ => throw new UnknownOcrEngineException()
            }
        );
    }

    AnsiConsole.MarkupLine("[bold purple]烙印を凍結彼方に過ぎ去り。猛撃守護を熱風とともに授かり。女神の心ば極め。[/]");
    logger.LogDebug("OCRエンジンは{engines}です", string.Join(",", ocrEngines));
    AnsiConsole.MarkupLine("[bold purple]獣と五感揺らぎ借り。万象を閃光とともに熱を授かり。女神の心眼を極め。[/]");

    var magiAnswer = await magi.GetAnswerAsync(imageFile.FullName);
    if (verbose)
    {
        AnsiConsole.MarkupLine(
            $"""
[pink1]Magiからの解答は以下の通りです
--------------------------------
{magiAnswer.Text}
--------------------------------
[/]
"""
        );

        var consensusTable = new Table();
        consensusTable.AddColumns("OCRエンジン", "出力");
        consensusTable.BorderColor(colorMap["Magi"]);
        foreach (var (recognizerName, result, history) in magiAnswer.RecognizerResults)
        {
            var hist = history.ToArray();
            AnsiConsole.MarkupLine(
                $"[{colorMap[recognizerName]}]{recognizerName}は「{string.Join("", hist.Select(h => h.Original))}」だと言っていました。[/]"
            );
            AnsiConsole.MarkupLine(
                $"[{colorMap[recognizerName]}]Chant.Gateで誤認識修正を行い、「{result}」に修正されました[/]"
            );
            var table = new Table();
            table.Title(recognizerName, colorMap[recognizerName]);
            table.BorderColor(colorMap[recognizerName]);
            table.AddColumns("元の文字列", "誤認識修正後", "編集距離での修正後", "編集距離", "編集距離(正規化)", "正確度");
            foreach (var item in hist)
            {
                table.AddRow(
                    item.Original,
                    item.FixMisrecognitionResult,
                    item.Result,
                    $"{item.Distance}",
                    $"{item.NormalizedDistance}",
                    $"{item.Potential}"
                );
            }
            AnsiConsole.Write(table);
            consensusTable.AddRow(
                recognizerName,
                string.Join(
                    "",
                    result
                        .Zip(magiAnswer.Text)
                        .Select(
                            pair =>
                                $"[{(pair.First == pair.Second ? "green" : "red")}]{pair.First}[/]"
                        )
                )
            );
        }

        AnsiConsole.MarkupLine("[pink1]Magiは以下のような多数決で呪文を復元しています。[/]");
        AnsiConsole.Write(consensusTable);
        AnsiConsole.MarkupLine(
            "[bold purple]輪廻に時を恵みを振り。震天動地の使いよ呼吸住まう。消し集結人を降ろさ。虚空蔵の戒めに声聞か轟け。螺鈿刻印を眷属照らし。呼吸希望を巨人よ散れ。幻を沈み禊にて駆ける。静粛の非力今一輝く。灰塵と雲壌を森に裁か。狼の英姿火を消え去ら。龍と誘いより解き放ち。[/]"
        );

        if (magiAnswer.Text.Length < 3)
        {
            // 3文字以下にはならないはず…
            logger.LogCritical("Magiは3文字以下の文字列「{text}」を返しました", magiAnswer.Text);
            AnsiConsole.MarkupLine("[red]読み取った呪文が3文字未満でした[/]");
            Environment.Exit(1);
        }

        AnsiConsole.MarkupLine("[bold purple]自由五月雨に揺の帰ら。螺旋の閃光よ螺旋の散る。死を作れ。[/]");

        // ただのパディングです…
        Console.WriteLine();
        AnsiConsole.Write(
            new Spectre.Console.Rule(magiAnswer.Text)
            {
                Border = BoxBorder.None,
                Justification = Justify.Center,
                Style = "red bold"
            }
        );
        Console.WriteLine();

        var yukichantBridge =
            provider.GetService<IYukiChantBridge>() ?? throw new NoNullAllowedException();
        var (exitCode, _, cmd, error) = await yukichantBridge.DecodeAsync(magiAnswer.Text);

        if (exitCode == 0)
            logger.LogDebug("呪文をyukichantでデコードした結果「{text}」が返却されました", cmd);
        else
        {
            logger.LogError("yukichantがエラーを返しました。終了ステータス:{status}: {error}", exitCode, error);
            AnsiConsole.MarkupLine("[red]呪文の解析に失敗しました(yukichantがエラーでした)[/]");
            Environment.Exit(1);
        }

        if (string.IsNullOrEmpty(cmd))
        {
            AnsiConsole.MarkupLine("[red]呪文の解析に失敗しました(解析結果が空文字でした)[/]");
            Environment.Exit(1);
        }

        if (!AnsiConsole.Confirm("実行しますか？"))
        {
            AnsiConsole.MarkupLine("[red]呪文の発動がキャンセルされました[/]");
            Environment.Exit(1);
        }

        AnsiConsole.Write(new Spectre.Console.Rule("[bold red]以て眷属羽に喰らい。聖者無限より星の研ぎ澄ませ。[/]"));

        var processStartInfo = new ProcessStartInfo("powershell.exe")
        {
            ArgumentList = { "-c", cmd }
        };

        var process = new Process { StartInfo = processStartInfo };
        process.Start();

        process.WaitForExit();
    }
}

enum OcrEngine
{
    Tesseract,
    VisionApi,
    Windows
}

[Serializable]
public class UnknownOcrEngineException : Exception
{
    public UnknownOcrEngineException() { }

    public UnknownOcrEngineException(string? message) : base(message) { }

    public UnknownOcrEngineException(string? message, Exception? innerException)
        : base(message, innerException) { }

    protected UnknownOcrEngineException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
