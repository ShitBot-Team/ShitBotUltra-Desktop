namespace shitbotteam.ultra.desktop.Services;

public interface IUiDispatcher
{
    void Enqueue(Action action);
}
