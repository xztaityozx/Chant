using System.CommandLine;
using System.Diagnostics;
using Chant.Gate;
using Chant.MediaProcessor;
using Chant.Recognizer.ConsensusUnit.VMagi;
using Chant.Recognizer.Google;
using Chant.Recognizer.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

var rootCommand = new RootCommand();
var videoFilePathArgument = new Argument<FileInfo>(
    name: "videoFilePath",
    description: "動画ファイルへのパス"
);
var debugOption = new Option<bool>(
    name: "--debug",
    description: "デバッグログをONにします",
    getDefaultValue: () => true
);
var chosenCountOption = new Option<int>(
    aliases: new[] { "--number-of-choices", "-n" },
    description: "フレームの中から何枚の画像を選択するかです。",
    getDefaultValue: () => 5
);
var binarizationThresholdOption = new Option<int>(
    aliases: new[] { "--binarization-threshold", "-b" },
    description: "二値化の閾値です。ImageMagickのthresholdに渡す値です。負数にすると二値化処理をスキップします",
    getDefaultValue: () => 20000
);
var directionOption = new Option<Direction>(
    aliases: new[] { "--direction", "-d" },
    description: "縦書きか横書きかを指定します",
    getDefaultValue: () => Direction.Vertical
);
var resizeOption = new Option<bool>(
    aliases: new[] { "--resize", "-r" },
    description: "抽出したフレームをリサイズするかどうかです",
    getDefaultValue: () => false
);

rootCommand.AddArgument(videoFilePathArgument);
rootCommand.AddOption(debugOption);
rootCommand.AddOption(chosenCountOption);
rootCommand.AddOption(binarizationThresholdOption);
rootCommand.AddOption(directionOption);
rootCommand.AddOption(resizeOption);

rootCommand.SetHandler(
    async (videoFile, debug, binarizationThreshold, chosenCount, direction, resize) =>
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, _) => cancellationTokenSource.Cancel();
        var arguments = new CommandLineArguments(
            debug,
            new VMagi.RecognizeRequestParameter(
                videoFile,
                direction,
                chosenCount,
                binarizationThreshold,
                resize
            )
        );

        try
        {
            await Handler(arguments, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[red]キャンセルされました[/]");
        }
        catch (FailedToRecognizeException e)
        {
            AnsiConsole.MarkupLine("[red]認識に失敗しました[/]");
            AnsiConsole.MarkupLine($"[red]Recognizerからのメッセージは以下の通りです\n{e.Message}[/]");
            Environment.ExitCode = 1;
        }
    },
    videoFilePathArgument,
    debugOption,
    binarizationThresholdOption,
    chosenCountOption,
    directionOption,
    resizeOption
);

await rootCommand.InvokeAsync(args);

async Task Handler(CommandLineArguments arguments, CancellationToken token)
{
    token.ThrowIfCancellationRequested();

    var (debug, recognizeRequestParameter) = arguments;

    // DIの設定この辺
    // なんというかHandlerの中じゃなくて、rootCommand の組み立てと同時にやった方がいいんかな
    // 設計分からん
    var serviceCollection = new ServiceCollection();
    serviceCollection.AddLogging(builder =>
    {
        builder.AddConsole();
        if (debug)
            builder.SetMinimumLevel(LogLevel.Debug);
    });
    serviceCollection.AddSingleton<Gate>();
    serviceCollection.AddSingleton<IRecognizerFactory, VisionApiFactory>();
    serviceCollection.AddSingleton<VMagi>();
    serviceCollection.AddSingleton<Chant.YukiChant.Data>();
    serviceCollection.AddSingleton<PreProcessor>();
    serviceCollection.AddSingleton<
        Chant.YukiChant.Bridge.IYukiChantBridge,
        Chant.YukiChant.Bridge.Docker
    >();

    await using var provider = serviceCollection.BuildServiceProvider();
    var vMagi = provider.GetRequiredService<VMagi>();

    // 読み取りの処理この辺から
    var vMagiResultSummary = await vMagi.RecognizeAsync(recognizeRequestParameter, token);

    AnsiConsole.MarkupLine("[green]認識結果は以下の通りです[/]");

    if (debug)
    {
        var sorted = vMagiResultSummary.RecognizerResults.OrderBy(x => x.Frame).ToArray();

        var guideTable = new Table();
        guideTable.Title("誤認識修正", new Style(Color.Pink1, null, Decoration.Bold));
        guideTable.BorderColor(Color.Pink1);
        guideTable.AddColumns("フレーム番号", "OCRエンジンの解答", "誤認識修正後の文字列");
        foreach (var (original, guided, frame) in sorted)
        {
            try
            {
                guideTable.AddRow(
                    $"{frame}",
                    original.Replace("\n\r", "").Replace("\n", ""),
                    guided
                );
            }
            catch (InvalidOperationException)
            {
                // めっちゃフォーマットが厳しいんだよな…。
                // マークアップ中にへんなバイナリが入ると例外吐いて落ちちゃう
                // 大体の場合originalがおかしいので、そこだけ別の文字列にしておく
                guideTable.AddRow($"{frame}", "snip", guided);
            }
        }

        AnsiConsole.Write(guideTable);

        try
        {
            var consensusResultTable = new Table();
            consensusResultTable.Title(
                "VMagiによる合議内容",
                new Style(Color.Blue, null, Decoration.Bold)
            );
            consensusResultTable.BorderColor(Color.Blue);
            consensusResultTable.AddColumns("フレーム番号", "文字列");
            foreach (var (_, guided, frame) in sorted)
            {
                consensusResultTable.AddRow(
                    $"{frame}",
                    string.Join(
                        "",
                        guided.Select(
                            (c, i) =>
                                $"[{(vMagiResultSummary.Result.Length > i && c == vMagiResultSummary.Result[i] ? "green" : "red")}]{c}[/]"
                        )
                    )
                );
            }

            AnsiConsole.Write(consensusResultTable);
        }
        catch (InvalidOperationException)
        {
            // Spectre.Console の例外だけ補足
            // こっちは変なバイナリが入り込むことは少ない…はずなのであんまりここには来ないと思うんだけど
            AnsiConsole.MarkupLine("[yellow]合議結果の表示に失敗しました。呪文の読み取りは正常に完了しています。[/]");
        }
    }

    Console.WriteLine();
    AnsiConsole.Write(
        new Rule(vMagiResultSummary.Result)
        {
            Border = BoxBorder.None,
            Justification = Justify.Center,
            Style = "red bold"
        }
    );
    Console.WriteLine();

    // YukiChantに投げて解読してもらう
    var yukichant = provider.GetRequiredService<Chant.YukiChant.Bridge.IYukiChantBridge>();
    var yukichantResult = await yukichant.DecodeAsync(vMagiResultSummary.Result, token);

    if (yukichantResult.ExitCode != 0)
    {
        AnsiConsole.MarkupLine("[red]YukiChantの実行に失敗しました[/]");
        AnsiConsole.MarkupLine($"[red]YukiChantからのメッセージは以下の通りです\n{yukichantResult.Error}[/]");
        Environment.ExitCode = 1;
        return;
    }

    var command = yukichantResult.Output;
    if (string.IsNullOrEmpty(command))
    {
        AnsiConsole.MarkupLine("[red]呪文の発動をキャンセルしました。解読結果の長さが0でした[/]");
        Environment.ExitCode = 1;
        return;
    }

    if (debug)
    {
        AnsiConsole.MarkupLine($"[cyan]読み取った呪文は「{command}」です[/]");
    }

    if (!AnsiConsole.Confirm("[cyan]実行しますか？[/]"))
    {
        AnsiConsole.MarkupLine("[yellow]呪文の発動をキャンセルしました[/]");
        Environment.ExitCode = 1;
        return;
    }

    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "powershell.exe" : "bash",
            ArgumentList = { "-c", command }
        }
    };

    AnsiConsole.Write(new Rule("[bold red]以て眷属羽に喰らい。聖者無限より星の研ぎ澄ませ。[/]"));

    process.Start();
    process.WaitForExit();

    if (debug)
    {
        AnsiConsole.MarkupLine("[green]呪文の発動が完了しました[/]");
    }
}

record CommandLineArguments(bool Debug, VMagi.RecognizeRequestParameter RecognizeRequestParameter);
