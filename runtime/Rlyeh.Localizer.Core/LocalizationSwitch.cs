namespace Rlyeh.Localizer.Core;

// One atomic token carries both the mode and its generation. Old work must not
// survive an ON -> OFF -> ON cycle, even when its source text is unchanged.
public sealed class LocalizationSwitch
{
    private long state;
    public LocalizationSwitch(bool enabled = true) => state = enabled ? 1 : 0;
    public long Revision => Volatile.Read(ref state);
    public bool Enabled => (Revision & 1) != 0;
    public bool Accepts(long revision) => (revision & 1) != 0 && Revision == revision;
    public bool SetEnabled(bool enabled)
    {
        while (true)
        {
            long current = Revision;
            if (((current & 1) != 0) == enabled) return false;
            if (Interlocked.CompareExchange(ref state, current + 1, current) == current) return true;
        }
    }
}
