using SteamFun.Domain;

namespace SteamFun.Ui;

internal sealed class BackLicensePanel : UserControl
{
    private readonly FlowLayoutPanel _categoryRows;
    private readonly FlowLayoutPanel _violations;
    private readonly FlowLayoutPanel _notes;
    private readonly FlowLayoutPanel _medical;
    private readonly Label _penaltyBadge;

    public BackLicensePanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(210, 212, 216);
        AutoScroll = true;
        Padding = new Padding(16);

        var card = new Panel
        {
            Width = 820,
            MinimumSize = new Size(820, 520),
            Height = 620,
            BackColor = UiTheme.CardBack,
            Location = new Point(16, 16),
            Padding = new Padding(14),
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(120, 120, 120));
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(8),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));

        var catGroup = MakeGroup("КАТЕГОРИИ ДОПУСКА");
        _categoryRows = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
        };
        catGroup.Controls.Add(_categoryRows);

        var violGroup = MakeGroup("УЧЁТ НАРУШЕНИЙ");
        _violations = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
        };
        _penaltyBadge = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Bottom,
            Height = 48,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            BackColor = UiTheme.PenaltyBack,
            ForeColor = UiTheme.AccentRed,
            Text = "— / 12",
        };
        violGroup.Controls.Add(_violations);
        violGroup.Controls.Add(_penaltyBadge);

        var notesGroup = MakeGroup("ОСОБЫЕ ОТМЕТКИ");
        _notes = BulletHost();
        notesGroup.Controls.Add(_notes);

        var medGroup = MakeGroup("МЕДКОМИССИЯ ГАБДД");
        _medical = BulletHost();
        medGroup.Controls.Add(_medical);

        layout.Controls.Add(catGroup, 0, 0);
        layout.SetRowSpan(catGroup, 2);
        layout.Controls.Add(violGroup, 1, 0);
        layout.Controls.Add(notesGroup, 1, 1);
        layout.Controls.Add(medGroup, 0, 2);
        layout.SetColumnSpan(medGroup, 2);

        var topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = UiTheme.HeaderBack,
        };

        card.Controls.Add(layout);
        card.Controls.Add(topBar);
        Controls.Add(card);
    }

    private static GroupBox MakeGroup(string title) => new()
    {
        Text = title,
        Dock = DockStyle.Fill,
        Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
        Padding = new Padding(8),
        Margin = new Padding(4),
    };

    private static FlowLayoutPanel BulletHost() => new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        Padding = new Padding(2),
    };

    public void Bind(GamerLicense license)
    {
        _categoryRows.Controls.Clear();
        foreach (var c in license.Categories)
            _categoryRows.Controls.Add(CreateCategoryRow(c));

        _violations.Controls.Clear();
        if (license.Penalties.Violations.Count == 0)
        {
            _violations.Controls.Add(Bullet("(чисто)", UiTheme.Caption));
        }
        else
        {
            foreach (var v in license.Penalties.Violations)
                _violations.Controls.Add(Bullet($"+{v.Points}  {v.Description}", UiTheme.Value));
        }

        _penaltyBadge.Text =
            $"{license.Penalties.TotalPoints} / {license.Penalties.MaxPoints}\n" +
            $"штрафных до лишения: {license.Penalties.RemainingUntilRevocation}";

        FillBullets(_notes, license.SpecialNotes, UiTheme.AccentRed);
        FillBullets(_medical, license.MedicalNotes, UiTheme.Value);
    }

    private static void FillBullets(FlowLayoutPanel host, IReadOnlyList<string> items, Color color)
    {
        host.Controls.Clear();
        if (items.Count == 0)
        {
            host.Controls.Add(Bullet("—", UiTheme.Caption));
            return;
        }

        foreach (var item in items)
            host.Controls.Add(Bullet("• " + item, color));
    }

    private static Label Bullet(string text, Color color) => new()
    {
        Text = text,
        ForeColor = color,
        Font = UiTheme.SmallFont,
        AutoSize = true,
        MaximumSize = new Size(340, 0),
        Margin = new Padding(0, 0, 0, 6),
    };

    private static Control CreateCategoryRow(GameCategory c)
    {
        var row = new Panel
        {
            Width = 400,
            Height = c.RevokeReason is null ? 28 : 46,
            Margin = new Padding(0, 0, 0, 2),
        };

        var code = new Label
        {
            Text = $"{c.Code}",
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Width = 28,
            Height = 24,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(0, 2),
            BackColor = StatusBack(c.Status),
            ForeColor = StatusFore(c.Status),
        };

        var title = new Label
        {
            Text = c.TitleRu,
            Font = UiTheme.SmallFont,
            AutoSize = true,
            Location = new Point(34, 4),
        };

        var status = new Label
        {
            Text = StatusRu(c.Status),
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            ForeColor = StatusFore(c.Status),
            AutoSize = true,
            Location = new Point(150, 4),
        };

        var stats = new Label
        {
            Text = $"{c.OwnedGames} игр · {c.TotalHours:0.#} ч",
            Font = UiTheme.CaptionFont,
            ForeColor = UiTheme.Caption,
            AutoSize = true,
            Location = new Point(250, 5),
        };

        row.Controls.Add(code);
        row.Controls.Add(title);
        row.Controls.Add(status);
        row.Controls.Add(stats);

        if (c.RevokeReason is not null)
        {
            row.Controls.Add(new Label
            {
                Text = c.RevokeReason,
                Font = UiTheme.CaptionFont,
                ForeColor = UiTheme.AccentRed,
                AutoSize = false,
                Width = 390,
                Height = 18,
                Location = new Point(34, 24),
                AutoEllipsis = true,
            });
        }

        return row;
    }

    private static Color StatusBack(CategoryStatus s) => s switch
    {
        CategoryStatus.Opened => UiTheme.Opened,
        CategoryStatus.Revoked => UiTheme.Revoked,
        _ => UiTheme.Closed,
    };

    private static Color StatusFore(CategoryStatus s) => s switch
    {
        CategoryStatus.Opened => UiTheme.OpenedBorder,
        CategoryStatus.Revoked => UiTheme.AccentRed,
        _ => UiTheme.Caption,
    };

    private static string StatusRu(CategoryStatus s) => s switch
    {
        CategoryStatus.Opened => "открыта",
        CategoryStatus.Revoked => "ЛИШЁН",
        _ => "не открыта",
    };
}
