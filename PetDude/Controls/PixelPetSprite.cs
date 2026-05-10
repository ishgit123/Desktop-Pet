using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace PetDude.Controls;

public sealed class PixelPetSprite : FrameworkElement
{
    private const double ArtSize = 96;

    public static readonly DependencyProperty VariantProperty =
        DependencyProperty.Register(nameof(Variant), typeof(string), typeof(PixelPetSprite),
            new FrameworkPropertyMetadata("Orange", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FrameProperty =
        DependencyProperty.Register(nameof(Frame), typeof(int), typeof(PixelPetSprite),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsWalkingProperty =
        DependencyProperty.Register(nameof(IsWalking), typeof(bool), typeof(PixelPetSprite),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PoseProperty =
        DependencyProperty.Register(nameof(Pose), typeof(string), typeof(PixelPetSprite),
            new FrameworkPropertyMetadata("Idle", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DirectionProperty =
        DependencyProperty.Register(nameof(Direction), typeof(string), typeof(PixelPetSprite),
            new FrameworkPropertyMetadata("Down", FrameworkPropertyMetadataOptions.AffectsRender));

    public string Variant
    {
        get => (string)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public int Frame
    {
        get => (int)GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    public bool IsWalking
    {
        get => (bool)GetValue(IsWalkingProperty);
        set => SetValue(IsWalkingProperty, value);
    }

    public string Pose
    {
        get => (string)GetValue(PoseProperty);
        set => SetValue(PoseProperty, value);
    }

    public string Direction
    {
        get => (string)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var scale = Math.Max(1, Math.Floor(Math.Min(ActualWidth, ActualHeight) / ArtSize));
        var offsetX = Math.Round((ActualWidth - ArtSize * scale) / 2.0);
        var offsetY = Math.Round((ActualHeight - ArtSize * scale) / 2.0);
        var palette = Palette.For(Variant);
        var frame = Frame % 32;
        var walk = IsWalking ? (Frame / 4) % 4 : 0;
        var blink = !IsWalking && frame is 24 or 25;
        var sit = Pose == "Sit";
        var loaf = Pose == "Loaf";
        var bob = IsWalking ? (walk is 1 or 3 ? -2 : 0) : sit ? -1 : 0;
        var tail = sit ? -3 : loaf ? 1 : (int)Math.Round(Math.Sin(Frame * 0.42) * 3);

        void Px(double x, double y, double w, double h, Brush brush)
        {
            dc.DrawRectangle(brush, null, new Rect(
                offsetX + Math.Round(x * scale),
                offsetY + Math.Round(y * scale),
                Math.Max(scale, Math.Round(w * scale)),
                Math.Max(scale, Math.Round(h * scale))));
        }

        if (loaf)
        {
            DrawLoaf(Px, palette, blink, tail);
            return;
        }

        if (Direction is "Up" or "UpLeft" or "UpRight")
        {
            DrawBack(Px, palette, sit, walk, tail, bob);
            return;
        }

        if (Direction is "Left" or "Right" or "DownLeft" or "DownRight")
        {
            DrawSide(Px, palette, sit, walk, tail, bob, blink);
            return;
        }

        DrawFront(Px, palette, sit, walk, tail, bob, blink);
    }

    private static void DrawFront(Action<double, double, double, double, Brush> px, Palette p, bool sit, int walk, int tail, int bob, bool blink)
    {
        DrawShadow(px, 23, 78, 51, 9);
        DrawFrontTail(px, p, tail);
        DrawFrontBody(px, p, sit);
        DrawFrontLegs(px, p, sit, walk);
        DrawFrontHead(px, p, bob);
        DrawFrontCoat(px, p, sit, bob);
        DrawFrontFace(px, p, bob, blink);
    }

    private static void DrawBack(Action<double, double, double, double, Brush> px, Palette p, bool sit, int walk, int tail, int bob)
    {
        DrawShadow(px, 23, 78, 51, 9);

        px(16, 59 + tail, 8, 18, p.Outline);
        px(10, 49 + tail, 10, 17, p.Outline);
        px(13, 47 + tail, 10, 8, p.Dark);
        px(17, 58 + tail, 5, 17, p.Dark);
        px(12, 51 + tail, 4, 3, p.Stripe);

        DrawBackBody(px, p, sit);
        DrawBackLegs(px, p, sit, walk);

        var y = 19 + bob;
        DrawBackHeadShape(px, p, y);
        switch (p.Pattern)
        {
            case "Calico":
                px(30, y + 17, 15, 17, p.Accent);
                px(55, y + 19, 11, 16, p.Accent2);
                px(36, 54, 14, 13, p.Accent);
                px(58, 56, 8, 10, p.Accent2);
                break;
            case "Point":
                px(27, y + 12, 12, 22, p.Accent);
                px(57, y + 12, 12, 22, p.Accent);
                px(55, 55, 10, 15, p.Accent2);
                break;
            case "Tuxedo":
                px(29, y + 17, 13, 28, p.Dark);
                px(55, y + 18, 10, 26, p.Dark);
                break;
            case "Snow":
                px(57, y + 22, 9, 18, p.Accent);
                px(34, 55, 16, 4, Brush("#FFFFFF", 0.28));
                break;
        }
    }

    private static void DrawSide(Action<double, double, double, double, Brush> px, Palette p, bool sit, int walk, int tail, int bob, bool blink)
    {
        DrawShadow(px, 18, 78, 62, 9);

        px(13, 58 + tail, 10, 19, p.Outline);
        px(7, 48 + tail, 12, 17, p.Outline);
        px(10, 50 + tail, 8, 12, p.Dark);
        px(17, 58 + tail, 5, 17, p.Base);
        px(10, 53 + tail, 5, 3, p.Stripe);
        px(18, 66 + tail, 3, 3, p.Stripe);

        DrawSideBody(px, p, sit);
        DrawSideLegs(px, p, sit, walk);
        DrawSideHead(px, p, 55, 21 + bob, blink);
        DrawSideCoat(px, p, 21 + bob);
    }

    private static void DrawLoaf(Action<double, double, double, double, Brush> px, Palette p, bool blink, int tail)
    {
        DrawShadow(px, 21, 76, 57, 9);
        px(18, 59, 60, 22, p.Outline);
        px(22, 56, 51, 22, p.Base);
        px(26, 58, 18, 15, p.Light);
        px(58, 61, 10, 12, p.Dark);
        px(28, 60, 11, 3, Brush("#FFFFFF", 0.18));
        px(63, 69, 5, 5, Brush("#120A08", 0.16));

        px(20, 41, 56, 28, p.Outline);
        px(27, 34, 10, 17, p.Outline);
        px(59, 34, 10, 17, p.Outline);
        px(29, 39, 8, 11, p.Ear);
        px(60, 39, 8, 11, p.Ear);
        px(27, 42, 44, 24, p.Base);
        px(31, 45, 17, 17, p.Light);
        px(60, 47, 8, 14, p.Dark);
        px(73, 63 + tail, 12, 8, p.Outline);
        px(77, 61 + tail, 13, 6, p.Dark);

        DrawLoafCoat(px, p);
        if (blink)
        {
            px(36, 52, 9, 2, p.Eye);
            px(56, 52, 9, 2, p.Eye);
        }
        else
        {
            px(37, 50, 7, 8, p.Eye);
            px(57, 50, 7, 8, p.Eye);
            px(38, 51, 2, 2, Brush("#FFFFFF"));
            px(58, 51, 2, 2, Brush("#FFFFFF"));
        }

        px(49, 61, 6, 3, p.Nose);
        px(40, 62, 5, 3, Brush("#F7A0B8", 0.38));
        px(59, 62, 5, 3, Brush("#F7A0B8", 0.38));
    }

    private static void DrawFrontTail(Action<double, double, double, double, Brush> px, Palette p, int tail)
    {
        px(70, 55 + tail, 8, 23, p.Outline);
        px(77, 45 + tail, 8, 21, p.Outline);
        px(82, 36 + tail, 8, 15, p.Outline);
        px(72, 57 + tail, 5, 19, p.Dark);
        px(78, 47 + tail, 5, 16, p.Base);
        px(83, 38 + tail, 5, 11, p.Light);
        px(76, 63 + tail, 4, 4, p.Stripe);
        px(81, 51 + tail, 4, 4, p.Stripe);
    }

    private static void DrawFrontBody(Action<double, double, double, double, Brush> px, Palette p, bool sit)
    {
        var y = sit ? 47 : 53;
        var h = sit ? 32 : 26;
        px(25, y + 4, 45, h - 4, p.Outline);
        px(29, y, 37, h, p.Outline);
        px(31, y + 3, 33, h - 4, p.Base);
        px(34, y + 7, 15, h - 10, p.Light);
        px(55, y + 8, 7, h - 11, p.Dark);
        px(40, y + 14, 16, 12, p.Chest);
        px(34, y + 4, 8, 3, Brush("#FFFFFF", 0.18));
        px(43, y + 21, 11, 3, Brush("#FFFFFF", 0.16));
        DrawBodyPattern(px, p, y, h);
    }

    private static void DrawFrontLegs(Action<double, double, double, double, Brush> px, Palette p, bool sit, int walk)
    {
        if (sit)
        {
            px(25, 74, 17, 15, p.Outline);
            px(54, 74, 17, 15, p.Outline);
            px(29, 74, 10, 11, p.Light);
            px(57, 74, 10, 11, p.Light);
            DrawToes(px, p, 31, 86);
            DrawToes(px, p, 59, 86);
            return;
        }

        var left = walk == 1 ? -4 : walk == 3 ? 2 : 0;
        var right = walk == 3 ? -4 : walk == 1 ? 2 : 0;
        px(28, 75 + left, 14, 14, p.Outline);
        px(55, 75 + right, 14, 14, p.Outline);
        px(31, 75 + left, 8, 10, p.Light);
        px(58, 75 + right, 8, 10, p.Light);
        DrawToes(px, p, 32, 86 + left);
        DrawToes(px, p, 59, 86 + right);
    }

    private static void DrawFrontHead(Action<double, double, double, double, Brush> px, Palette p, int bob)
    {
        var y = 18 + bob;
        px(24, y + 3, 13, 23, p.Outline);
        px(59, y + 3, 13, 23, p.Outline);
        px(27, y + 8, 8, 15, p.Ear);
        px(61, y + 8, 8, 15, p.Ear);
        px(29, y + 12, 4, 8, p.Chest);
        px(63, y + 12, 4, 8, p.Chest);
        px(33, y, 31, 9, p.Outline);
        px(20, y + 17, 56, 34, p.Outline);
        px(26, y + 9, 44, 43, p.Base);
        px(31, y + 13, 18, 32, p.Light);
        px(61, y + 16, 7, 26, p.Dark);
        px(41, y + 47, 20, 7, p.Outline);
        px(43, y + 47, 16, 5, p.Chest);
        px(28, y + 18, 9, 5, Brush("#FFFFFF", 0.18));
        px(69, y + 31, 3, 13, Brush("#120A08", 0.14));
        if (p.HasStripes)
        {
            px(39, y + 9, 5, 10, p.Stripe);
            px(52, y + 9, 5, 10, p.Stripe);
            px(46, y + 7, 5, 8, p.Stripe);
            px(28, y + 26, 6, 4, p.Stripe);
            px(64, y + 26, 6, 4, p.Stripe);
        }
    }

    private static void DrawFrontFace(Action<double, double, double, double, Brush> px, Palette p, int bob, bool blink)
    {
        var y = 18 + bob;
        if (blink)
        {
            px(36, y + 32, 11, 2, p.Eye);
            px(56, y + 32, 11, 2, p.Eye);
        }
        else
        {
            px(36, y + 28, 8, 12, p.Eye);
            px(57, y + 28, 8, 12, p.Eye);
            px(38, y + 30, 2, 3, Brush("#FFFFFF"));
            px(59, y + 30, 2, 3, Brush("#FFFFFF"));
            px(41, y + 38, 2, 1, Brush("#4D1B07", 0.28));
            px(62, y + 38, 2, 1, Brush("#4D1B07", 0.28));
        }

        px(49, y + 41, 7, 4, p.Nose);
        px(51, y + 42, 3, 1, Brush("#FFFFFF", 0.24));
        px(45, y + 46, 4, 3, p.Outline);
        px(56, y + 46, 4, 3, p.Outline);
        px(51, y + 45, 3, 5, p.Outline);
        px(39, y + 46, 5, 3, Brush("#F7A0B8", 0.42));
        px(61, y + 46, 5, 3, Brush("#F7A0B8", 0.42));
        px(23, y + 39, 13, 2, p.Whisker);
        px(67, y + 39, 13, 2, p.Whisker);
        px(22, y + 47, 13, 2, p.Whisker);
        px(68, y + 47, 13, 2, p.Whisker);
        px(25, y + 35, 10, 1, p.Whisker);
        px(68, y + 35, 10, 1, p.Whisker);
    }

    private static void DrawFrontCoat(Action<double, double, double, double, Brush> px, Palette p, bool sit, int bob)
    {
        var y = 18 + bob;
        switch (p.Pattern)
        {
            case "Calico":
                px(27, y + 17, 16, 18, p.Accent);
                px(57, y + 18, 11, 16, p.Accent2);
                px(31, y + 21, 6, 4, p.AccentLight);
                px(42, y + 8, 10, 7, p.Accent);
                break;
            case "Point":
                px(25, y + 11, 13, 20, p.Accent);
                px(59, y + 11, 13, 20, p.Accent);
                px(59, y + 32, 8, 15, p.Accent2);
                break;
            case "Tuxedo":
                px(27, y + 17, 14, 29, p.Dark);
                px(58, y + 18, 9, 26, p.Dark);
                px(43, y + 37, 16, 10, p.Chest);
                break;
            case "Snow":
                px(36, y + 17, 18, 8, Brush("#FFFFFF", 0.32));
                px(60, y + 23, 7, 20, p.Accent);
                break;
        }

        if (sit)
        {
            px(39, 72, 18, 3, Brush("#FFFFFF", 0.14));
        }
    }

    private static void DrawBackBody(Action<double, double, double, double, Brush> px, Palette p, bool sit)
    {
        var y = sit ? 49 : 55;
        var h = sit ? 31 : 25;
        px(25, y + 4, 45, h - 4, p.Outline);
        px(29, y, 37, h, p.Outline);
        px(31, y + 3, 33, h - 4, p.Base);
        px(35, y + 7, 11, h - 10, p.Light);
        px(56, y + 8, 8, h - 11, p.Dark);
        px(45, y + 4, 7, h - 8, p.Stripe);
        px(35, y + 5, 7, 3, Brush("#FFFFFF", 0.16));
        DrawBodyPattern(px, p, y, h);
    }

    private static void DrawBackLegs(Action<double, double, double, double, Brush> px, Palette p, bool sit, int walk)
    {
        if (sit)
        {
            px(25, 75, 17, 14, p.Outline);
            px(54, 75, 17, 14, p.Outline);
            px(29, 75, 10, 10, p.Base);
            px(57, 75, 10, 10, p.Dark);
            DrawToes(px, p, 31, 86);
            DrawToes(px, p, 59, 86);
            return;
        }

        var left = walk == 1 ? -4 : walk == 3 ? 2 : 0;
        var right = walk == 3 ? -4 : walk == 1 ? 2 : 0;
        px(28, 76 + left, 14, 13, p.Outline);
        px(55, 76 + right, 14, 13, p.Outline);
        px(31, 76 + left, 8, 9, p.Base);
        px(58, 76 + right, 8, 9, p.Dark);
        DrawToes(px, p, 32, 86 + left);
        DrawToes(px, p, 59, 86 + right);
    }

    private static void DrawBackHeadShape(Action<double, double, double, double, Brush> px, Palette p, double y)
    {
        px(24, y + 3, 13, 23, p.Outline);
        px(59, y + 3, 13, 23, p.Outline);
        px(27, y + 8, 8, 15, p.Ear);
        px(61, y + 8, 8, 15, p.Ear);
        px(33, y, 31, 9, p.Outline);
        px(20, y + 17, 56, 34, p.Outline);
        px(26, y + 9, 44, 43, p.Base);
        px(31, y + 14, 16, 31, p.Light);
        px(61, y + 17, 7, 25, p.Dark);
        px(41, y + 47, 20, 7, p.Outline);
        px(45, y + 47, 12, 5, p.Dark);
        px(28, y + 18, 9, 5, Brush("#FFFFFF", 0.16));
        if (p.HasStripes)
        {
            px(38, y + 11, 6, 15, p.Stripe);
            px(52, y + 11, 6, 15, p.Stripe);
            px(46, y + 9, 6, 19, p.Stripe);
            px(28, y + 32, 6, 10, p.Stripe);
            px(63, y + 32, 6, 10, p.Stripe);
        }
    }

    private static void DrawSideBody(Action<double, double, double, double, Brush> px, Palette p, bool sit)
    {
        var y = sit ? 53 : 58;
        var h = sit ? 27 : 22;
        px(22, y + 4, 49, h - 4, p.Outline);
        px(27, y, 40, h, p.Outline);
        px(30, y + 3, 34, h - 4, p.Base);
        px(34, y + 6, 16, h - 9, p.Light);
        px(58, y + 7, 7, h - 10, p.Dark);
        px(51, y + 13, 12, 7, p.Chest);
        px(35, y + 4, 10, 3, Brush("#FFFFFF", 0.18));
        DrawBodyPattern(px, p, y, h);
    }

    private static void DrawSideLegs(Action<double, double, double, double, Brush> px, Palette p, bool sit, int walk)
    {
        if (sit)
        {
            px(27, 75, 16, 14, p.Outline);
            px(56, 75, 15, 14, p.Outline);
            px(31, 75, 9, 10, p.Light);
            px(59, 75, 9, 10, p.Dark);
            DrawToes(px, p, 32, 86);
            DrawToes(px, p, 60, 86);
            return;
        }

        var back = walk == 3 ? -4 : walk == 1 ? 2 : 0;
        var front = walk == 1 ? -4 : walk == 3 ? 2 : 0;
        px(29, 76 + back, 14, 13, p.Outline);
        px(57, 76 + front, 14, 13, p.Outline);
        px(32, 76 + back, 8, 9, p.Light);
        px(60, 76 + front, 8, 9, p.Dark);
        DrawToes(px, p, 33, 86 + back);
        DrawToes(px, p, 61, 86 + front);
    }

    private static void DrawSideHead(Action<double, double, double, double, Brush> px, Palette p, double x, double y, bool blink)
    {
        px(x + 2, y + 4, 11, 21, p.Outline);
        px(x + 23, y + 9, 9, 17, p.Outline);
        px(x + 4, y + 9, 7, 14, p.Ear);
        px(x + 24, y + 13, 6, 11, p.Ear);
        px(x, y + 18, 33, 34, p.Outline);
        px(x + 4, y + 22, 25, 29, p.Base);
        px(x + 7, y + 26, 10, 22, p.Light);
        px(x + 23, y + 28, 6, 17, p.Dark);
        px(x + 27, y + 42, 9, 10, p.Outline);
        px(x + 29, y + 44, 6, 7, p.Chest);
        if (blink)
        {
            px(x + 19, y + 34, 8, 2, p.Eye);
        }
        else
        {
            px(x + 20, y + 30, 7, 11, p.Eye);
            px(x + 22, y + 32, 2, 3, Brush("#FFFFFF"));
        }

        px(x + 31, y + 43, 5, 3, p.Nose);
        px(x + 32, y + 48, 4, 3, p.Outline);
        px(x + 24, y + 46, 4, 3, Brush("#F7A0B8", 0.38));
        px(x + 31, y + 37, 12, 1, p.Whisker);
        px(x + 31, y + 46, 12, 1, p.Whisker);
        px(x + 31, y + 41, 13, 1, p.Whisker);
        if (p.HasStripes)
        {
            px(x + 8, y + 23, 5, 10, p.Stripe);
            px(x + 16, y + 23, 4, 9, p.Stripe);
        }
    }

    private static void DrawSideCoat(Action<double, double, double, double, Brush> px, Palette p, double headY)
    {
        switch (p.Pattern)
        {
            case "Calico":
                px(62, headY + 23, 14, 16, p.Accent);
                px(32, 61, 17, 13, p.Accent);
                px(56, 62, 9, 12, p.Accent2);
                break;
            case "Point":
                px(59, headY + 20, 11, 24, p.Accent);
                px(76, headY + 26, 7, 16, p.Accent2);
                px(58, 70, 11, 8, p.Accent);
                break;
            case "Tuxedo":
                px(59, headY + 24, 11, 22, p.Dark);
                px(60, 64, 8, 13, p.Dark);
                px(50, 70, 14, 5, p.Chest);
                break;
            case "Snow":
                px(76, headY + 29, 7, 14, p.Accent);
                px(35, 64, 14, 3, Brush("#FFFFFF", 0.30));
                break;
            default:
                if (p.HasStripes)
                {
                    px(35, 67, 5, 8, p.Stripe);
                    px(47, 67, 5, 8, p.Stripe);
                }
                break;
        }
    }

    private static void DrawBodyPattern(Action<double, double, double, double, Brush> px, Palette p, double y, double h)
    {
        switch (p.Pattern)
        {
            case "Calico":
                px(31, y + 4, 16, 12, p.Accent);
                px(57, y + 7, 8, 12, p.Accent2);
                px(37, y + 6, 6, 4, p.AccentLight);
                break;
            case "Point":
                px(57, y + 5, 8, h - 9, p.Accent2);
                px(31, y + h - 7, 10, 5, p.Accent);
                break;
            case "Tuxedo":
                px(39, y + 13, 19, 10, p.Chest);
                px(42, y + 14, 8, 3, Brush("#FFFFFF", 0.35));
                break;
            case "Snow":
                px(55, y + 7, 8, 9, p.Accent);
                px(34, y + 16, 14, 4, Brush("#FFFFFF", 0.28));
                break;
            default:
                if (p.HasStripes)
                {
                    px(35, y + 5, 5, 10, p.Stripe);
                    px(47, y + 5, 5, 10, p.Stripe);
                    px(59, y + h - 9, 5, 5, p.Stripe);
                }
                break;
        }
    }

    private static void DrawLoafCoat(Action<double, double, double, double, Brush> px, Palette p)
    {
        switch (p.Pattern)
        {
            case "Calico":
                px(28, 43, 15, 14, p.Accent);
                px(58, 44, 10, 13, p.Accent2);
                px(31, 59, 18, 10, p.Accent);
                break;
            case "Point":
                px(27, 42, 10, 18, p.Accent);
                px(60, 42, 10, 18, p.Accent);
                px(60, 61, 11, 13, p.Accent2);
                break;
            case "Tuxedo":
                px(28, 45, 12, 18, p.Dark);
                px(59, 45, 9, 16, p.Dark);
                px(46, 59, 15, 9, p.Chest);
                break;
            case "Snow":
                px(58, 47, 9, 15, p.Accent);
                px(31, 59, 14, 3, Brush("#FFFFFF", 0.28));
                break;
        }
    }

    private static void DrawToes(Action<double, double, double, double, Brush> px, Palette p, double x, double y)
    {
        px(x, y, 2, 1, p.Outline);
        px(x + 4, y, 2, 1, p.Outline);
        px(x + 8, y, 2, 1, p.Outline);
    }

    private static void DrawShadow(Action<double, double, double, double, Brush> px, double x, double y, double w, double h)
    {
        px(x, y, w, h - 2, Brush("#425A61", 0.22));
        px(x + 5, y + 2, w - 10, h - 1, Brush("#6F8991", 0.38));
        px(x + 15, y + 5, w - 30, 3, Brush("#B7CDD2", 0.16));
    }

    private static Brush Brush(string color, double opacity = 1)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!);
        brush.Opacity = opacity;
        brush.Freeze();
        return brush;
    }

    private sealed record Palette(
        Brush Outline,
        Brush Base,
        Brush Light,
        Brush Dark,
        Brush Chest,
        Brush Ear,
        Brush Eye,
        Brush Nose,
        Brush Whisker,
        Brush Stripe,
        Brush Accent,
        Brush Accent2,
        Brush AccentLight,
        bool HasStripes,
        string Pattern)
    {
        public static Palette For(string variant)
        {
            return variant switch
            {
                "Black" => new Palette(
                    Brush("#140A08"), Brush("#2A2527"), Brush("#4A4246"), Brush("#0B090A"),
                    Brush("#EFEFEF"), Brush("#302A2D"), Brush("#91E86F"), Brush("#42272F"), Brush("#140A08"), Brush("#191415"),
                    Brush("#0F0D0E"), Brush("#5C565A"), Brush("#6A6266"), false, "Tuxedo"),
                "Calico" => new Palette(
                    Brush("#4C1705"), Brush("#F5F0DC"), Brush("#FFFFFF"), Brush("#B86B38"),
                    Brush("#FFE7B0"), Brush("#D66F28"), Brush("#3B2718"), Brush("#FF7FA3"), Brush("#4C1705"), Brush("#6B341A"),
                    Brush("#E7832E"), Brush("#6B341A"), Brush("#FFBE6F"), true, "Calico"),
                "White" => new Palette(
                    Brush("#3C302A"), Brush("#EDE9D8"), Brush("#FFFFFF"), Brush("#BDB8AA"),
                    Brush("#FFF8E8"), Brush("#F4F0DE"), Brush("#1A1A1A"), Brush("#E98AA0"), Brush("#3C302A"), Brush("#D3CEBD"),
                    Brush("#C8C2B4"), Brush("#AFA89B"), Brush("#FFFFFF"), false, "Snow"),
                "Gray" => new Palette(
                    Brush("#151515"), Brush("#686868"), Brush("#D8D8D8"), Brush("#444444"),
                    Brush("#EFEFEF"), Brush("#BDBDBD"), Brush("#111111"), Brush("#6E6E6E"), Brush("#2B2B2B"), Brush("#3A3A3A"),
                    Brush("#4B4B4B"), Brush("#2F2F2F"), Brush("#BEBEBE"), true, "Tabby"),
                "Cream" => new Palette(
                    Brush("#4C1705"), Brush("#F7C477"), Brush("#FFF8B8"), Brush("#7B594D"),
                    Brush("#F5E9B1"), Brush("#FFF8B8"), Brush("#5B240A"), Brush("#7A4937"), Brush("#4C1705"), Brush("#845944"),
                    Brush("#7B594D"), Brush("#5E4038"), Brush("#FFF8B8"), false, "Point"),
                _ => new Palette(
                    Brush("#4C1705"), Brush("#F5A321"), Brush("#FFD07A"), Brush("#7A2A05"),
                    Brush("#FFD61A"), Brush("#FFD07A"), Brush("#552000"), Brush("#FF6D9A"), Brush("#4C1705"), Brush("#D66F00"),
                    Brush("#E57700"), Brush("#9B3900"), Brush("#FFE19A"), true, "Tabby"),
            };
        }
    }
}
