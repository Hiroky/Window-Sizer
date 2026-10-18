namespace WindowSizer;

/// <summary>解像度プリセット。幅と高さがどちらも 0 の項目は手入力（カスタム）を表す。</summary>
internal sealed record DisplayPreset(int Width, int Height, string Name, string AspectRatio)
{
    /// <summary>プリセットに一致しない手入力の状態を表す項目。</summary>
    public static readonly DisplayPreset Custom = new(0, 0, "カスタム", string.Empty);

    public bool IsCustom => Width == 0 && Height == 0;

    public override string ToString() =>
        IsCustom ? Name : $"{Width} x {Height}  {Name} / {AspectRatio}";
}

/// <summary>一般的なディスプレイの解像度一覧（幅の小さい順）。</summary>
internal static class DisplayPresets
{
    public static readonly IReadOnlyList<DisplayPreset> All =
    [
        new(640, 480, "VGA", "4:3"),
        new(800, 600, "SVGA", "4:3"),
        new(1024, 768, "XGA", "4:3"),
        new(1280, 720, "HD (720p)", "16:9"),
        new(1280, 800, "WXGA", "16:10"),
        new(1280, 1024, "SXGA", "5:4"),
        new(1366, 768, "WXGA（ノート PC 向け）", "16:9"),
        new(1440, 900, "WXGA+", "16:10"),
        new(1536, 864, "フル HD の 125% 表示", "16:9"),
        new(1600, 900, "HD+", "16:9"),
        new(1600, 1200, "UXGA", "4:3"),
        new(1680, 1050, "WSXGA+", "16:10"),
        new(1920, 1080, "フル HD (1080p)", "16:9"),
        new(1920, 1200, "WUXGA", "16:10"),
        new(2560, 1080, "UW-FHD", "21:9"),
        new(2560, 1440, "WQHD (1440p)", "16:9"),
        new(2560, 1600, "WQXGA", "16:10"),
        new(3440, 1440, "UWQHD", "21:9"),
        new(3840, 2160, "4K UHD", "16:9"),
        new(5120, 1440, "DQHD", "32:9"),
        new(5120, 2880, "5K", "16:9"),
        new(7680, 4320, "8K UHD", "16:9"),
    ];

    /// <summary>幅と高さが一致するプリセットを返す。無ければ null。</summary>
    public static DisplayPreset? Find(int width, int height) =>
        All.FirstOrDefault(p => p.Width == width && p.Height == height);
}
