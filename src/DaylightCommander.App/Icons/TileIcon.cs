using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace DaylightCommander.App.Icons;

/// <summary>
/// 격자 보기용 큰 아이콘. 작은 목록 아이콘(<see cref="DuoIcon"/>)과 달리 크게 그려도 허전하지 않도록
/// 폴더(뒷판·앞판)·문서 용지(접힌 모서리·본문 줄)·앱 창(제목줄) 세 모양에 위아래 옅은 그라데이션, 고정 굵기의 얇은 선,
/// 아래쪽 그림자를 더하고, 종류별 색의 확장자 배지를 얹는다. 색은 <see cref="ColorKey"/>의 앱 색 자원 하나에서 파생해
/// 라이트·다크·팔레트 변경을 따른다. 모양은 96×96 격자에 그린다.
/// </summary>
public sealed class TileIcon : Control
{
    public static readonly StyledProperty<string?> IconProperty = AvaloniaProperty.Register<TileIcon, string?>(nameof(Icon));
    public static readonly StyledProperty<string?> ColorKeyProperty = AvaloniaProperty.Register<TileIcon, string?>(nameof(ColorKey));
    public static readonly StyledProperty<string?> BadgeProperty = AvaloniaProperty.Register<TileIcon, string?>(nameof(Badge));
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<TileIcon>();

    const double Grid = 96;
    /// <summary>이보다 작게 그려질 때는 배지 글자를 읽을 수 없어 배지 대신 본문 줄에 종류 색을 입힌다.</summary>
    const double BadgeMinSide = 80;

    static readonly BoxShadows Shadow = new(new BoxShadow { OffsetY = 3, Blur = 5, Color = Color.FromArgb(0x59, 0, 0, 0) });
    static readonly Geometry PaperShape = Geometry.Parse("M22 10h34l18 18v52a4 4 0 0 1-4 4H22a4 4 0 0 1-4-4V14a4 4 0 0 1 4-4z");
    static readonly Geometry PaperFold = Geometry.Parse("M56 10v14a4 4 0 0 0 4 4h14z");
    static readonly Geometry FolderBack = Geometry.Parse("M10 24a5 5 0 0 1 5-5h20l8 8h38a5 5 0 0 1 5 5v40a5 5 0 0 1-5 5H15a5 5 0 0 1-5-5z");
    static readonly Geometry FolderFront = Geometry.Parse("M8 40a5 5 0 0 1 5-5h70a5 5 0 0 1 5 5l-3 38a5 5 0 0 1-5 4.6H16a5 5 0 0 1-5-4.6z");
    static readonly Geometry WindowBar = Geometry.Parse("M10 25a7 7 0 0 1 7-7h62a7 7 0 0 1 7 7v10H10z");

    static TileIcon()
    {
        AffectsRender<TileIcon>(IconProperty, BadgeProperty, ForegroundProperty);
        ColorKeyProperty.Changed.AddClassHandler<TileIcon>((icon, _) => icon.BindColorKey());
    }

    public string? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string? ColorKey { get => GetValue(ColorKeyProperty); set => SetValue(ColorKeyProperty, value); }
    public string? Badge { get => GetValue(BadgeProperty); set => SetValue(BadgeProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    IDisposable? _colorBinding;

    void BindColorKey()
    {
        _colorBinding?.Dispose();
        _colorBinding = null;
        if (ColorKey is { Length: > 0 } key && Application.Current is { } app)
            _colorBinding = this.Bind(ForegroundProperty, app.GetResourceObservable(key));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEffectivelyEnabledProperty) InvalidateVisual();
    }

    static Color Mix(Color from, Color to, double amount) => Color.FromArgb(
        255,
        (byte)Math.Round(from.R + (to.R - from.R) * amount),
        (byte)Math.Round(from.G + (to.G - from.G) * amount),
        (byte)Math.Round(from.B + (to.B - from.B) * amount));

    static LinearGradientBrush Vertical(Color top, Color bottom) => new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops = [new GradientStop(top, 0), new GradientStop(bottom, 1)],
    };

    public override void Render(DrawingContext context)
    {
        var side = Math.Min(Bounds.Width, Bounds.Height);
        if (side <= 0 || (Foreground as ISolidColorBrush)?.Color is not { } accent) return;

        var scale = side / Grid;
        using var move = context.PushTransform(
            Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation((Bounds.Width - side) / 2, (Bounds.Height - side) / 2));
        using var enabled = context.PushOpacity(IsEffectivelyEnabled ? 1 : 0.4);
        var showBadge = side >= BadgeMinSide;

        switch (Icon)
        {
            case "folder": DrawFolder(context, accent); break;
            case "file-app": DrawWindow(context, accent, showBadge); break;
            default: DrawPaper(context, accent, showBadge); break;
        }
    }

    static void DrawFolder(DrawingContext context, Color accent)
    {
        var light = Mix(accent, Colors.White, 0.35);
        var dark = Mix(accent, Colors.Black, 0.3);
        context.DrawRectangle(Brushes.Transparent, null, new RoundedRect(new Rect(8, 35, 80, 48), 5), Shadow);
        context.DrawGeometry(Vertical(Mix(accent, Colors.Black, 0.2), Mix(accent, Colors.Black, 0.38)), null, FolderBack);
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xE6, 0xF7, 0xF3, 0xE8)), null, new RoundedRect(new Rect(14, 31, 68, 12), 2));
        context.DrawGeometry(Vertical(light, accent), new Pen(new SolidColorBrush(Color.FromArgb(0x99, dark.R, dark.G, dark.B)), 1.5, lineJoin: PenLineJoin.Round), FolderFront);
        context.DrawLine(new Pen(new SolidColorBrush(Mix(accent, Colors.White, 0.7)), 1.5, lineCap: PenLineCap.Round), new Point(13, 36.5), new Point(83, 36.5));
    }

    void DrawPaper(DrawingContext context, Color accent, bool showBadge)
    {
        var edge = Color.Parse("#6F93C2");
        context.DrawRectangle(Brushes.Transparent, null, new RoundedRect(new Rect(18, 10, 56, 74), 4), Shadow);
        context.DrawGeometry(Vertical(Color.Parse("#E4EEFA"), Color.Parse("#A9C3E4")), new Pen(new SolidColorBrush(edge), 1.5, lineJoin: PenLineJoin.Round), PaperShape);
        context.DrawGeometry(new SolidColorBrush(Color.Parse("#7FA0CC")), null, PaperFold);
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0x80, 255, 255, 255)), 1), new Point(56, 10), new Point(74, 28));

        var lines = new Pen(new SolidColorBrush(showBadge ? edge : accent), 2.5, lineCap: PenLineCap.Round);
        using var faint = context.PushOpacity(showBadge ? 0.8 : 0.95);
        context.DrawLine(lines, new Point(28, 40), new Point(56, 40));
        context.DrawLine(lines, new Point(28, 48), new Point(64, 48));
        context.DrawLine(lines, new Point(28, 56), new Point(50, 56));
        if (!showBadge) { context.DrawLine(lines, new Point(28, 64), new Point(58, 64)); return; }
        DrawBadge(context, accent, left: 12, top: 64);
    }

    void DrawWindow(DrawingContext context, Color accent, bool showBadge)
    {
        var edge = Color.Parse("#6E7A90");
        context.DrawRectangle(Brushes.Transparent, null, new RoundedRect(new Rect(10, 18, 76, 60), 7), Shadow);
        context.DrawRectangle(Vertical(Color.Parse("#D9DEE8"), Color.Parse("#9AA5B8")), new Pen(new SolidColorBrush(edge), 1.5), new RoundedRect(new Rect(10, 18, 76, 60), 7));
        context.DrawGeometry(new SolidColorBrush(Color.Parse("#5C6A82")), null, WindowBar);
        context.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0x59, 255, 255, 255)), 1), new Point(10.8, 36), new Point(85.2, 36));
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#FF8A80")), null, new Point(22, 27), 3, 3);
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#FFD36E")), null, new Point(32, 27), 3, 3);
        context.DrawEllipse(new SolidColorBrush(Color.Parse("#7FD99A")), null, new Point(42, 27), 3, 3);
        var body = new SolidColorBrush(Color.FromArgb(0x8C, edge.R, edge.G, edge.B));
        context.DrawRectangle(body, null, new RoundedRect(new Rect(18, 44, 30, 5), 2.5));
        context.DrawRectangle(body, null, new RoundedRect(new Rect(18, 54, 46, 5), 2.5));
        if (showBadge) DrawBadge(context, accent, left: null, top: 62, right: 90);
    }

    /// <summary>확장자 배지: 종류 색을 어둡게 한 바탕에 흰 글자. 폭은 글자에 맞춘다. <paramref name="left"/>가 없으면 <paramref name="right"/>에 붙인다.</summary>
    void DrawBadge(DrawingContext context, Color accent, double? left, double top, double right = 0)
    {
        if (string.IsNullOrEmpty(Badge)) return;
        var text = new FormattedText(Badge, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Medium), 12, Brushes.White);
        var width = Math.Max(28, text.Width + 14);
        var x = left ?? right - width;
        var fill = Mix(accent, Colors.Black, 0.35);
        context.DrawRectangle(new SolidColorBrush(fill), null, new RoundedRect(new Rect(x, top, width, 20), 4));
        context.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x24, 255, 255, 255)), null, new RoundedRect(new Rect(x, top, width, 8), 4));
        context.DrawText(text, new Point(x + (width - text.Width) / 2, top + (20 - text.Height) / 2));
    }
}
