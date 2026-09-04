using System.Net.Http;

namespace SteamFun.Ui;

internal static class UiTheme
{
    public static readonly Color CardBack = Color.FromArgb(236, 236, 236);
    public static readonly Color HeaderBack = Color.FromArgb(55, 58, 64);
    public static readonly Color HeaderFore = Color.White;
    public static readonly Color Caption = Color.FromArgb(90, 90, 90);
    public static readonly Color Value = Color.FromArgb(20, 20, 20);
    public static readonly Color AccentRed = Color.FromArgb(180, 35, 35);
    public static readonly Color Opened = Color.FromArgb(220, 245, 220);
    public static readonly Color OpenedBorder = Color.FromArgb(40, 120, 50);
    public static readonly Color Revoked = Color.FromArgb(255, 220, 220);
    public static readonly Color RevokedBorder = Color.FromArgb(180, 35, 35);
    public static readonly Color Closed = Color.FromArgb(245, 245, 245);
    public static readonly Color ClosedBorder = Color.FromArgb(170, 170, 170);
    public static readonly Color PenaltyBack = Color.FromArgb(255, 228, 228);

    public static Font TitleFont { get; } = new("Segoe UI Semibold", 14f, FontStyle.Bold);
    public static Font SubFont { get; } = new("Segoe UI", 8.5f);
    public static Font CaptionFont { get; } = new("Segoe UI", 8f);
    public static Font ValueFont { get; } = new("Segoe UI Semibold", 10f, FontStyle.Bold);
    public static Font SmallFont { get; } = new("Segoe UI", 9f);
}

internal static class FieldFactory
{
    public static (Panel Row, Label Value) Create(string caption, int width = 0)
    {
        var row = new Panel
        {
            Height = 36,
            Width = width > 0 ? width : 280,
            Margin = new Padding(0, 0, 8, 4),
        };

        var cap = new Label
        {
            Text = caption,
            Font = UiTheme.CaptionFont,
            ForeColor = UiTheme.Caption,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 14,
        };

        var val = new Label
        {
            Text = "—",
            Font = UiTheme.ValueFont,
            ForeColor = UiTheme.Value,
            AutoSize = false,
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
        };

        row.Controls.Add(val);
        row.Controls.Add(cap);
        return (row, val);
    }
}

internal static class AvatarLoader
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    static AvatarLoader()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("SteamFun/0.1");
    }

    public static async Task<Image?> LoadAsync(string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            await using var stream = await Http.GetStreamAsync(url, ct).ConfigureAwait(false);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
            ms.Position = 0;
            using var tmp = Image.FromStream(ms);
            return new Bitmap(tmp);
        }
        catch
        {
            return null;
        }
    }
}
