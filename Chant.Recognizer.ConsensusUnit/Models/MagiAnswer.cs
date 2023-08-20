using Chant.Gate.Models;

namespace Chant.Recognizer.ConsensusUnit.Models;

/// <summary>
/// Magiの解答を表すクラス
/// </summary>
/// <param name="Text">最終結果</param>
/// <param name="RecognizerResults">各Recognizerの結果</param>
public record MagiAnswer(
    string Text,
    IEnumerable<(
        string RecognizerName,
        string Result,
        IEnumerable<ReRecognizeResult> History
    )> RecognizerResults
);
