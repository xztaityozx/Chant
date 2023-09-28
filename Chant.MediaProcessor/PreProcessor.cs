using System.Diagnostics;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace Chant.MediaProcessor;

public class PreProcessor
{
    private readonly DockerClient dockerClient;
    private readonly ILogger<PreProcessor> logger;
    private const string ImageName = "chant/imagemagick";
    private const string FFMpegImageName = "linuxserver/ffmpeg";

    public PreProcessor(ILogger<PreProcessor> logger)
    {
        this.logger = logger;
        dockerClient = new DockerClientConfiguration().CreateClient();
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    private async Task BuildContainer(CancellationToken token)
    {
        var images = await dockerClient.Images.ListImagesAsync(
            new ImagesListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    {
                        "reference",
                        new Dictionary<string, bool> { { ImageName, true } }
                    }
                }
            },
            token
        );

        if (images.Any())
        {
            logger.LogDebug("既にイメージが存在するのでビルドをスキップします");
            return;
        }

        var context = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(context);
        const string dockerfileContent = """
FROM ubuntu:22.04
RUN apt-get update && apt-get -y upgrade
RUN DEBIAN_FRONTEND=noninteractive apt-get -y install imagemagick
ENTRYPOINT ["convert"]
""";
        var dockerFilePath = Path.Combine(context, "Dockerfile");

        try
        {
            await File.WriteAllTextAsync(dockerFilePath, dockerfileContent, token);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = context,
                    FileName = "docker",
                    Arguments = $"build -t {ImageName} .",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                },
            };

            process.ErrorDataReceived += (_, args) =>
            {
                var message = args.Data;
                if (string.IsNullOrEmpty(message))
                    return;

                logger.LogDebug("{message}", message);
            };

            process.Start();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(token);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Dockerイメージのビルドに失敗しました(失敗したイメージ: {ImageName})"
                );
            }

            logger.LogDebug("イメージのビルドが完了しました");
        }
        finally
        {
            Directory.Delete(context, true);
        }
    }

    /// <summary>
    /// FFMpegを使って動画ファイルからフレームを抽出する。Dockerコンテナを使っているのでDockerが必要。イメージがない場合はPullしてくれる
    /// </summary>
    /// <param name="videoFileInfo"></param>
    /// <param name="outputDirectory"></param>
    /// <param name="size"></param>
    /// <param name="resize"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<string[]> ExtractFrameImageAsync(
        FileInfo videoFileInfo,
        string outputDirectory,
        string size,
        bool resize,
        CancellationToken token
    )
    {
        await BuildContainer(token);

        // 出力先を確実に作っておくぜ
        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        logger.LogDebug("コンテナを起動します");
        var container = await dockerClient.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = FFMpegImageName,
                Cmd = resize
                    ? new[] { "-i", "/input.mp4", "-s", size, "/output/%04d.png" }
                    : new[] { "-i", "/input.mp4", "/output/%04d.png" },
                HostConfig = new HostConfig
                {
                    AutoRemove = true,
                    Binds = new List<string>
                    {
                        $"{videoFileInfo.FullName}:/input.mp4",
                        $"{outputDirectory}:/output"
                    }
                }
            },
            token
        );

        await dockerClient.Containers.StartContainerAsync(
            container.ID,
            new ContainerStartParameters(),
            token
        );

        var result = await dockerClient.Containers.WaitContainerAsync(container.ID, token);
        if (result.StatusCode != 0)
        {
            logger.LogCritical("フレームの抽出中に致命的なエラーが発生しました。処理を中断しています");
            logger.LogError("{msg}", result.Error.Message);
        }
        logger.LogDebug("コンテナの実行が完了しました");

        return Directory.GetFiles(outputDirectory, "*.png");
    }

    public async Task EnsureBuildImageMagickImage(CancellationToken token)
    {
        await BuildContainer(token);
    }

    /// <summary>
    /// ImageMagickを使って画像を2値化する。Dockerコンテナを使っているのでDockerが必要。
    /// イメージがない場合はビルドしないといけないので、先に EnsureBuildImageMagickImage を呼ぶこと
    /// </summary>
    /// <param name="imageFilePath"></param>
    /// <param name="outputDirectory"></param>
    /// <param name="threshold"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<string> BinarizeAsync(
        string imageFilePath,
        string outputDirectory,
        int threshold,
        CancellationToken token
    )
    {
        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        var outputFileName = $"{Guid.NewGuid()}.png";

        logger.LogDebug("コンテナを起動します");
        var container = await dockerClient.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = ImageName,
                Cmd = new[]
                {
                    "/input.png",
                    "-threshold",
                    $"{threshold}",
                    $"/output/{outputFileName}"
                },
                HostConfig = new HostConfig
                {
                    AutoRemove = true,
                    Binds = new List<string>
                    {
                        $"{imageFilePath}:/input.png",
                        $"{outputDirectory}:/output"
                    }
                }
            },
            token
        );

        await dockerClient.Containers.StartContainerAsync(
            container.ID,
            new ContainerStartParameters(),
            token
        );

        var result = await dockerClient.Containers.WaitContainerAsync(container.ID, token);
        if (result.StatusCode != 0)
        {
            logger.LogCritical("2値化処理中に致命的なエラーが発生しました。処理を中断しています");
            logger.LogError("{msg}", result.Error.Message);
        }
        logger.LogDebug("コンテナの実行が完了しました");
        logger.LogDebug("出力ファイル: {outputFile}", outputFileName);

        return Path.Combine(outputDirectory, outputFileName);
    }
}
