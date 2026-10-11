namespace Saku_Overclock.Overlay.Helpers;

public static class SingleInstance
{
    private static Mutex? _mutex;

    public static bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, @"Local\SakuOverclock.Overlay", out var isFirstInstance);
        return isFirstInstance;
    }
}