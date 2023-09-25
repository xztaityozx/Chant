namespace Chant.Recognizer.Shared;

public enum Direction
{
    Horizontal,
    Vertical,
}

public interface IRecognizer
{
    public Task<string> RecognizeAsync(string filePath, Direction direction);
    public string RecognizerName { get; }
}

public interface IRecognizerFactory
{
    public IRecognizer Create();
}
