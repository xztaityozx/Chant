using System.Runtime.Serialization;
using System.Text.Json;

namespace Chant.Recognizer.ConsensusUnit;

public partial class Magi
{
    [Serializable]
    public class Config
    {
        public IDictionary<string, decimal> RecognizerReliability { get; init; } =
            new Dictionary<string, decimal>();

        public static Config Load(string path)
        {
            try
            {
                return JsonSerializer.Deserialize<Config>(File.ReadAllText(path))
                    ?? throw new FailedToLoadMagiConfigFromFileException(path);
            }
            catch (Exception e) when (e is FileNotFoundException or JsonException)
            {
                throw new FailedToLoadMagiConfigFromFileException(path, e);
            }
        }

        [Serializable]
        public class FailedToLoadMagiConfigFromFileException : Exception
        {
            private static string ToMessage(string path) =>
                $"Failed to load magi config from file: {path}";

            public FailedToLoadMagiConfigFromFileException() { }

            public FailedToLoadMagiConfigFromFileException(string path) : base(ToMessage(path)) { }

            public FailedToLoadMagiConfigFromFileException(string path, Exception inner)
                : base(ToMessage(path), inner) { }

            protected FailedToLoadMagiConfigFromFileException(
                SerializationInfo info,
                StreamingContext context
            ) : base(info, context) { }
        }
    }
}
