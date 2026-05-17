using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SageRage.UI.Services;

public static class CircuitBackgroundService
{
    private static readonly Random _rng = new();

    private static readonly string[] _circuits =
    [
        "pack://application:,,,/Assets/Circuits/circuit1.png",
        "pack://application:,,,/Assets/Circuits/circuit2.png",
        "pack://application:,,,/Assets/Circuits/circuit3.png",
        "pack://application:,,,/Assets/Circuits/circuit4.png",
        "pack://application:,,,/Assets/Circuits/circuit5.png",
        "pack://application:,,,/Assets/Circuits/circuit6.png",
    ];

    private static int _lastIndex = -1;

    public static ImageBrush GetRandomBrush()
    {
        int index;
        do { index = _rng.Next(_circuits.Length); }
        while (index == _lastIndex && _circuits.Length > 1);
        _lastIndex = index;

        var bmp = new BitmapImage(new Uri(_circuits[index]));
        return new ImageBrush(bmp)
        {
            Stretch  = Stretch.UniformToFill,
            Opacity  = 0.50,
            TileMode = TileMode.None,
        };
    }
}
