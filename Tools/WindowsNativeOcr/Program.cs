using Chant.Recognizer.Shared;

if (args.Length == 0)
{
    Console.WriteLine("Usage: WindowsNativeOcr.exe <image file>");
    return;
}

var recognizer = new Chant.Recognizer.Windows.Native();
var text = await recognizer.RecognizeAsync(args[0], Direction.Horizontal);

Console.WriteLine(text);
