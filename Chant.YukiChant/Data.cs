using System.Collections.Immutable;
using System.Runtime.Serialization;
using System.Text.Json;

namespace Chant.YukiChant;

public class Data
{
    /// <summary>
    /// YukiChantに登場する動詞一覧
    /// </summary>
    public readonly ImmutableArray<string> Verbs;

    /// <summary>
    /// YukiChantに登場する名詞一覧
    /// </summary>
    public readonly ImmutableArray<string> Nouns;

    /// <summary>
    /// YukiChantに登場する単語の長さごとに分類した辞書
    /// </summary>
    public readonly ImmutableDictionary<int, string[]> LengthDictionary;

    private readonly ImmutableDictionary<char, bool> charDictionary;

    public Data()
    {
        Verbs = LoadFromJson("dousi.json").SelectMany(x => x.Value).ToImmutableArray();
        Nouns = LoadFromJson("meisi.json").SelectMany(x => x.Value).ToImmutableArray();

        LengthDictionary = Verbs
            .Concat(Nouns)
            .GroupBy(x => x.Length)
            .ToImmutableDictionary(x => x.Key, x => x.ToArray());

        charDictionary = Verbs
            .Concat(Nouns)
            .SelectMany(x => x)
            .Distinct()
            .ToImmutableDictionary(x => x, _ => true);
    }

    /// <summary>
    /// その文字がYukiChantの辞書に含まれているかどうかを返す
    /// </summary>
    /// <param name="c"></param>
    /// <returns></returns>
    public bool Contains(char c) => charDictionary.ContainsKey(c);

    /// <summary>
    /// ビルド時にyukichantのdataディレクトリからコピーしてきた辞書ファイルを読み込んで返す
    /// </summary>
    /// <param name="fileName">meisi.jsonかdousi.json</param>
    /// <returns></returns>
    /// <exception cref="FailedToLoadYukiChantDataException"></exception>
    private static Dictionary<string, string[]> LoadFromJson(string fileName)
    {
        using var stream = new StreamReader(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "submodules",
                "yukichant",
                "data",
                fileName
            )
        );

        return JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream.BaseStream)
            ?? throw new FailedToLoadYukiChantDataException(fileName);
    }

    [Serializable]
    public class FailedToLoadYukiChantDataException : Exception
    {
        public FailedToLoadYukiChantDataException(string fileName)
            : base($"Failed to load YukiChant data from {fileName}") { }

        public FailedToLoadYukiChantDataException(string fileName, Exception inner)
            : base($"Failed to load YukiChant data from {fileName}", inner) { }

        protected FailedToLoadYukiChantDataException(
            SerializationInfo info,
            StreamingContext context
        ) : base(info, context) { }
    }
}
