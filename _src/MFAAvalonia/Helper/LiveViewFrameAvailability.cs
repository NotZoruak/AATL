namespace MFAAvalonia.Helper;

public enum LiveViewFrameAvailabilityChange
{
    None,
    BecameUnavailable,
    Recovered
}

public sealed class LiveViewFrameAvailability
{
    private const int MissingFrameWarningThreshold = 3;

    private int _consecutiveMissingFrames;
    private bool _unavailabilityReported;

    public LiveViewFrameAvailabilityChange RecordFrame(bool hasFrame)
    {
        if (hasFrame)
        {
            _consecutiveMissingFrames = 0;
            if (!_unavailabilityReported)
                return LiveViewFrameAvailabilityChange.None;

            _unavailabilityReported = false;
            return LiveViewFrameAvailabilityChange.Recovered;
        }

        _consecutiveMissingFrames++;
        if (_unavailabilityReported || _consecutiveMissingFrames < MissingFrameWarningThreshold)
            return LiveViewFrameAvailabilityChange.None;

        _unavailabilityReported = true;
        return LiveViewFrameAvailabilityChange.BecameUnavailable;
    }

    public void Reset()
    {
        _consecutiveMissingFrames = 0;
        _unavailabilityReported = false;
    }
}
