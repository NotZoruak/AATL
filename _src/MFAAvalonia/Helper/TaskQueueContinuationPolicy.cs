namespace MFAAvalonia.Helper;

public static class TaskQueueContinuationPolicy
{
    public static bool CanContinue(bool taskFailed, bool continueOnError) =>
        taskFailed && continueOnError;
}
