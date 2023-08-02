using Chant.YukiChant.Models;

namespace Chant.YukiChant.Bridge;

public interface IYukiChantBridge
{
    public Task<YukiChantResult> DecodeAsync(
        string input,
        CancellationToken cancellationToken = default
    );

    public Task<YukiChantResult> EncodeAsync(
        string input,
        CancellationToken cancellationToken = default
    );
}
