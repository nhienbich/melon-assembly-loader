using MelonLoader;

namespace MelonAssemblyLoader;

internal interface ILog
{
    void Info(string message);
    void Warn(string message);
    void Error(string message);
}

internal sealed class MelonLog : ILog
{
    private readonly MelonLogger.Instance log;

    public MelonLog(MelonLogger.Instance log)
    {
        this.log = log;
    }

    public void Info(string message) => log.Msg(message);

    public void Warn(string message) => log.Warning(message);

    public void Error(string message) => log.Error(message);
}
