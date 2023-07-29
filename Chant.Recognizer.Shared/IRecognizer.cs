namespace Chant.Recognizer.Shared;

public interface IRecognizer
{
    public Task<string> RecognizeAsync(string filePath);
    public string RecognizerName { get; }
}
