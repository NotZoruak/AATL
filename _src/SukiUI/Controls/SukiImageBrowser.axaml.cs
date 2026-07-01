using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;

namespace SukiUI.Controls;

public partial class SukiImageBrowser : Window
{
    public SukiImageBrowser()
    {
        InitializeComponent();
    }

    public void SetImage(Bitmap bitmap)
    {
        ImageViewer.Source = bitmap;
    }
}

