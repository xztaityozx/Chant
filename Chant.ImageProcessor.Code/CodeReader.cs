using System.Data;
using OpenCvSharp;

namespace Chant.ImageProcessor.Code;

public class CodeReader {
    public double ChantStartAngle { get; init; } = -100.0d;

    public (double Width, double Height) GaussianBlurSize { get; init; } = (25, 25);
    public (double NormalizeAlpha, double NormalizeBeta) NormalizeRange { get; init; } = (0, 255);

    public bool Debug { get; init; } = false;


    private void Show(string title, Mat mat) {
        if (!Debug) return;
        Cv2.ImShow($"{title} GaussianBlurSize: {GaussianBlurSize}", mat);
        Cv2.WaitKey();
    }

    private Mat CreateBinaryMat(Mat input)
    {
        using var grayMat = input.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var blurMat = grayMat.GaussianBlur(new Size(GaussianBlurSize.Width, GaussianBlurSize.Height), 0);
        using var diffMat = new Mat();
        Cv2.Absdiff(grayMat, blurMat, diffMat);
        using var normalizedMat = diffMat.Normalize(
            NormalizeRange.NormalizeAlpha, NormalizeRange.NormalizeBeta,
            NormTypes.MinMax
        );

        var result = normalizedMat.Threshold(0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        // 輪郭->伸縮をさせて輪郭を取りやすくする
        Cv2.Dilate(result, result, null);
        Cv2.Erode(result, result, null);

        return result;
    }

    private CircleSegment FindCircle(Mat input)
    {

        // 半径は画像の短辺の1/3。画像内は大きな円1つという前提があるので、ある程度大きくしておけば検出しやすい
        var minRadius = Math.Min(input.Width, input.Height) / 3.0;
        // 円の検出をしてみる
        var circles = Cv2.HoughCircles(
            input,
            HoughModes.Gradient,
            dp: 1f,
            minDist: Math.Max(input.Width, input.Height), // 画像内にはただ一つの円があるという前提があるのでここを大きくして1つに絞り込む
            param2: 30,
            minRadius: (int)minRadius
        );

        if (circles.Length == 0)
            // NoNullAllowedException が正しいかは微妙よね
            throw new NoNullAllowedException("画像内に円が見つかりませんでした");

        return circles[0];
    }

    public string ReadCode(string inputFile) {
        var outputFile = Path.Join(Path.GetTempPath(), $"{DateTime.Now:yyyy-MM-dd-hh-mm-ss}-code_preprocessed.png");

        using var inputMat = Cv2.ImRead(inputFile);
        var (width, height) = (inputMat.Width, inputMat.Height);
        using var scaleDownMat = inputMat.Resize(new Size(width / 3, height / 3));

        using var binaryMat = CreateBinaryMat(scaleDownMat);
        Show("Binary", binaryMat);

        var circle = FindCircle(binaryMat);
        var center = circle.Center.ToPoint();
        // 中心から一番近い上下左右の画像の端までを半径とする
        var radius = Math.Min(
            Math.Min(center.X, scaleDownMat.Width - center.X),
            Math.Min(center.Y, scaleDownMat.Height - center.Y)
        );
        var diameter = radius * 2;

        if (Debug){
            // 円を描画
            using var circleMat = scaleDownMat.Clone();
            Cv2.Circle(circleMat, center, radius, Scalar.Red, 2);
            Cv2.Circle(circleMat, center, (int)circle.Radius, Scalar.Blue, 2);
            Show("Circle", circleMat);
        }

        // 取り出した円を内接する正方形で画像を切り出す
        var square = new Rect(center.X - radius, center.Y - radius, diameter, diameter);
        square = square.Intersect(new Rect(0, 0, binaryMat.Width, binaryMat.Height));
        using var squareMat = new Mat(binaryMat, square);

        // center を中心に呪文の開始位置まで回転。これも自動検出できるといいんだけど
        var angle = Cv2.GetRotationMatrix2D(center, ChantStartAngle, 1.0);
        Cv2.WarpAffine(scaleDownMat, scaleDownMat, angle, new Size());
        Show("Rotate", scaleDownMat);

        // Polar変換で円形から直線形に変換
        using var dePolarMat = new Mat();
        Cv2.WarpPolar(
            scaleDownMat, dePolarMat,
            new Size(diameter, Math.PI * diameter), // 変換後の画像のサイズ。WarpPolarでは縦長に展開仕様とするので、縦長な画像として設定。横幅は直径、高さは演習(2πr)
            center, radius,
            InterpolationFlags.Linear | InterpolationFlags.WarpFillOutliers | InterpolationFlags.Cubic,
            WarpPolarMode.Linear);

        // 縦向きだとOCRがだるいので回転しておく
        Cv2.Rotate(dePolarMat, dePolarMat, RotateFlags.Rotate90Counterclockwise);

        Show("Result", dePolarMat);

        // 書き出しておわり
        Cv2.ImWrite(outputFile, dePolarMat);

        return outputFile;
    }
}
