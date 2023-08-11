namespace Chant.YukiChant.Models;

/// <summary>
/// yukichantの実行結果を表すレコード
/// </summary>
/// <param name="Mode">エンコードかデコードか</param>
/// <param name="ExitCode">終了ステータス。何に使うか謎</param>
/// <param name="Output">エンコード・デコード結果</param>
/// <param name="Error">エラー出力</param>
public record YukiChantResult(long ExitCode, YukiChantMode Mode, string Output, string Error);

public enum YukiChantMode
{
    Encode,
    Decode
}
