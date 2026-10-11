using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Overlay.Contracts;

public interface IBackgroundDataReceiver
{
    /// <summary>
    ///     Start reading Shared Memory
    /// </summary>
    void Start();

    /// <summary>
    ///     Stop reading Shared Memory
    /// </summary>
    void Stop();
    
    /// <summary>
    ///     Data Received
    /// </summary>
    event Action<SensorsInformation>? Updated;
}