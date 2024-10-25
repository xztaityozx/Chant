using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Chant.Gate;
using Chant.ImageProcessor.Code;
using Chant.Recognizer.Google;
using Chant.Recognizer.Shared;
using Chant.YukiChant.Bridge;
using Cocona;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

var appBuilder = CoconaApp.CreateBuilder();
appBuilder.Services.AddLogging(
    builder => {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Information);
    });
appBuilder.Services.AddTransient<Gate>();
appBuilder.Services.AddTransient<Chant.YukiChant.Data>();
appBuilder.Services.AddTransient<Chant.Recognizer.ConsensusUnit.Magi>();
appBuilder.Services.AddTransient<IYukiChantBridge, Chant.YukiChant.Bridge.Docker>();

var app = appBuilder.Build();

app.Run( async ([Option] string inputFile, [Option] bool debug = false, [Option] double startAngle = -100.0d) => {
    var logger = app.Services.GetService<ILogger<Program>>() ?? throw new NoNullAllowedException();

    var gaussianBlurSizeList = new List<(double W, double H)> {
        (5, 5),
        (15, 15),
        (25, 25),
        (35, 35),
        (45, 45),
    };

    try {
        var recognizeResultList = new List<(string RecognizerName, string Result)>();

        AnsiConsole.MarkupLine("[bold purple]者の姿へ炎は喰らい。理に従い破壊の呼吸住まう。[/]");
        AnsiConsole.MarkupLine("[bold purple]破断秘術を万物に還る。強欲の前を底より下り。彼らを祈り暴威を借り。女の指を鉤爪で傷つき。[/]");
        AnsiConsole.MarkupLine("[bold purple]守護を刃の再生を語れ。王が平穏の今こそ裁く。入相の鐘の脈に彼方に休め。音の加護を手の歌え。[/]");
        AnsiConsole.MarkupLine("[bold purple]刀に断罪の星を刻み込め。狭霧姫君よ石の示さ。刀に触れる。[/]");
        var parityLines = new List<string>();
        foreach (var gaussianBlurSize in gaussianBlurSizeList) {
            var red = new CodeReader {
                GaussianBlurSize = gaussianBlurSize,
                ChantStartAngle = startAngle,
                Debug = debug,
            };
            var code = red.ReadCode(inputFile);
            var visionApi = new VisionApi();
            var result = await visionApi.RecognizeAsync(code, Direction.Horizontal);

            // 読み取り結果は大体2行。そのうちの1行はパリティが書いてあるはず。行が2行未満の場合はパリティが無いと判断
            var lines = result.Split('\n');
            var chantLine = lines[0];
            if (lines.Length >= 2) {
                // どっちがパリティかはわからない。ただしパリティ行には「。」がないはずなので、それを利用して判定する
                if (lines[0].Contains('。')) {
                    parityLines.Add(lines[1]);
                } else {
                    parityLines.Add(lines[0]);
                    // 1行目がパリティ行だった場合は2行目が呪文行
                    chantLine = lines[1];
                }
            }

            recognizeResultList.Add(($"VisionApi, GaussianBlurSize: {gaussianBlurSize}", chantLine));
        }

        AnsiConsole.MarkupLine("[bold red]内臓に真理祓いを抗う。[/]");
        AnsiConsole.MarkupLine("[bold red]原始にて帝王幸運引き裂く。[/]");
        AnsiConsole.MarkupLine("[bold red]淵より明光よ暴威を失わ。[/]");
        AnsiConsole.MarkupLine("[bold red]恐怖の静寂に指先に集い。[/]");
        AnsiConsole.MarkupLine("[bold red]腕に狭霧消し傷つける。[/]");
        AnsiConsole.MarkupLine("[bold red]臨界凍結輪廻に呼び覚まさ。[/]");
        AnsiConsole.MarkupLine("[bold red]祓いを神聖震えと結び。[/]");
        AnsiConsole.MarkupLine("[bold red]天使の輝きを胸中に死に。[/]");
        AnsiConsole.MarkupLine("[bold red]姫君よ翼で扉を現し。[/]");

        var magi = app.Services.GetService<Chant.Recognizer.ConsensusUnit.Magi>() ?? throw new NoNullAllowedException();
        var answer = await magi.GetAnswerFromRecognizeResultTextList(recognizeResultList);

        Console.WriteLine();
        AnsiConsole.Write(
            new Spectre.Console.Rule($"[bold red]{answer.Text}[/]")
        );
        Console.WriteLine();


        AnsiConsole.MarkupLine("[bold purple]内臓に真実は祓いを抗う。原始にて帝王脈動守る。記憶の石の波動と帰す。前の安寧を虚空を支え。開き極光よ楔を讃え。鬼と凍土に輪廻に呼び覚まさ。瞬の神より震えと思い出せ。天空に輝石を森に死ぬ。作れ。[/]");
        logger.LogDebug("Magiは: {answer} と言っています", answer.Text);
        foreach (var tuple in answer.RecognizerResults) {
            logger.LogDebug("{RecognizerName}:\t{Result}", tuple.RecognizerName, tuple.Result);
        }

        AnsiConsole.MarkupLine("[bold blue]断絶圧殺魔力を守る。沈黙の原始にて妖精に触れる。最果ての瞬の再生を食らい。瞬きよ障壁は声ば轟け。主に胸に暴威を借り。羽に希望の彼方より降らせ。淵に怨敵活殺歌い。従い。[/]");
        var parity = parityLines.Select(
                // 誤読補正処理
                hex => HexRegex().Replace(hex.Replace('x', '2'), "").Replace(" ", "")
            )
            .GroupBy(x => x)
            .MaxBy(x => x.Count())?.Key ?? "";
        if (!string.IsNullOrEmpty(parity)) {
            // パリティ1桁が、呪文の各文字を16で割った余りになっているはずなので、誤り検出に使えるってワケ
            try {
                var hexList = parity.Select(x => int.Parse($"{x}", NumberStyles.HexNumber)).ToList();
                if (hexList.Count == answer.Text.Length) {
                    if (hexList.Zip(answer.Text).Any(tuple => tuple.First % 16 != tuple.Second % 16)) {
                        logger.LogWarning("パリティが不正です: {parity}", parity);
                    }
                }
            }
            catch (Exception e) {
                logger.LogWarning("パリティの解析に失敗しました: {e}", e.Message);
            }
        }

        var yukichantBridge = app.Services.GetService<IYukiChantBridge>() ?? throw new NoNullAllowedException();
        var (exitCode, _, cmd, error) = await yukichantBridge.DecodeAsync(answer.Text);

        if(exitCode == 0) {
            logger.LogDebug("呪文をyukichantでデコードした結果「{text}」が返却されました", cmd);
        } else {
            logger.LogError("yukichantがエラーを返しました。終了ステータス:{status}: {error}", exitCode, error);
            AnsiConsole.MarkupLine("[red]呪文の解析に失敗しました(yukichantがエラーでした)[/]");
            return 1;
        }

        if (string.IsNullOrEmpty(cmd)) {
            AnsiConsole.MarkupLine("[red]呪文の解析に失敗しました(解析結果が空文字でした)[/]");
            return 1;
        }

        if (!AnsiConsole.Confirm("実行しますか？")) {
            AnsiConsole.MarkupLine("[red]呪文の発動がキャンセルされました[/]");
            return 1;
        }

        AnsiConsole.Write(
            new Spectre.Console.Rule("[bold red]以て眷属羽に喰らい。聖者無限より星の研ぎ澄ませ。[/]")
        );

        var processStartInfo = new ProcessStartInfo("powershell.exe") {
            ArgumentList = { "-c", cmd }
        };
        var process = new Process { StartInfo = processStartInfo };
        process.Start();

        process.WaitForExit();

        return 0;
    } catch (NoNullAllowedException e) {
        logger.LogError("エラーが発生しました: {e}", e.Message);
        return 1;
    } catch (Exception e) {
        logger.LogCritical("ハンドルされていないエラーが発生しました: {e}", e);
        return 1;
    }
});

partial class Program
{
    [GeneratedRegex("[0-9a-f]")]
    private static partial Regex HexRegex();
}
