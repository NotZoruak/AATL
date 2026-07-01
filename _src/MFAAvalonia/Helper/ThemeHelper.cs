using Avalonia.Styling;
using SukiUI;

namespace MFAAvalonia.Helper;

public static class ThemeHelper
{
    static ThemeHelper()
    {

    }
    public static void UpdateThemeIndexChanged(int value)
    {
        switch (value)
        {
            case 0:
                SukiTheme.GetInstance().ChangeBaseTheme(ThemeVariant.Light);
                break;
            case 1:
                SukiTheme.GetInstance().ChangeBaseTheme(ThemeVariant.Dark);
                break;
            default:
                // ThemeManager.Current.UsingWindowsAppTheme = true;
                break;
        }
    }
}
