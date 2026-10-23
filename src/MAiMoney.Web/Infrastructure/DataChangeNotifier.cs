namespace MAiMoney.Web.Infrastructure;

/// <summary>
/// Tells the open page that transactions or categories changed somewhere else in the UI,
/// such as in the header's quick-add menu, so it can reload. One instance per circuit.
/// </summary>
public class DataChangeNotifier
{
    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();
}
