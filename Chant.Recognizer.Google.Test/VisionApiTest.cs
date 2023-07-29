namespace Chant.Recognizer.Google.Test;

public class VisionApiTest
{
    [Fact]
    public void VisionApi_RecognizerName_Test()
    {
        var api = new VisionApi();
        Assert.Equal("GoogleCloudVisionApi", api.RecognizerName);
    }
}
