namespace MCPforDalamud.McpServer;

public class FrameworkRunner : IFrameworkRunner
{
    public void Run(Action action)
    {
        var tcs = new TaskCompletionSource();
        Service.Framework.RunOnFrameworkThread(() =>
        {
            try
            {
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        tcs.Task.GetAwaiter().GetResult();
    }
}
