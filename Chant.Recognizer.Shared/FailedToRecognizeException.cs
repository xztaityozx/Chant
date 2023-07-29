namespace Chant.Recognizer.Shared;

[Serializable]
public class FailedToRecognizeException : Exception
{
    private const string DefaultMessage = "Failed to recognize";
    public string OriginalError { get; } = "";

    public FailedToRecognizeException() { }

    public FailedToRecognizeException(string message) : base(DefaultMessage)
    {
        OriginalError = message;
    }

    public FailedToRecognizeException(string message, Exception inner) : base(DefaultMessage, inner)
    {
        OriginalError = message;
    }

    protected FailedToRecognizeException(
        System.Runtime.Serialization.SerializationInfo serializationInfo,
        System.Runtime.Serialization.StreamingContext streamingContext
    ) : base(serializationInfo, streamingContext) { }
}
