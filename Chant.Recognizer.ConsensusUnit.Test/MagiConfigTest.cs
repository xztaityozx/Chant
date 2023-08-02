using System.Text.Json;

namespace Chant.Recognizer.ConsensusUnit.Test;

public class MagiConfigTest
{
    /// <summary>
    /// JSON文字列を読み込んで、<see cref="Magi.Config"/>のインスタンスを生成するテスト
    /// </summary>
    /// <param name="jsonString"></param>
    [Theory]
    [InlineData("{}")]
    [InlineData(
        """
{
    "RecognizerReliability": {
        "A": 0.9,
        "B": 0.8,
        "C": 0.7
    }
}
"""
    )]
    public void Load_Test(string jsonString)
    {
        using var testData = new TempFileFixture();
        var path = testData.CreateFile(jsonString);
        var actual = Magi.Config.Load(path);
        var expected = JsonSerializer.Deserialize<Magi.Config>(jsonString.AsSpan());

        Assert.NotNull(expected);
        Assert.Equal(expected.RecognizerReliability, actual.RecognizerReliability);
    }

    /// <summary>
    /// ファイルが見つからなかった時に例外を投げることを確認するテスト
    /// </summary>
    [Fact]
    public void Load_ThrowException_On_FileNotFound_Test()
    {
        using var testData = new TempFileFixture();
        var path = testData.CreateFile("{}");
        var exception = Assert.Throws<Magi.Config.FailedToLoadMagiConfigFromFileException>(
            () => Magi.Config.Load(path + "x")
        );

        Assert.Equal($"Failed to load magi config from file: {path}x", exception.Message);
        Assert.IsType<FileNotFoundException>(exception.InnerException);
    }

    /// <summary>
    /// JSONデータが不正な時に例外を投げることを確認するテスト
    /// </summary>
    /// <param name="jsonString"></param>
    [Theory]
    [InlineData("x")]
    [InlineData(
        """
{
    "ABC":
}
"""
    )]
    [InlineData(
        """
{
"""
    )]
    public void Load_ThrowException_On_JsonData_Is_Invalid_Test(string jsonString)
    {
        using var testData = new TempFileFixture();
        var path = testData.CreateFile(jsonString);
        var exception = Assert.Throws<Magi.Config.FailedToLoadMagiConfigFromFileException>(
            () => Magi.Config.Load(path)
        );

        Assert.Equal($"Failed to load magi config from file: {path}", exception.Message);
        Assert.IsType<JsonException>(exception.InnerException);
    }

    /// <summary>
    /// Tmpファイルを作成して、Disposeで消してくれるUtilクラス
    /// </summary>
    private sealed class TempFileFixture : IDisposable
    {
        private string tmpFilePath = "";
        private bool disposed;

        public string CreateFile(string jsonString)
        {
            tmpFilePath = Path.GetTempFileName();
            File.WriteAllText(tmpFilePath, jsonString);
            return tmpFilePath;
        }

        // ちゃんとしたDisposeパターンめんどい
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
                return;
            if (!disposing)
                return;
            if (!File.Exists(tmpFilePath))
                return;
            File.Delete(tmpFilePath);
            disposed = true;
        }

        ~TempFileFixture()
        {
            Dispose(false);
        }
    }
}
