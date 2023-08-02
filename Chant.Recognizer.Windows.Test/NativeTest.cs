namespace Chant.Recognizer.Windows.Test;

public class NativeTest
{
    [Fact]
    public void RecognizerNameTest()
    {
        var native = new Native();
        Assert.Equal("WindowsOcr", native.RecognizerName);
    }
}
