using Chant.Recognizer.Shared;
using Google.Cloud.Vision.V1;

namespace Chant.Recognizer.Google;

public class VisionApi : IRecognizer
{
    /// <summary>
    /// Google Cloud Vision API を使って画像からテキストを抽出する
    /// いまのところ横書きのみ…。
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public async Task<string> RecognizeAsync(string filePath)
    {
        var image = await Image.FromFileAsync(filePath);
        var client = await ImageAnnotatorClient.CreateAsync();
        var textAnnotation = await client.DetectDocumentTextAsync(image);
        return string.Join(
            "",
            textAnnotation.Pages
                .SelectMany(page => page.Blocks)
                .SelectMany(block => block.Paragraphs)
                .SelectMany(paragraph => paragraph.Words)
                .SelectMany(word => word.Symbols)
                // 100x100のグリッドに正規化して並び替える
                // 普通にテキストとして取り出すとパラグラフがえらいことになりがちなので…
                .OrderBy(symbol => symbol.BoundingBox.Vertices[0].Y / 100 * 100)
                .ThenBy(symbol => symbol.BoundingBox.Vertices[0].X / 100 * 100)
                .Select(a => a.Text)
        );
    }

    public string RecognizerName => "GoogleCloudVisionApi";
}
