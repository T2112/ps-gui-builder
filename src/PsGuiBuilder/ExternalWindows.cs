// Personal project by Thomas Schueler.
// Developed on personal time using personal equipment.
// Not related to any employment.

namespace PsGuiBuilder;

public static class ExternalWindows
{
    private static int _held;
    private static string? _deferred;

    public static bool Held => Volatile.Read(ref _held) > 0;

    public static void Hold() => Interlocked.Increment(ref _held);

    public static string? Release()
    {
        if (Interlocked.Decrement(ref _held) > 0)
            return null;

        var message = _deferred;
        _deferred = null;
        return message;
    }

    public static bool TryDefer(string message)
    {
        if (!Held)
            return false;

        _deferred = message;
        return true;
    }
}
