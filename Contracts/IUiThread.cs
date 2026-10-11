namespace Saku_Overclock.Overlay.Contracts;

/// <summary>
///     Message from host-window
/// </summary>
public delegate void UiMessageHandler(uint message, nuint wParam, nint lParam);

public interface IUiThread
{
    /// <summary>
    ///     Hidden host-window Hwnd
    /// </summary>
    nint Handle { get; }
    
    /// <summary>
    ///     Create hidden host-window
    /// </summary>
    void Initialize();

    /// <summary>
    ///     Remove host-window
    /// </summary>
    void Cleanup();

    /// <summary>
    ///     Post action on window
    /// </summary>
    void Post(Action action);

    /// <summary>
    ///     Async action on window
    /// </summary>
    Task InvokeAsync(Action action, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Other handler messages, such as Wm Hotkey, Wm Timer
    /// </summary>
    event UiMessageHandler? MessageReceived;
    
    /// <summary>
    ///     User end session
    /// </summary>
    event Action? SessionEnding;
}