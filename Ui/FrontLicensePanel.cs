using SteamFun.Domain;

namespace SteamFun.Ui;

internal sealed class FrontLicensePanel : UserControl
{
    private readonly PictureBox _avatar;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _serial;
    private readonly Label _classLabel;
    private readonly Label _signature;
    private readonly Label _barcode;
    private readonly Label _marks;
    private readonly FlowLayoutPanel _fields;
    private readonly FlowLayoutPanel _categories;
    private readonly Dictionary<string, Label> _values = new();

    public FrontLicensePanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(210, 212, 216);
        AutoScroll = true;
        Padding = new Padding(16);

        var card = new Panel
        {
            Width = 820,
            Height = 560,
            BackColor = UiTheme.CardBack,
            Padding = new Padding(0),
            Location = new Point(16, 16),
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(120, 120, 120));
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 58,
            BackColor = UiTheme.HeaderBack,
            Padding = new Padding(14, 8, 14, 8),
        };

        _title = new Label
        {
            Text = "УДОСТОВЕРЕНИЕ ГЕЙМЕРА · ГАБДД",
            Font = UiTheme.TitleFont,
            ForeColor = UiTheme.HeaderFore,
            AutoSize = true,
            Location = new Point(14, 6),
        };
        _subtitle = new Label
        {
            Text = "Габеновская инспекция безопасности игрового движения",
            Font = UiTheme.SubFont,
            ForeColor = Color.FromArgb(200, 200, 200),
            AutoSize = true,
            Location = new Point(14, 32),
        };
        _serial = new Label
        {
            Text = "серия STEAM",
            Font = UiTheme.SmallFont,
            ForeColor = Color.FromArgb(230, 230, 230),
            AutoSize = true,
            TextAlign = ContentAlignment.TopRight,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        header.Controls.Add(_title);
        header.Controls.Add(_subtitle);
        header.Controls.Add(_serial);
        header.Resize += (_, _) =>
        {
            _serial.Location = new Point(header.Width - _serial.PreferredWidth - 16, 10);
        };

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };

        var left = new Panel { Dock = DockStyle.Left, Width = 170, Padding = new Padding(0, 4, 12, 0) };
        _avatar = new PictureBox
        {
            Width = 140,
            Height = 140,
            BorderStyle = BorderStyle.FixedSingle,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
            Location = new Point(8, 8),
        };
        _classLabel = new Label
        {
            Text = "КЛАСС: —",
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            AutoSize = false,
            Width = 140,
            Height = 22,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(8, 156),
        };
        var sigCap = new Label
        {
            Text = "ПОДПИСЬ ВЛАДЕЛЬЦА",
            Font = UiTheme.CaptionFont,
            ForeColor = UiTheme.Caption,
            AutoSize = true,
            Location = new Point(8, 190),
        };
        _signature = new Label
        {
            Text = "—",
            Font = new Font("Segoe Script", 14f, FontStyle.Italic),
            ForeColor = UiTheme.Value,
            AutoSize = false,
            Width = 140,
            Height = 36,
            Location = new Point(8, 208),
        };
        left.Controls.Add(_avatar);
        left.Controls.Add(_classLabel);
        left.Controls.Add(sigCap);
        left.Controls.Add(_signature);

        _fields = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = false,
            Padding = new Padding(4, 0, 0, 0),
        };

        AddField("1. НИКНЕЙМ", 250);
        AddField("2. ГРАЖДАНСТВО", 250);
        AddField("3. ЗА. ВЫДАНО", 160);
        AddField("4. ДЕЙСТВИТЕЛЬНО ДО", 180);
        AddField("5. СТАЖ ВОЖДЕНИЯ", 160);
        AddField("6. МЕСТО ЖИТЕЛЬСТВА", 340);
        AddField("7. ТРАНСМИССИЯ", 340);
        AddField("8. ГРУППА КРОВИ", 200);
        AddField("9. ПАРК СРЕДСТВ", 200);
        AddField("10. ПРОБЕГ ОБЩИЙ", 220);
        AddField("11. ВАС-УЧЁТ", 180);

        var marksPanel = new Panel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(4, 4, 4, 0) };
        var marksCap = new Label
        {
            Text = "12. ОСОБЫЕ ПРИМЕТЫ",
            Font = UiTheme.CaptionFont,
            ForeColor = UiTheme.Caption,
            Dock = DockStyle.Top,
            Height = 14,
        };
        _marks = new Label
        {
            Text = "—",
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.Value,
            Dock = DockStyle.Fill,
            AutoEllipsis = false,
        };
        marksPanel.Controls.Add(_marks);
        marksPanel.Controls.Add(marksCap);

        var right = new Panel { Dock = DockStyle.Fill };
        right.Controls.Add(_fields);
        right.Controls.Add(marksPanel);

        var mid = new Panel { Dock = DockStyle.Fill };
        mid.Controls.Add(right);
        mid.Controls.Add(left);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 150, Padding = new Padding(14, 0, 14, 10) };
        var catCap = new Label
        {
            Text = "КАТЕГОРИИ",
            Font = UiTheme.CaptionFont,
            ForeColor = UiTheme.Caption,
            Dock = DockStyle.Top,
            Height = 18,
        };
        _categories = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = true,
            AutoScroll = true,
            Padding = new Padding(0, 2, 0, 0),
        };
        _barcode = new Label
        {
            Text = "Steam64ID: —",
            Font = new Font("Consolas", 9f),
            Dock = DockStyle.Bottom,
            Height = 22,
            ForeColor = UiTheme.Value,
        };
        bottom.Controls.Add(_categories);
        bottom.Controls.Add(_barcode);
        bottom.Controls.Add(catCap);

        body.Controls.Add(mid);
        card.Controls.Add(body);
        card.Controls.Add(bottom);
        card.Controls.Add(header);

        // order: bottom docked first visually under fill... WinForms dock order matters.
        // Rebuild dock: header top, bottom bottom, body fill
        card.Controls.Clear();
        card.Controls.Add(body);   // fill
        card.Controls.Add(bottom); // bottom
        card.Controls.Add(header); // top

        Controls.Add(card);
    }

    private void AddField(string caption, int width)
    {
        var (row, value) = FieldFactory.Create(caption, width);
        _fields.Controls.Add(row);
        _values[caption] = value;
    }

    public async Task BindAsync(GamerLicense license, CancellationToken ct = default)
    {
        var p = license.Profile;
        _serial.Text = $"серия STEAM\n№ GL {Serial(p.SteamId64)}";
        _classLabel.Text = $"КЛАСС: STEAM LVL {p.SteamLevel}";
        _signature.Text = p.PersonaName;
        _barcode.Text = $"|||||||||||  {p.SteamId64}  |||||||||||";

        Set("1. НИКНЕЙМ", p.PersonaName);
        Set("2. ГРАЖДАНСТВО", license.Citizenship);
        Set("3. ЗА. ВЫДАНО", FormatDate(p.AccountCreated));
        Set("4. ДЕЙСТВИТЕЛЬНО ДО", license.ValidUntil);
        Set("5. СТАЖ ВОЖДЕНИЯ", FormatTenure(p.AccountCreated));
        Set("6. МЕСТО ЖИТЕЛЬСТВА", license.Residence);
        Set("7. ТРАНСМИССИЯ", license.Transmission);
        Set("8. ГРУППА КРОВИ", license.BloodType);
        Set("9. ПАРК СРЕДСТВ", $"{license.FleetSize} ед. (без запуска: {license.NeverPlayedCount})");
        Set("10. ПРОБЕГ ОБЩИЙ", $"{license.TotalHours:0.#} ч  ·  14 дн.: {license.HoursLast14Days:0.#} ч");
        Set("11. ВАС-УЧЁТ", p.VacBanned ? $"состоит ({p.NumberOfVacBans} VAC)" : "не состоит");
        _marks.Text = license.SpecialMarks ?? "—";

        _categories.Controls.Clear();
        foreach (var c in license.Categories)
            _categories.Controls.Add(CreateCategoryChip(c));

        var old = _avatar.Image;
        _avatar.Image = null;
        old?.Dispose();
        var img = await AvatarLoader.LoadAsync(p.AvatarFullUrl, ct);
        if (img is not null)
            _avatar.Image = img;
    }

    private void Set(string key, string? value)
    {
        if (_values.TryGetValue(key, out var label))
            label.Text = string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    private static Control CreateCategoryChip(GameCategory c)
    {
        var (back, border, fore) = c.Status switch
        {
            CategoryStatus.Opened => (UiTheme.Opened, UiTheme.OpenedBorder, UiTheme.OpenedBorder),
            CategoryStatus.Revoked => (UiTheme.Revoked, UiTheme.RevokedBorder, UiTheme.AccentRed),
            _ => (UiTheme.Closed, UiTheme.ClosedBorder, UiTheme.Caption),
        };

        var chip = new Label
        {
            AutoSize = false,
            Width = 118,
            Height = 28,
            Margin = new Padding(2),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            Text = $"{c.Code}. {c.TitleRu}",
            BackColor = back,
            ForeColor = fore,
            Padding = new Padding(2),
        };
        chip.Paint += (_, e) =>
        {
            using var pen = new Pen(border);
            e.Graphics.DrawRectangle(pen, 0, 0, chip.Width - 1, chip.Height - 1);
        };
        return chip;
    }

    private static string FormatDate(DateTimeOffset? dt) =>
        dt is null ? "—" : dt.Value.ToLocalTime().ToString("dd.MM.yyyy");

    private static string FormatTenure(DateTimeOffset? created)
    {
        if (created is null) return "—";
        var span = DateTimeOffset.Now - created.Value;
        var years = (int)(span.TotalDays / 365.25);
        var months = (int)((span.TotalDays - years * 365.25) / 30.44);
        return $"{years} л. {months} мес.";
    }

    private static string Serial(string steamId64)
    {
        if (steamId64.Length < 8) return steamId64;
        var tail = steamId64[^8..];
        return $"{tail[..4]} {tail[4..]}";
    }
}
