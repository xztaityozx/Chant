namespace Chant.MediaProcessor.Test;

public class PreProcessorTest
{
    [Fact]
    public async Task SplitVideoTest()
    {
        var videoFile = new FileInfo("./asset/input.mp4");
        Assert.True(videoFile.Exists);

        var preProcessor = new PreProcessor();
        var frames = (
            await preProcessor.SplitVideoAsync(videoFile, CancellationToken.None)
        ).ToArray();

        Assert.NotNull(frames);
        Assert.Single(frames);
    }

    [Fact]
    public async Task ResizeAndBinarizeImageTest()
    {
        const string imageFile = "./asset/input.png";
        Assert.True(File.Exists(imageFile));

        var preProcessor = new PreProcessor();
        var processedImage = await preProcessor.ResizeAndBinarizeImageAsync(
            imageFile,
            CancellationToken.None
        );

        Assert.True(File.Exists(processedImage));
    }
}
