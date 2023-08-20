namespace Chant.Gate.Models;

public record GuideResult(
    string Original,
    string Result,
    IEnumerable<ReRecognizeResult> ReRecognizeHistory
);
