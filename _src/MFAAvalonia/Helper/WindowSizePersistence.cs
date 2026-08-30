namespace MFAAvalonia.Helper;

public readonly record struct WindowSize(double Width, double Height);

public static class WindowSizePersistence
{
    public static WindowSize? GetValidSize(double width, double height)
    {
        return width > 100 && height > 100
            ? new WindowSize(width, height)
            : null;
    }
}
