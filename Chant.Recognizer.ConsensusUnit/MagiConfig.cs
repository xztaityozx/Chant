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

        /// <summary>
        /// デフォルト設定～。テキトー！
        /// </summary>
        public static Config Default =>
            new()
            {
                RecognizerReliability = new Dictionary<string, decimal>
                {
                    // GoogleVisionApiは信頼度が一番高いようにしているけど、ほかの2種が一致してれば超えるようにしてる
                    // GoogleVisionApiが一番高いのは、テキトーに試した感じだと一番精度が高かったから
                    ["Tesseract"] = 1.0M,
                    ["Windows"] = 1.0M,
                    ["VisionApi"] = 1.5M
                }
            };

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
