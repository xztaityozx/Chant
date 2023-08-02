using Windows.Graphics.Imaging;
using Chant.Recognizer.Shared;
using Windows.Media.Ocr;

namespace Chant.Recognizer.Windows;

public class Native : IRecognizer
{
    private readonly OcrEngine engine;

    public Native()
    {
        engine = OcrEngine.TryCreateFromUserProfileLanguages();
    }

    /// <summary>
    /// Windows RT APIを使ってWindowsのOCRを呼び出して画像から文字列を読み取って返す
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public async Task<string> RecognizeAsync(string filePath)
    {
        await using var file = File.OpenRead(filePath);
        using var memoryStream = file.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(memoryStream);
        var softwareBitMap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied
        );

        var result = await engine.RecognizeAsync(softwareBitMap);

        // 横書きとして並び替えしてる
        return string.Join(
                "",
                result.Lines
                    .SelectMany(line => line.Words)
                    .OrderBy(word => (int)word.BoundingRect.Y / 100 * 100)
                    .ThenBy(word => (int)word.BoundingRect.X / 100 * 100)
                    .Select(word => word.Text)
            )
            .Replace(" ", "");
    }

    public string RecognizerName => "WindowsOcr";
}
