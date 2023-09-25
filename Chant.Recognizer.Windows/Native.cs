using Windows.Graphics.Imaging;
using Chant.Recognizer.Shared;
using Windows.Media.Ocr;

namespace Chant.Recognizer.Windows;

public class Native : IRecognizer
{
    private readonly OcrEngine engine;
    public int Rect { get; set; } = 100;

    public Native()
    {
        engine = OcrEngine.TryCreateFromUserProfileLanguages();
    }

    /// <summary>
    /// Windows RT APIを使ってWindowsのOCRを呼び出して画像から文字列を読み取って返す
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="direction"></param>
    /// <returns></returns>
    public async Task<string> RecognizeAsync(string filePath, Direction direction)
    {
        await using var file = File.OpenRead(filePath);
        using var memoryStream = file.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(memoryStream);
        var softwareBitMap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied
        );

        var result = await engine.RecognizeAsync(softwareBitMap);

        var words = result.Lines
            .SelectMany(line => line.Words)
            .Select(
                word =>
                    new
                    {
                        word.Text,
                        X = (int)word.BoundingRect.X / Rect * Rect,
                        Y = (int)word.BoundingRect.Y / Rect * Rect,
                    }
            );

        if (direction == Direction.Vertical)
        {
            // 縦書きとして並び替えしてる
            return string.Join(
                    "",
                    words
                        .OrderByDescending(word => word.X)
                        .ThenBy(word => word.Y)
                        .Select(word => word.Text)
                )
                .Replace(" ", "");
        }

        // 横書きとして並び替えしてる
        return string.Join(
                "",
                words.OrderBy(word => word.Y).ThenBy(word => word.X).Select(word => word.Text)
            )
            .Replace(" ", "");
    }

    public string RecognizerName => "WindowsOcr";
}
