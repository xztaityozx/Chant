using System.Runtime.Serialization;

namespace Chant.YukiChant.Bridge;

/// <summary>
/// yukichantの起動に失敗したときに投げる例外
/// </summary>
[Serializable]
public class FailedToLaunchYukiChantException : Exception
{
    public FailedToLaunchYukiChantException(string message, Exception innerException)
        : base(message, innerException) { }

    protected FailedToLaunchYukiChantException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
