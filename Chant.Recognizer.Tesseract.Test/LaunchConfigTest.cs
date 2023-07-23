using System.Collections;

namespace Chant.Recognizer.Tesseract.Test;

public class LaunchConfigTest
{
    /// <summary>
    /// LaunchConfig.Status�̃e�X�g
    /// </summary>
    /// <param name="path"></param>
    /// <param name="lang"></param>
    /// <param name="psm"></param>
    /// <param name="expected"></param>
    [Theory, ClassData(typeof(StatusTestData))]
    public void Status_Test(
        string path,
        string lang,
        int psm,
        (bool IsValid, string ErrorMessage) expected
    )
    {
        var config = new LaunchConfig(path, lang, psm);
        var (isValid, message) = config.Status;
        Assert.Equal(expected.IsValid, isValid);
        Assert.Equal(expected.ErrorMessage, message);
    }

    /// <summary>
    /// Status_Test�̃e�X�g�f�[�^��񋟂���N���X
    /// </summary>
    internal class StatusTestData : IEnumerable<object[]>
    {
        private readonly string dummyFilePath;
        private readonly List<object[]> data;

        #region IEnumerable<object[]>�̎���
        public IEnumerator<object[]> GetEnumerator() => data.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion

        public StatusTestData()
        {
            dummyFilePath = Path.GetTempFileName();
            data = new List<object[]>
            {
                new object[] { "", "jpn", 6, (false, "ExecutionFilePath is not exists") },
                new object[] { "not_exists", "jpn", 6, (false, "ExecutionFilePath is not exists") },
                new object[] { dummyFilePath, "", 6, (false, "Language is null or empty") },
                new object[] { dummyFilePath, "jpn", -1, (false, "Psm is invalid") },
                new object[] { dummyFilePath, "jpn", 14, (false, "Psm is invalid") },
                new object[] { dummyFilePath, "jpn", 0, (true, "") },
                new object[] { dummyFilePath, "jpn", 13, (true, "") },
            };
        }

        /// <summary>
        /// TearDown�p�̃f�X�g���N�^�����ǁA��肭�����Ă�̂�����
        /// </summary>
        ~StatusTestData()
        {
            File.Delete(dummyFilePath);
        }
    }
}
