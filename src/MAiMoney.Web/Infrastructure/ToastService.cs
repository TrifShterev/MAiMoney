namespace MAiMoney.Web.Infrastructure;

public enum ToastKind
{
    Success,
    Error
}

public record Toast(Guid Id, string Message, ToastKind Kind);

/// <summary>Short confirmation messages shown in the corner of the screen. One instance per circuit.</summary>
public class ToastService
{
    private readonly List<Toast> toasts = [];

    public event Action? Changed;

    public IReadOnlyList<Toast> Toasts => toasts;

    public void Show(string message, ToastKind kind = ToastKind.Success)
    {
        toasts.Add(new Toast(Guid.NewGuid(), message, kind));
        Changed?.Invoke();
    }

    public void Dismiss(Guid id)
    {
        if (toasts.RemoveAll(t => t.Id == id) > 0)
        {
            Changed?.Invoke();
        }
    }
}
