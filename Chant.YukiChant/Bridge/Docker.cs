using System.Diagnostics;
using System.Runtime.Serialization;
using Chant.YukiChant.Models;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging;

namespace Chant.YukiChant.Bridge;

/// <summary>
/// Dockerを使ってyukichantを実行するクラス。ビルドもしてくれる。でもDocker Desktopの起動はしないので、実行前に起動しておくこと。
/// </summary>
public class Docker : IYukiChantBridge
{
    private const string ImageName = "chant/yukichant-node18";
    private readonly DockerClient dockerClient;
    private readonly ILogger<Docker> logger;

    public Docker(ILogger<Docker> logger)
    {
        // WindowsとMacならDocker Desktopがすでに起動していないとダメ
        dockerClient = new DockerClientConfiguration().CreateClient();
        this.logger = logger;
    }

    /// <summary>
    /// コンテナがあるかどうかを調べる
    /// </summary>
    /// <returns></returns>
    private async Task<bool> IsImageExistsAsync()
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
            }
        );
        return images.Any();
    }

    private async Task BuildImageAsync()
    {
        // コンテキストのコピーが少なくなるように一時ディレクトリを作ってそこでビルドする
        var context = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(context);

        const string dockerfileContent = """
FROM node:18.17.1-slim

RUN npm install -g yukichant

ENTRYPOINT ["chant"]
""";
        await File.WriteAllTextAsync(Path.Combine(context, "Dockerfile"), dockerfileContent);

        // dockerClient越しにイメージをビルドしたいんだけど
        // BuildImageFromDockerfileAsyncのAPIが複雑すぎるのでProcessを使ってる…
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"build -t {ImageName} .",
                WorkingDirectory = context,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var logs = new List<string>();
        // docker buildのログはstderrに出る
        process.ErrorDataReceived += (_, args) =>
        {
            var message = args.Data;
            if (string.IsNullOrEmpty(message))
                return;

            logger.LogInformation("{message}", message);
            logs.Add(message);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        Directory.Delete(context, true);

        if (process.ExitCode != 0)
        {
            throw new FailedToBuildImageException(logs);
        }

        logger.LogInformation("succeeded to build docker image for yukichant");
    }

    private async Task<YukiChantResult> RunAsync(
        YukiChantMode mode,
        string input,
        CancellationToken cancellationToken
    )
    {
        if (!await IsImageExistsAsync())
        {
            await BuildImageAsync();
        }

        try
        {
            var container = await dockerClient.Containers.CreateContainerAsync(
                new CreateContainerParameters
                {
                    Image = ImageName,
                    Cmd = mode switch
                    {
                        // inputの受け取り方次第なんだけど、最後の改行とかが削除されてて思った通りのエンコード結果にならないかも…
                        // なのでコンテナを起動しておいて、Attachでstdinにinputを流し込もうとしたけど
                        // Startした瞬間に chant がランダムな呪文を出力して終了するので上手くいかない
                        YukiChantMode.Encode
                            => new[] { input },
                        // こっちはstdinを待ち受けられるので、引数に渡さなくてもいいんだけど
                        // 後の実装をEncode側とあわせるために引数にしてる
                        YukiChantMode.Decode
                            => new[] { "-d", input },
                        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
                    },
                    HostConfig = new HostConfig { AutoRemove = true },
                },
                cancellationToken
            );

            await dockerClient.Containers.StartContainerAsync(
                container.ID,
                new ContainerStartParameters(),
                cancellationToken
            );

            // 出力拾いたいだけなのになんかめんどいな…
            var stream = await dockerClient.Containers.GetContainerLogsAsync(
                container.ID,
                false,
                new ContainerLogsParameters
                {
                    ShowStderr = true,
                    ShowStdout = true,
                    Follow = true
                },
                cancellationToken
            );

            var (stdout, stderr) = await stream.ReadOutputToEndAsync(cancellationToken);

            var waitResponse = await dockerClient.Containers.WaitContainerAsync(
                container.ID,
                cancellationToken
            );

            return new YukiChantResult(
                waitResponse.StatusCode,
                mode,
                stdout.TrimEnd(),
                stderr.TrimEnd()
            );
        }
        catch (DockerApiException e)
        {
            throw new FailedToLaunchYukiChantException("failed to launch yukichant container", e);
        }
    }

    public async Task<YukiChantResult> DecodeAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        return await RunAsync(YukiChantMode.Decode, input, cancellationToken);
    }

    public async Task<YukiChantResult> EncodeAsync(
        string input,
        CancellationToken cancellationToken = default
    )
    {
        return await RunAsync(YukiChantMode.Encode, input, cancellationToken);
    }
}

[Serializable]
public class FailedToBuildImageException : Exception
{
    public FailedToBuildImageException(IEnumerable<string> errors)
        : base(
            $"failed to build docker image for yukichant.{Environment.NewLine}inner error: {string.Join(Environment.NewLine, errors)}"
        ) { }

    public FailedToBuildImageException(string message, Exception innerException)
        : base(message, innerException) { }

    protected FailedToBuildImageException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
