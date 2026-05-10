using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using LinearGradientBrush = System.Windows.Media.LinearGradientBrush;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace PetDude.Controls;

public sealed class PixelFarmScene : FrameworkElement
{
    public static readonly DependencyProperty ThemeProperty =
        DependencyProperty.Register(nameof(Theme), typeof(string), typeof(PixelFarmScene),
            new FrameworkPropertyMetadata("Spring Farm", FrameworkPropertyMetadataOptions.AffectsRender));

    private readonly DispatcherTimer _sceneTimer;
    private int _frame;

    private static readonly Brush SkyTop = Brush("#7EEFD3");
    private static readonly Brush SkyBottom = Brush("#C9FFE5");
    private static readonly Brush MountainFar = Brush("#4F91A2");
    private static readonly Brush MountainMid = Brush("#2E8D67");
    private static readonly Brush MountainLight = Brush("#54D95F");
    private static readonly Brush GrassA = Brush("#77B85B");
    private static readonly Brush GrassB = Brush("#82C465");
    private static readonly Brush GrassDark = Brush("#4B8F43");
    private static readonly Brush GrassLight = Brush("#9AE678");
    private static readonly Brush DirtA = Brush("#D7A34E");
    private static readonly Brush DirtB = Brush("#C88F3D");
    private static readonly Brush DirtDark = Brush("#8B5C32");
    private static readonly Brush Stone = Brush("#BCA36C");
    private static readonly Brush StoneDark = Brush("#7C6848");
    private static readonly Brush WaterA = Brush("#3D91B8");
    private static readonly Brush WaterB = Brush("#65BED4");
    private static readonly Brush WaterDark = Brush("#2B6D91");
    private static readonly Pen TransparentPen = new(Brushes.Transparent, 0);

    public string Theme
    {
        get => (string)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public PixelFarmScene()
    {
        _sceneTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        _sceneTimer.Tick += (_, _) =>
        {
            _frame++;
            InvalidateVisual();
        };

        Loaded += (_, _) => _sceneTimer.Start();
        Unloaded += (_, _) => _sceneTimer.Stop();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var width = Math.Max(1, ActualWidth);
        var height = Math.Max(1, ActualHeight);
        var horizon = Math.Clamp(height * 0.25, 78, 150);

        DrawSky(dc, width, horizon, _frame, Theme);
        DrawMountains(dc, width, horizon);
        DrawGround(dc, width, height, horizon, Theme);
        DrawFarmLayout(dc, width, height, horizon, _frame, Theme);
        DrawThemeAccent(dc, width, height, horizon, Theme, _frame);
        DrawSceneCritters(dc, width, height, horizon, _frame);
        DrawAtmosphere(dc, width, height, _frame);
    }

    private static void DrawSky(DrawingContext dc, double width, double horizon, int frame, string theme)
    {
        var (skyTop, skyBottom) = theme switch
        {
            "Night Garden" => ("#102B56", "#315B83"),
            "Autumn Orchard" => ("#F2B06B", "#FFE0A1"),
            "Forest Pond" => ("#81D9C6", "#C5F7D3"),
            "Mountain Ranch" => ("#79D7EF", "#D6FFF4"),
            "Flower Meadow" => ("#91F1D8", "#FFF0C8"),
            _ => ("#7EEFD3", "#C9FFE5")
        };
        var sky = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        sky.GradientStops.Add(new GradientStop(ParseColor(skyTop), 0));
        sky.GradientStops.Add(new GradientStop(ParseColor(skyBottom), 1));
        dc.DrawRectangle(sky, null, Rect(0, 0, width, horizon + 36));
        DrawSkyLight(dc, width, horizon, theme, frame);

        for (var i = 0; i < 7; i++)
        {
            var laneWidth = Math.Max(150, width + 180);
            var speed = 0.10 + i * 0.035;
            var seed = 40 + i * 127;
            var x = (seed + frame * speed) % laneWidth - 90;
            var y = 18 + i % 3 * 16 + Math.Sin((frame + i * 33) * 0.006) * 1.5;
            DrawCloud(dc, x, y);
        }

        if (theme == "Night Garden")
        {
            for (var i = 0; i < 28; i++)
            {
                var x = Hash(i * 31, 9) % Math.Max(1, (int)width);
                var y = 12 + Hash(i * 19, 4) % Math.Max(1, (int)horizon);
                dc.DrawRectangle(Brush(i % 5 == 0 ? "#FFF8B8" : "#DFF6FF"), null, Rect(x, y, i % 5 == 0 ? 3 : 2, i % 5 == 0 ? 3 : 2));
            }
        }
    }

    private static void DrawMountains(DrawingContext dc, double width, double horizon)
    {
        for (var x = -80.0; x < width + 100; x += 92)
        {
            DrawMountain(dc, x, horizon - 66, 92, 68, MountainFar, "#3B7485");
        }

        for (var x = -40.0; x < width + 80; x += 78)
        {
            DrawMountain(dc, x, horizon - 44, 86, 58, MountainMid, "#1F6C5A");
            DrawMountainHighlight(dc, x + 28, horizon - 38);
        }

        dc.DrawRectangle(Brush("#20715B"), null, Rect(0, horizon - 3, width, 18));
        for (var x = -20.0; x < width; x += 28)
        {
            DrawPine(dc, x, horizon - 44, 0.75);
        }
    }

    private static void DrawGround(DrawingContext dc, double width, double height, double horizon, string theme)
    {
        var grassA = theme switch
        {
            "Night Garden" => Brush("#386B55"),
            "Autumn Orchard" => Brush("#A6A04C"),
            "Forest Pond" => Brush("#4E9C58"),
            "Flower Meadow" => Brush("#88C96A"),
            _ => GrassA
        };
        var grassB = theme switch
        {
            "Night Garden" => Brush("#437A5E"),
            "Autumn Orchard" => Brush("#C2A953"),
            "Forest Pond" => Brush("#5BAA60"),
            "Flower Meadow" => Brush("#96D875"),
            _ => GrassB
        };

        dc.DrawRectangle(grassA, null, Rect(0, horizon, width, height - horizon));

        const int tile = 16;
        for (var y = Snap(horizon); y < height; y += tile)
        {
            for (var x = 0; x < width; x += tile)
            {
                var parity = (((int)x / tile) + ((int)y / tile)) % 2 == 0;
                dc.DrawRectangle(parity ? grassB : grassA, null, Rect(x, y, tile, tile));
                if (Hash(x, y) % 5 == 0)
                {
                    dc.DrawRectangle(GrassDark, null, Rect(x + 3, y + 10, 5, 2));
                }
                if (Hash(y, x) % 7 == 0)
                {
                    dc.DrawRectangle(GrassLight, null, Rect(x + 10, y + 5, 2, 2));
                }
                if (Hash(x + 13, y + 29) % 6 == 0)
                {
                    DrawGrassTuft(dc, x + 5, y + 6, theme);
                }
                if (Hash(x + 41, y + 7) % 17 == 0)
                {
                    DrawTinyFlower(dc, x + 11, y + 8, Hash(x, y) % 3);
                }
            }
        }
    }

    private static void DrawFarmLayout(DrawingContext dc, double width, double height, double horizon, int frame, string theme)
    {
        var yardTop = horizon + 54;
        var dirtTop = Math.Max(horizon + 92, height - 116);
        DrawDirtBand(dc, 0, dirtTop, width, height - dirtTop);

        DrawPath(dc, width * 0.48, horizon + 30, 34, height - horizon - 34);
        DrawPath(dc, 46, height - 86, width - 92, 30);
        DrawStonePath(dc, width * 0.48 - 8, horizon + 42, 48, height - horizon - 62);
        DrawStonePath(dc, width * 0.70, height - 118, 42, 92);

        DrawPond(dc, Math.Max(38, width - 206), Math.Max(horizon + 118, height - 132), 178, 100, frame);
        DrawCropRows(dc, 44, Math.Max(yardTop + 76, height - 174), Math.Min(300, width * 0.48), 124);
        DrawFlowerPatch(dc, Math.Max(340, width * 0.58), Math.Max(yardTop + 52, height - 162), 130, 94);
        DrawBerryPatch(dc, Math.Max(84, width * 0.20), Math.Max(yardTop + 46, height - 230), 118, 52, frame);

        DrawFence(dc, 0, horizon + 38, width);
        DrawFence(dc, 56, Math.Max(horizon + 96, height - 182), Math.Min(260, width * 0.45));

        DrawGreenhouse(dc, 34, yardTop + 12);
        DrawCabin(dc, Math.Max(300, width - 250), yardTop);
        DrawHayStack(dc, Math.Max(260, width * 0.42), yardTop + 56);
        DrawGardenSign(dc, Math.Max(150, width * 0.30), height - 98);
        DrawScarecrow(dc, Math.Max(270, width * 0.55), Math.Max(horizon + 104, height - 150), frame);
        DrawToolCrate(dc, Math.Max(70, width - 316), height - 76);

        DrawTree(dc, 92, horizon + 8 + (frame % 6 == 0 ? 1 : 0), 1.05);
        DrawTree(dc, Math.Max(180, width * 0.34), horizon + 4 + (frame % 7 == 0 ? 1 : 0), 0.88);
        DrawTree(dc, Math.Max(470, width - 110), horizon + 16 + (frame % 5 == 0 ? 1 : 0), 1.2);
        DrawBush(dc, 22, height - 72 + (frame % 8 == 0 ? 1 : 0), 1.1);
        DrawBush(dc, Math.Max(220, width * 0.55), height - 66 + (frame % 9 == 0 ? 1 : 0), 0.95);
        DrawForegroundDetail(dc, width, height, frame);
    }

    private static void DrawThemeAccent(DrawingContext dc, double width, double height, double horizon, string theme, int frame)
    {
        switch (theme)
        {
            case "Flower Meadow":
                DrawFlowerPatch(dc, 26, horizon + 70, Math.Min(width - 52, 420), Math.Min(150, height - horizon - 90));
                DrawButterfly(dc, width * 0.62 + Math.Sin(frame * 0.04) * 18, horizon + 64 + Math.Cos(frame * 0.06) * 10);
                break;
            case "Forest Pond":
                for (var x = 8.0; x < width; x += 76)
                {
                    DrawPine(dc, x, horizon + 6 + (x % 2), 1.15);
                }
                DrawBush(dc, width - 150, height - 96, 1.35);
                break;
            case "Mountain Ranch":
                DrawFence(dc, 0, horizon + 82, width);
                DrawSheep(dc, width * 0.25 + Math.Sin(frame * 0.04) * 12, horizon + 88);
                DrawSheep(dc, width * 0.62 + Math.Sin(frame * 0.035) * 16, horizon + 102);
                break;
            case "Autumn Orchard":
                for (var x = 42.0; x < width; x += 140)
                {
                    DrawTree(dc, x, horizon + 36, 0.9);
                    dc.DrawRectangle(Brush("#D9532F"), null, Rect(x + 28, horizon + 58, 7, 7));
                    dc.DrawRectangle(Brush("#F2B949"), null, Rect(x + 48, horizon + 70, 7, 7));
                }
                break;
            case "Night Garden":
                dc.DrawRectangle(Brush("#0A1730", 0.28), null, Rect(0, 0, width, height));
                for (var i = 0; i < 10; i++)
                {
                    var x = (Hash(i * 71, 15) + frame) % Math.Max(1, (int)width);
                    var y = horizon + 34 + Hash(i * 11, 42) % Math.Max(1, (int)(height - horizon - 60));
                    dc.DrawRectangle(Brush("#B9FF8A"), null, Rect(x, y, 3, 3));
                    dc.DrawRectangle(Brush("#F8FFD0"), null, Rect(x + 1, y - 1, 1, 1));
                }
                break;
        }
    }

    private static void DrawSkyLight(DrawingContext dc, double width, double horizon, string theme, int frame)
    {
        if (theme == "Night Garden")
        {
            var moonX = width - 82;
            dc.DrawRectangle(Brush("#F7F1C6"), null, Rect(moonX, 18, 28, 28));
            dc.DrawRectangle(Brush("#102B56"), null, Rect(moonX + 12, 14, 20, 24));
            dc.DrawRectangle(Brush("#FFF8B8", 0.22), null, Rect(moonX - 10, 10, 48, 48));
            return;
        }

        var sunX = Math.Max(46, width * 0.12 + Math.Sin(frame * 0.01) * 5);
        dc.DrawRectangle(Brush("#FFE783"), null, Rect(sunX, 18, 26, 26));
        dc.DrawRectangle(Brush("#FFF7BA", 0.38), null, Rect(sunX - 8, 10, 42, 42));
        for (var i = 0; i < 5; i++)
        {
            dc.DrawRectangle(Brush("#FFF7BA", 0.18), null, Rect(sunX + 42 + i * 34, 28 + i % 2 * 8, Math.Max(12, width * 0.10), 3));
        }
    }

    private static void DrawDirtBand(DrawingContext dc, double x, double y, double width, double height)
    {
        dc.DrawRectangle(DirtA, null, Rect(x, y, width, height));
        for (var py = Snap(y); py < y + height; py += 16)
        {
            for (var px = Snap(x); px < x + width; px += 16)
            {
                dc.DrawRectangle(Hash(px, py) % 2 == 0 ? DirtB : DirtA, null, Rect(px, py, 16, 16));
                if (Hash(py, px) % 4 == 0)
                {
                    dc.DrawRectangle(DirtDark, null, Rect(px + 4, py + 9, 7, 2));
                }
                if (Hash(px + 5, py + 3) % 7 == 0)
                {
                    dc.DrawRectangle(Brush("#E6BD69"), null, Rect(px + 2, py + 3, 4, 2));
                }
                if (Hash(px + 9, py + 17) % 9 == 0)
                {
                    dc.DrawRectangle(Brush("#7A4E2B"), null, Rect(px + 12, py + 12, 2, 2));
                }
            }
        }
    }

    private static void DrawPath(DrawingContext dc, double x, double y, double width, double height)
    {
        dc.DrawRectangle(Brush("#D6B363"), null, Rect(x, y, width, height));
        dc.DrawRectangle(Brush("#B98B46"), null, Rect(x, y + height - 5, width, 5));
        for (var py = y + 8; py < y + height - 8; py += 18)
        {
            dc.DrawRectangle(Brush("#E3C276"), null, Rect(x + 5 + Hash(py, x) % Math.Max(1, (int)Math.Max(1, width - 16)), py, 8, 2));
        }
    }

    private static void DrawStonePath(DrawingContext dc, double x, double y, double width, double height)
    {
        for (var py = y; py < y + height; py += 18)
        {
            for (var px = x + (Hash(py, x) % 2) * 8; px < x + width; px += 18)
            {
                dc.DrawRectangle(StoneDark, null, Rect(px, py + 2, 12, 8));
                dc.DrawRectangle(Stone, null, Rect(px, py, 12, 7));
                dc.DrawRectangle(Brush("#D6C58B"), null, Rect(px + 2, py + 1, 5, 2));
            }
        }
    }

    private static void DrawCropRows(DrawingContext dc, double x, double y, double width, double height)
    {
        var rows = Math.Max(2, (int)(height / 34));
        for (var row = 0; row < rows; row++)
        {
            var rowY = y + row * 34;
            dc.DrawRectangle(DirtDark, null, Rect(x, rowY + 12, width, 16));
            for (var px = x + 12; px < x + width - 10; px += 34)
            {
                DrawCrop(dc, px, rowY + 2, row % 3);
            }
        }
    }

    private static void DrawCrop(DrawingContext dc, double x, double y, int type)
    {
        dc.DrawRectangle(Brush("#275F36"), null, Rect(x + 10, y + 18, 4, 10));
        dc.DrawRectangle(Brush("#3AA348"), null, Rect(x + 4, y + 12, 10, 8));
        dc.DrawRectangle(Brush("#2F8D3D"), null, Rect(x + 14, y + 10, 9, 9));
        dc.DrawRectangle(Brush("#56C85D"), null, Rect(x + 8, y + 7, 8, 8));
        if (type == 1)
        {
            dc.DrawRectangle(Brush("#E44352"), null, Rect(x + 12, y + 9, 6, 7));
            dc.DrawRectangle(Brush("#FF8992"), null, Rect(x + 13, y + 10, 2, 2));
        }
        else if (type == 2)
        {
            dc.DrawRectangle(Brush("#F4E26B"), null, Rect(x + 10, y + 6, 8, 8));
            dc.DrawRectangle(Brush("#FFF6A3"), null, Rect(x + 12, y + 7, 3, 2));
        }
        else
        {
            dc.DrawRectangle(Brush("#82E36B"), null, Rect(x + 15, y + 12, 3, 2));
        }
    }

    private static void DrawFlowerPatch(DrawingContext dc, double x, double y, double width, double height)
    {
        for (var py = y; py < y + height; py += 18)
        {
            for (var px = x; px < x + width; px += 18)
            {
                dc.DrawRectangle(Brush("#27743A"), null, Rect(px + 7, py + 8, 6, 12));
                var bloom = Hash(px, py) % 3 == 0 ? "#EF7FA0" : Hash(px, py) % 3 == 1 ? "#F3D66C" : "#83A9FF";
                dc.DrawRectangle(Brush(bloom), null, Rect(px + 4, py + 4, 5, 5));
                dc.DrawRectangle(Brush(bloom), null, Rect(px + 11, py + 4, 5, 5));
                dc.DrawRectangle(Brush(bloom), null, Rect(px + 7, py + 1, 5, 5));
                dc.DrawRectangle(Brush("#FFF5CE"), null, Rect(px + 8, py + 7, 4, 4));
                dc.DrawRectangle(Brush("#7FEA70"), null, Rect(px + 2, py + 14, 5, 3));
            }
        }
    }

    private static void DrawBerryPatch(DrawingContext dc, double x, double y, double width, double height, int frame)
    {
        dc.DrawRectangle(Brush("#6F4A2D"), null, Rect(x, y + height - 10, width, 14));
        dc.DrawRectangle(Brush("#9E6A39"), null, Rect(x + 4, y + height - 12, width - 8, 8));
        for (var px = x + 10; px < x + width - 10; px += 22)
        {
            var sway = frame % 9 == 0 ? 1 : 0;
            dc.DrawRectangle(Brush("#1F6B38"), null, Rect(px + 7, y + 20 + sway, 4, 18));
            dc.DrawRectangle(Brush("#2E9C48"), null, Rect(px + 2, y + 14 + sway, 12, 10));
            dc.DrawRectangle(Brush("#43BF59"), null, Rect(px + 10, y + 11 + sway, 11, 10));
            dc.DrawRectangle(Brush("#78E468"), null, Rect(px + 8, y + 10 + sway, 5, 5));
            dc.DrawRectangle(Brush("#D83454"), null, Rect(px + 12, y + 15 + sway, 4, 4));
            dc.DrawRectangle(Brush("#FF7A8F"), null, Rect(px + 13, y + 15 + sway, 1, 1));
        }
    }

    private static void DrawGardenSign(DrawingContext dc, double x, double y)
    {
        dc.DrawRectangle(Brush("#5E3925"), null, Rect(x + 16, y + 22, 6, 30));
        dc.DrawRectangle(Brush("#5E3925"), null, Rect(x + 56, y + 22, 6, 30));
        dc.DrawRectangle(Brush("#71442A"), null, Rect(x + 8, y + 5, 64, 24));
        dc.DrawRectangle(Brush("#C48B4C"), null, Rect(x + 12, y + 3, 56, 22));
        dc.DrawRectangle(Brush("#F0BF72"), null, Rect(x + 16, y + 6, 26, 4));
        dc.DrawRectangle(Brush("#6FA34C"), null, Rect(x + 22, y + 14, 8, 7));
        dc.DrawRectangle(Brush("#D84855"), null, Rect(x + 32, y + 13, 6, 6));
        dc.DrawRectangle(Brush("#7A4B2D"), null, Rect(x + 44, y + 14, 14, 3));
    }

    private static void DrawScarecrow(DrawingContext dc, double x, double y, int frame)
    {
        var sway = frame % 14 == 0 ? 1 : 0;
        dc.DrawRectangle(Brush("#5A3925"), null, Rect(x + 27, y + 22, 6, 50));
        dc.DrawRectangle(Brush("#5A3925"), null, Rect(x + 8, y + 34 + sway, 44, 5));
        dc.DrawRectangle(Brush("#D9A65B"), null, Rect(x + 20, y + 6, 20, 20));
        dc.DrawRectangle(Brush("#8B5A32"), null, Rect(x + 13, y + 4, 34, 8));
        dc.DrawRectangle(Brush("#654027"), null, Rect(x + 20, y, 20, 7));
        dc.DrawRectangle(Brush("#704D9C"), null, Rect(x + 17, y + 28, 26, 26));
        dc.DrawRectangle(Brush("#5B397D"), null, Rect(x + 18, y + 49, 24, 8));
        dc.DrawRectangle(Brush("#F0D184"), null, Rect(x + 12, y + 38 + sway, 6, 6));
        dc.DrawRectangle(Brush("#F0D184"), null, Rect(x + 43, y + 38 - sway, 6, 6));
        dc.DrawRectangle(Brush("#3B2718"), null, Rect(x + 24, y + 14, 3, 3));
        dc.DrawRectangle(Brush("#3B2718"), null, Rect(x + 34, y + 14, 3, 3));
        dc.DrawRectangle(Brush("#A24D32"), null, Rect(x + 27, y + 20, 10, 2));
        dc.DrawRectangle(Brush("#FFE08A"), null, Rect(x + 17, y + 7, 7, 3));
    }

    private static void DrawToolCrate(DrawingContext dc, double x, double y)
    {
        dc.DrawRectangle(Brush("#5E3925"), null, Rect(x, y + 20, 58, 24));
        dc.DrawRectangle(Brush("#A46637"), null, Rect(x + 4, y + 14, 50, 26));
        dc.DrawRectangle(Brush("#C98A4D"), null, Rect(x + 8, y + 18, 42, 5));
        dc.DrawRectangle(Brush("#734225"), null, Rect(x + 6, y + 30, 46, 4));
        dc.DrawRectangle(Brush("#66797B"), null, Rect(x + 12, y + 3, 5, 19));
        dc.DrawRectangle(Brush("#D6D2B8"), null, Rect(x + 9, y, 11, 5));
        dc.DrawRectangle(Brush("#5B6F86"), null, Rect(x + 32, y + 1, 5, 23));
        dc.DrawRectangle(Brush("#C9D8DA"), null, Rect(x + 29, y, 11, 5));
        dc.DrawRectangle(Brush("#7D5235"), null, Rect(x + 42, y + 4, 4, 20));
        dc.DrawRectangle(Brush("#B9A36C"), null, Rect(x + 39, y + 1, 10, 5));
    }

    private static void DrawForegroundDetail(DrawingContext dc, double width, double height, int frame)
    {
        var y = height - 38;
        for (var x = -8.0; x < width + 20; x += 22)
        {
            var lift = Hash(x, y) % 3;
            DrawGrassTuft(dc, x, y + lift, "Spring Farm");
            if (Hash(x + 13, y + frame) % 5 == 0)
            {
                DrawTinyFlower(dc, x + 10, y + lift + 3, Hash(x, frame) % 3);
            }
        }
    }

    private static void DrawGrassTuft(DrawingContext dc, double x, double y, string theme)
    {
        var dark = theme == "Night Garden" ? "#214B3E" : "#2F753A";
        var mid = theme == "Autumn Orchard" ? "#7D873D" : "#41A849";
        var light = theme == "Autumn Orchard" ? "#D1B750" : "#75DD61";
        dc.DrawRectangle(Brush(dark), null, Rect(x + 4, y + 8, 8, 3));
        dc.DrawRectangle(Brush(mid), null, Rect(x + 2, y + 5, 4, 6));
        dc.DrawRectangle(Brush(mid), null, Rect(x + 8, y + 3, 4, 8));
        dc.DrawRectangle(Brush(light), null, Rect(x + 6, y + 1, 3, 8));
        dc.DrawRectangle(Brush(light), null, Rect(x + 12, y + 6, 3, 5));
    }

    private static void DrawTinyFlower(DrawingContext dc, double x, double y, int type)
    {
        var color = type switch
        {
            1 => "#F4E26B",
            2 => "#83A9FF",
            _ => "#EF7FA0"
        };
        dc.DrawRectangle(Brush("#2F753A"), null, Rect(x + 3, y + 5, 2, 7));
        dc.DrawRectangle(Brush(color), null, Rect(x, y + 1, 4, 4));
        dc.DrawRectangle(Brush(color), null, Rect(x + 5, y, 4, 4));
        dc.DrawRectangle(Brush("#FFF5CE"), null, Rect(x + 4, y + 3, 2, 2));
    }

    private static void DrawPond(DrawingContext dc, double x, double y, double width, double height, int frame)
    {
        dc.DrawRectangle(Brush("#8A642D"), null, Rect(x - 8, y - 8, width + 16, height + 16));
        dc.DrawRectangle(Brush("#B77F39"), null, Rect(x - 4, y - 4, width + 8, height + 8));
        dc.DrawRectangle(WaterDark, null, Rect(x, y, width, height));
        dc.DrawRectangle(WaterA, null, Rect(x + 8, y + 8, width - 16, height - 16));
        var shimmer = (frame % 8) * 3;
        dc.DrawRectangle(WaterB, null, Rect(x + 20 + shimmer, y + 18, width - 70, 5));
        dc.DrawRectangle(WaterB, null, Rect(x + 52 - shimmer / 2, y + 48, width - 86, 5));
        dc.DrawRectangle(Brush("#A8E8E7", 0.58), null, Rect(x + 28 - shimmer / 3, y + 30, width - 118, 3));
        dc.DrawRectangle(Brush("#255C76"), null, Rect(x + 8, y + height - 14, width - 20, 6));
        dc.DrawRectangle(Brush("#3C8F44"), null, Rect(x + 28, y + height - 22, 20, 10));
        dc.DrawRectangle(Brush("#87D662"), null, Rect(x + 34, y + height - 28, 8, 8));
        dc.DrawRectangle(Brush("#E986B0"), null, Rect(x + 46, y + height - 34, 7, 4));
        dc.DrawRectangle(Brush("#FFE9A6"), null, Rect(x + 48, y + height - 36, 3, 2));
    }

    private static void DrawFence(DrawingContext dc, double x, double y, double width)
    {
        dc.DrawRectangle(Brush("#6A442C"), null, Rect(x, y + 20, width, 7));
        dc.DrawRectangle(Brush("#7C5232"), null, Rect(x, y + 36, width, 7));
        dc.DrawRectangle(Brush("#B7834D"), null, Rect(x, y + 20, width, 2));
        for (var px = x; px < x + width; px += 34)
        {
            dc.DrawRectangle(Brush("#5A3925"), null, Rect(px + 3, y + 9, 12, 44));
            dc.DrawRectangle(Brush("#8D623D"), null, Rect(px, y + 5, 14, 42));
            dc.DrawRectangle(Brush("#B5854D"), null, Rect(px + 2, y + 1, 10, 8));
            dc.DrawRectangle(Brush("#D0A264"), null, Rect(px + 3, y + 8, 3, 34));
            dc.DrawRectangle(Brush("#4B2F20"), null, Rect(px + 10, y + 14, 2, 28));
        }
    }

    private static void DrawCabin(DrawingContext dc, double x, double y)
    {
        dc.DrawRectangle(Brush("#654027"), null, Rect(x + 18, y + 70, 160, 14));
        dc.DrawRectangle(Brush("#B97436"), null, Rect(x + 24, y + 36, 144, 78));
        for (var px = x + 32; px < x + 160; px += 18)
        {
            dc.DrawRectangle(Brush("#D18A45"), null, Rect(px, y + 40, 5, 68));
            dc.DrawRectangle(Brush("#7E4D2D"), null, Rect(px + 10, y + 40, 3, 68));
            dc.DrawRectangle(Brush("#F0B568"), null, Rect(px + 1, y + 43, 2, 14));
        }

        dc.DrawRectangle(Brush("#8B2E24"), null, Rect(x + 12, y + 24, 168, 18));
        dc.DrawRectangle(Brush("#C54D2E"), null, Rect(x + 26, y + 8, 140, 24));
        dc.DrawRectangle(Brush("#E56B39"), null, Rect(x + 42, y, 108, 18));
        dc.DrawRectangle(Brush("#FF9961"), null, Rect(x + 46, y + 3, 96, 4));
        for (var px = x + 36; px < x + 160; px += 22)
        {
            dc.DrawRectangle(Brush("#872819"), null, Rect(px, y + 10, 4, 30));
            dc.DrawRectangle(Brush("#F08951"), null, Rect(px + 6, y + 12, 10, 2));
        }

        dc.DrawRectangle(Brush("#5E3925"), null, Rect(x + 52, y + 72, 34, 42));
        dc.DrawRectangle(Brush("#C9894A"), null, Rect(x + 58, y + 78, 22, 30));
        dc.DrawRectangle(Brush("#E4B06C"), null, Rect(x + 61, y + 80, 9, 25));
        dc.DrawRectangle(Brush("#744321"), null, Rect(x + 58, y + 94, 22, 4));
        dc.DrawRectangle(Brush("#2D1D16"), null, Rect(x + 76, y + 92, 4, 4));
        dc.DrawRectangle(Brush("#4E6D78"), null, Rect(x + 112, y + 62, 34, 28));
        dc.DrawRectangle(Brush("#B9EEFF"), null, Rect(x + 118, y + 66, 22, 18));
        dc.DrawRectangle(Brush("#E9FFFF", 0.58), null, Rect(x + 120, y + 67, 8, 5));
        dc.DrawRectangle(Brush("#6B4B33"), null, Rect(x + 126, y + 64, 4, 24));
        dc.DrawRectangle(Brush("#6B4B33"), null, Rect(x + 116, y + 74, 26, 4));
        dc.DrawRectangle(Brush("#4D4D55"), null, Rect(x + 148, y - 18, 18, 34));
        dc.DrawRectangle(Brush("#86828A"), null, Rect(x + 150, y - 24, 14, 8));
        dc.DrawRectangle(Brush("#B5B0B8"), null, Rect(x + 151, y - 20, 12, 3));
    }

    private static void DrawGreenhouse(DrawingContext dc, double x, double y)
    {
        dc.DrawRectangle(Brush("#7D5235"), null, Rect(x + 8, y + 92, 112, 8));
        dc.DrawRectangle(Brush("#82D7DF"), null, Rect(x + 8, y + 34, 112, 60));
        dc.DrawRectangle(Brush("#C9F6F0"), null, Rect(x + 18, y + 24, 92, 20));
        dc.DrawRectangle(Brush("#FFFFFF", 0.32), null, Rect(x + 22, y + 29, 24, 8));
        dc.DrawRectangle(Brush("#E4A26A"), null, Rect(x + 4, y + 30, 8, 68));
        dc.DrawRectangle(Brush("#E4A26A"), null, Rect(x + 118, y + 30, 8, 68));
        dc.DrawRectangle(Brush("#E4A26A"), null, Rect(x + 60, y + 24, 8, 74));
        dc.DrawRectangle(Brush("#E4A26A"), null, Rect(x + 8, y + 58, 112, 7));
        dc.DrawRectangle(Brush("#D98657"), null, Rect(x + 42, y + 72, 44, 26));
        dc.DrawRectangle(Brush("#6FB45D"), null, Rect(x + 20, y + 66, 22, 18));
        dc.DrawRectangle(Brush("#6FB45D"), null, Rect(x + 88, y + 66, 18, 18));
        dc.DrawRectangle(Brush("#E986B0"), null, Rect(x + 27, y + 62, 5, 5));
        dc.DrawRectangle(Brush("#F4E26B"), null, Rect(x + 96, y + 64, 5, 5));
    }

    private static void DrawHayStack(DrawingContext dc, double x, double y)
    {
        dc.DrawRectangle(Brush("#8B5C2F"), null, Rect(x, y + 22, 72, 36));
        dc.DrawRectangle(Brush("#E7B54F"), null, Rect(x + 8, y + 10, 56, 42));
        dc.DrawRectangle(Brush("#F4D16D"), null, Rect(x + 14, y + 4, 42, 18));
        dc.DrawRectangle(Brush("#A66F2D"), null, Rect(x + 14, y + 24, 44, 4));
        dc.DrawRectangle(Brush("#A66F2D"), null, Rect(x + 18, y + 38, 38, 4));
        dc.DrawRectangle(Brush("#FFE08A"), null, Rect(x + 20, y + 8, 20, 4));
        dc.DrawRectangle(Brush("#B8792B"), null, Rect(x + 10, y + 15, 4, 33));
        dc.DrawRectangle(Brush("#B8792B"), null, Rect(x + 54, y + 18, 4, 28));
    }

    private static void DrawCloud(DrawingContext dc, double x, double y)
    {
        dc.DrawRectangle(Brush("#D5F2EE"), null, Rect(x + 3, y + 15, 58, 18));
        dc.DrawRectangle(Brush("#E8FFF7"), null, Rect(x, y + 10, 58, 18));
        dc.DrawRectangle(Brush("#F7FFFB"), null, Rect(x + 12, y, 28, 28));
        dc.DrawRectangle(Brush("#F7FFFB"), null, Rect(x + 32, y + 5, 20, 18));
        dc.DrawRectangle(Brush("#C7E8E1"), null, Rect(x + 4, y + 26, 44, 5));
        dc.DrawRectangle(Brush("#B5DAD7"), null, Rect(x + 42, y + 15, 12, 8));
        dc.DrawRectangle(Brush("#FFFFFF", 0.5), null, Rect(x + 18, y + 4, 13, 4));
    }

    private static void DrawMountain(DrawingContext dc, double x, double y, double width, double height, Brush body, string shade)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(new Point(x, y + height), true, true);
            ctx.LineTo(new Point(x + width * 0.5, y), true, false);
            ctx.LineTo(new Point(x + width, y + height), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(body, null, geo);
        dc.DrawRectangle(Brush(shade), null, Rect(x + width * 0.5, y + 10, width * 0.16, height - 10));
    }

    private static void DrawMountainHighlight(DrawingContext dc, double x, double y)
    {
        dc.DrawRectangle(MountainLight, null, Rect(x, y, 8, 34));
        dc.DrawRectangle(MountainLight, null, Rect(x - 9, y + 18, 8, 20));
        dc.DrawRectangle(MountainLight, null, Rect(x + 9, y + 12, 8, 26));
    }

    private static void DrawPine(DrawingContext dc, double x, double y, double scale)
    {
        var s = scale;
        dc.DrawRectangle(Brush("#7A4F31"), null, Rect(x + 11 * s, y + 38 * s, 8 * s, 22 * s));
        dc.DrawRectangle(Brush("#A36A39"), null, Rect(x + 14 * s, y + 40 * s, 3 * s, 16 * s));
        dc.DrawRectangle(Brush("#0F5E43"), null, Rect(x + 6 * s, y + 28 * s, 20 * s, 14 * s));
        dc.DrawRectangle(Brush("#13784C"), null, Rect(x + 2 * s, y + 18 * s, 28 * s, 16 * s));
        dc.DrawRectangle(Brush("#1B9156"), null, Rect(x + 7 * s, y + 7 * s, 18 * s, 16 * s));
        dc.DrawRectangle(Brush("#54D95F"), null, Rect(x + 14 * s, y + 13 * s, 5 * s, 20 * s));
        dc.DrawRectangle(Brush("#0A472F"), null, Rect(x + 8 * s, y + 33 * s, 12 * s, 4 * s));
    }

    private static void DrawTree(DrawingContext dc, double x, double y, double scale)
    {
        var s = scale;
        dc.DrawRectangle(Brush("#674326"), null, Rect(x + 28 * s, y + 66 * s, 18 * s, 56 * s));
        dc.DrawRectangle(Brush("#8B5A32"), null, Rect(x + 34 * s, y + 70 * s, 6 * s, 44 * s));
        dc.DrawRectangle(Brush("#B07842"), null, Rect(x + 37 * s, y + 73 * s, 3 * s, 32 * s));
        for (var row = 0; row < 5; row++)
        {
            for (var col = 0; col < 5; col++)
            {
                if (Math.Abs(col - 2) + row < 6)
                {
                    var color = (row + col) % 3 == 0 ? "#1F8D3F" : (row + col) % 3 == 1 ? "#28AD45" : "#116F35";
                    dc.DrawRectangle(Brush(color), null, Rect(x + (col * 13 + row * 2) * s, y + (row * 12) * s, 22 * s, 18 * s));
                }
            }
        }
        dc.DrawRectangle(Brush("#66E55B"), null, Rect(x + 30 * s, y + 16 * s, 10 * s, 42 * s));
        dc.DrawRectangle(Brush("#A2FF77"), null, Rect(x + 36 * s, y + 20 * s, 8 * s, 10 * s));
        dc.DrawRectangle(Brush("#0B5A2C"), null, Rect(x + 20 * s, y + 52 * s, 16 * s, 9 * s));
    }

    private static void DrawBush(DrawingContext dc, double x, double y, double scale)
    {
        var s = scale;
        dc.DrawRectangle(Brush("#0D5C35"), null, Rect(x + 10 * s, y + 22 * s, 80 * s, 26 * s));
        dc.DrawRectangle(Brush("#16853E"), null, Rect(x + 2 * s, y + 12 * s, 76 * s, 26 * s));
        dc.DrawRectangle(Brush("#24AA46"), null, Rect(x + 18 * s, y + 2 * s, 62 * s, 28 * s));
        dc.DrawRectangle(Brush("#63E45D"), null, Rect(x + 34 * s, y + 12 * s, 12 * s, 22 * s));
        dc.DrawRectangle(Brush("#8BFF70"), null, Rect(x + 42 * s, y + 7 * s, 16 * s, 5 * s));
        dc.DrawRectangle(Brush("#0B4B2C"), null, Rect(x + 18 * s, y + 37 * s, 50 * s, 6 * s));
    }

    private static void DrawAtmosphere(DrawingContext dc, double width, double height, int frame)
    {
        for (var i = 0; i < 34; i++)
        {
            var x = (Hash(i * 37, 11) + frame * (1 + i % 3)) % Math.Max(1, (int)width);
            var y = 34 + (Hash(i * 17, 29) + frame * (1 + i % 2)) % Math.Max(1, (int)(height - 60));
            dc.DrawRectangle(Brush(i % 2 == 0 ? "#F7B7D3" : "#FFF3C4"), null, Rect(x, y, 3, 3));
        }
    }

    private static void DrawSceneCritters(DrawingContext dc, double width, double height, double horizon, int frame)
    {
        var birdX = width - (frame * 2 % Math.Max(120, (int)width + 120));
        DrawBird(dc, birdX, 34 + frame % 18, "#B23AD8");
        DrawBird(dc, birdX + 58, 54 + frame % 11, "#E43AD8");

        var butterflyX = 90 + frame % Math.Max(120, (int)Math.Max(120, width - 180));
        DrawButterfly(dc, butterflyX, horizon + 36 + Math.Sin(frame * 0.4) * 9);

        var chickenX = 120 + frame % 46;
        DrawChicken(dc, chickenX, Math.Max(horizon + 102, height - 154));

        var sheepX = Math.Max(220, width * 0.42) + Math.Sin(frame * 0.12) * 20;
        DrawSheep(dc, sheepX, horizon + 34);
    }

    private static void DrawBird(DrawingContext dc, double x, double y, string color)
    {
        var body = Brush(color);
        dc.DrawRectangle(Brush("#4A2256", 0.28), null, Rect(x + 11, y + 18, 22, 4));
        dc.DrawRectangle(body, null, Rect(x + 12, y + 8, 18, 10));
        dc.DrawRectangle(body, null, Rect(x + 5, y + 4, 12, 8));
        dc.DrawRectangle(body, null, Rect(x + 26, y + 3, 14, 9));
        dc.DrawRectangle(Brush("#FF85F2"), null, Rect(x + 15, y + 9, 7, 4));
        dc.DrawRectangle(Brush("#FFFFFF", 0.55), null, Rect(x + 27, y + 4, 5, 3));
        dc.DrawRectangle(Brush("#FFE766"), null, Rect(x + 38, y + 10, 8, 4));
        dc.DrawRectangle(Brush("#7B198F"), null, Rect(x + 16, y + 15, 14, 4));
    }

    private static void DrawButterfly(DrawingContext dc, double x, double y)
    {
        dc.DrawRectangle(Brush("#FFF0A0"), null, Rect(x + 6, y + 5, 4, 12));
        dc.DrawRectangle(Brush("#F0A0FF"), null, Rect(x, y, 8, 8));
        dc.DrawRectangle(Brush("#D682FF"), null, Rect(x + 10, y + 1, 8, 8));
        dc.DrawRectangle(Brush("#B95BDE"), null, Rect(x + 1, y + 7, 7, 6));
        dc.DrawRectangle(Brush("#A64FCF"), null, Rect(x + 12, y + 8, 7, 6));
        dc.DrawRectangle(Brush("#FFFFFF"), null, Rect(x + 2, y + 2, 3, 3));
        dc.DrawRectangle(Brush("#FFFFFF"), null, Rect(x + 13, y + 3, 3, 3));
    }

    private static void DrawChicken(DrawingContext dc, double x, double y)
    {
        dc.DrawRectangle(Brush("#6A5C39", 0.26), null, Rect(x + 8, y + 32, 30, 4));
        dc.DrawRectangle(Brush("#FFF2CF"), null, Rect(x + 8, y + 10, 28, 18));
        dc.DrawRectangle(Brush("#FFFFFF"), null, Rect(x + 12, y + 4, 18, 14));
        dc.DrawRectangle(Brush("#FFE8A8"), null, Rect(x + 12, y + 15, 12, 8));
        dc.DrawRectangle(Brush("#E44436"), null, Rect(x + 16, y, 10, 6));
        dc.DrawRectangle(Brush("#3A231B"), null, Rect(x + 26, y + 8, 3, 3));
        dc.DrawRectangle(Brush("#F2B949"), null, Rect(x + 32, y + 10, 8, 4));
        dc.DrawRectangle(Brush("#DFA23C"), null, Rect(x + 14, y + 28, 4, 6));
        dc.DrawRectangle(Brush("#DFA23C"), null, Rect(x + 28, y + 28, 4, 6));
        dc.DrawRectangle(Brush("#B46B2D"), null, Rect(x + 12, y + 34, 9, 1));
        dc.DrawRectangle(Brush("#B46B2D"), null, Rect(x + 26, y + 34, 9, 1));
    }

    private static void DrawSheep(DrawingContext dc, double x, double y)
    {
        dc.DrawRectangle(Brush("#6A5C39", 0.22), null, Rect(x + 6, y + 39, 34, 4));
        dc.DrawRectangle(Brush("#EDE6D4"), null, Rect(x + 6, y + 14, 34, 20));
        dc.DrawRectangle(Brush("#F9F4E8"), null, Rect(x + 10, y + 8, 25, 22));
        dc.DrawRectangle(Brush("#FFFFFF"), null, Rect(x + 15, y + 10, 12, 6));
        dc.DrawRectangle(Brush("#DCD4C1"), null, Rect(x + 7, y + 25, 30, 5));
        dc.DrawRectangle(Brush("#D7D0BE"), null, Rect(x + 32, y + 12, 10, 12));
        dc.DrawRectangle(Brush("#2E241E"), null, Rect(x + 36, y + 16, 3, 3));
        dc.DrawRectangle(Brush("#BEB4A2"), null, Rect(x + 39, y + 20, 4, 3));
        dc.DrawRectangle(Brush("#5A4638"), null, Rect(x + 12, y + 34, 4, 8));
        dc.DrawRectangle(Brush("#5A4638"), null, Rect(x + 30, y + 34, 4, 8));
    }

    private static Brush Brush(string color)
    {
        return Brush(color, 1.0);
    }

    private static Brush Brush(string color, double opacity)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!);
        brush.Opacity = opacity;
        brush.Freeze();
        return brush;
    }

    private static Color ParseColor(string color)
    {
        return (Color)ColorConverter.ConvertFromString(color)!;
    }

    private static Rect Rect(double x, double y, double width, double height)
    {
        return new Rect(Snap(x), Snap(y), Math.Max(0, Snap(width)), Math.Max(0, Snap(height)));
    }

    private static double Snap(double value) => Math.Round(value);

    private static int Hash(double x, double y)
    {
        unchecked
        {
            var a = (int)Math.Round(x);
            var b = (int)Math.Round(y);
            var hash = a * 73856093 ^ b * 19349663;
            return Math.Abs(hash);
        }
    }
}
