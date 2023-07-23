namespace Chant.Recognizer.Tesseract;

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
                return (false, "ExecutionFilePath is not exists");
            }

            // psmの範囲は0~13
            if (Psm is < 0 or > 13)
            {
                return (false, "Psm is invalid");
            }

            // 指定言語が空文字列だとTesseractが起動できない。正直jpn固定でいい
            return string.IsNullOrEmpty(Language)
                ? (false, "Language is null or empty")
                : (true, "");
        }
    }
}
