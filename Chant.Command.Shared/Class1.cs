using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Chant.Command.Shared;

public sealed class DefaultCommand : AsyncCommand<DefaultCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[FILE]")]
        [Description("画像ファイルへのパス")]
        public string? FilePath { get; init; }

        [CommandOption("-d|debug")]
        [Description("Debugログを出力します")]
        public bool Debug { get; init; }

        [CommandOption("-v|verbose")]
        [Description("変換過程などの詳細ログを出力します")]
        public bool Verbose { get; init; }

        [CommandOption("-o|--ocr-engines")]
        [Description("使用するOCRエンジンを指定します。カンマ区切りです")]
        public string[] OcrEngines { get; init; } = { "tesseract" };
    }

    private readonly IAnsiConsole console;

    public DefaultCommand(IAnsiConsole console)
    {
        this.console = console;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        throw new NotImplementedException();
    }
}
