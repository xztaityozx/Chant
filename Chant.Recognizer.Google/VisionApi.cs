using Chant.Recognizer.Shared;
using Google.Cloud.Vision.V1;

namespace Chant.Recognizer.Google;

public class VisionApiFactory : IRecognizerFactory
{
    public IRecognizer Create()
    {
        return new VisionApi();
    }
}

public class VisionApi : IRecognizer
{
    public int Rect { get; set; } = 100;

    /// <summary>
    /// Google Cloud Vision API を使って画像からテキストを抽出する
    /// </summary>
    /// <param name="filePath">画像ファイルへのパス</param>
    /// <param name="direction">文字の方向</param>
    /// <returns></returns>
    public async Task<string> RecognizeAsync(string filePath, Direction direction)
    {
        var image = await Image.FromFileAsync(filePath);
        var client = await ImageAnnotatorClient.CreateAsync();
        var textAnnotation = await client.DetectDocumentTextAsync(image);

        // 並び替えないでそのまま取り出す。というのもRectを実行前に正確に計算するのが難しいっていうのがある
        // Rectの大きさを実行前に調べておけばいいのだけど
        // それってVisionAPIもやってるよねってなった(そしてVisionAPIのが高性能)
        return textAnnotation.Text;

        // LTのデモでやってたように100x100のグリッドが引かれた画像編集ソフトで書いた画像ならRectの効果は絶大
        // VisionAPIが順番を間違えても、座標をもとに並び替えることで、必ず正しい順番になるから
        //var words = textAnnotation.Pages
        //    .SelectMany(page => page.Blocks)
        //    .SelectMany(block => block.Paragraphs)
        //    .SelectMany(paragraph => paragraph.Words)
        //    .SelectMany(word => word.Symbols)
        //    .Select(
        //        symbol =>
        //            new
        //            {
        //                symbol.Text,
        //                X = symbol.BoundingBox.Vertices[0].X / Rect * Rect,
        //                Y = symbol.BoundingBox.Vertices[0].Y / Rect * Rect
        //            }
        //    );

        //// Rect * Rect のグリッドに正規化してからdirectionに従って並び替える
        //// 普通にテキストとして取り出すと段落がガタガタになることがある
        //return string.Join(
        //    "",
        //    direction == Direction.Vertical
        //        ? words
        //            .OrderByDescending(word => word.X) // 縦書きは右上から左下に並べることで再現できる
        //            .ThenBy(word => word.Y)
        //            .Select(word => word.Text)
        //        // こちらは横書き
        //        : words.OrderBy(word => word.Y).ThenBy(word => word.X).Select(word => word.Text)
        //);
    }

    public string RecognizerName => "GoogleCloudVisionApi";
}
