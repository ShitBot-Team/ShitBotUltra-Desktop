using Microsoft.UI.Dispatching;

namespace shitbotteam.ultra.desktop.Services;

public sealed class WinUiDispatcher(DispatcherQueue dispatcherQueue) : IUiDispatcher
{
    public void Enqueue(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!dispatcherQueue.TryEnqueue(() => action()))
        {
            // The window is already closing; queued UI updates can be discarded.
        }
    }
}
