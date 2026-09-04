using SteamFun.Domain;

namespace SteamFun.Ui;

internal sealed class PersonalityPanel : UserControl
{
    private readonly Panel _card;
    private readonly Label _archetype;
    private readonly Label _headline;
    private readonly Label _summary;
    private readonly FlowLayoutPanel _traits;
    private readonly FlowLayoutPanel _strengths;
    private readonly FlowLayoutPanel _risks;
    private readonly Label _compatibility;
    private readonly Label _verdict;
    private readonly Panel _bottom;

    public PersonalityPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(210, 212, 216);
        AutoScroll = true;
        Padding = new Padding(16);

        _card = new Panel
        {
            Width = 840,
            Height = 760,
            MinimumSize = new Size(840, 700),
            BackColor = UiTheme.CardBack,
            Location = new Point(16, 16),
            Padding = new Padding(0),
        };
        _card.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(120, 120, 120));
            e.Graphics.DrawRectangle(pen, 0, 0, _card.Width - 1, _card.Height - 1);
        };

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 78,
            BackColor = UiTheme.HeaderBack,
            Padding = new Padding(16, 10, 16, 8),
        };

        var title = new Label
        {
            Text = "ПСИХОЭКСПЕРТИЗА ГАБДД · НЕ ДЛЯ РЕЗЮМЕ",
            Font = UiTheme.TitleFont,
            ForeColor = UiTheme.HeaderFore,
            AutoSize = true,
            Location = new Point(16, 8),
        };
        _archetype = new Label
        {
            Text = "Архетип: —",
            Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 210, 120),
            AutoSize = true,
            Location = new Point(16, 38),
        };
        header.Controls.Add(title);
        header.Controls.Add(_archetype);

        var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 12) };

        _headline = new Label
        {
            Text = "Выдайте удостоверение, чтобы получить заключение.",
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            ForeColor = UiTheme.Value,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 28,
        };

        _summary = new Label
        {
            Text = "—",
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.Value,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 100,
        };

        // Низ: совместимость + штамп — фиксированно высокий, чтобы длинный вердикт влезал.
        _bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 210,
            Padding = new Padding(0, 4, 0, 0),
        };

        var columns = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0, 8, 0, 8),
        };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4f));

        _traits = BulletColumn();
        _strengths = BulletColumn();
        _risks = BulletColumn();
        columns.Controls.Add(WrapGroup("Симптомы", _traits), 0, 0);
        columns.Controls.Add(WrapGroup("Суперспособности", _strengths), 1, 0);
        columns.Controls.Add(WrapGroup("Побочные эффекты", _risks), 2, 0);

        var compCap = SectionCaption("С кем/чем совместим");
        _compatibility = new Label
        {
            Text = "—",
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.Value,
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 52,
        };

        var verdCap = SectionCaption("Штамп комиссии");
        _verdict = new Label
        {
            Text = "—",
            Font = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold),
            ForeColor = UiTheme.OpenedBorder,
            AutoSize = false,
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Opened,
            Padding = new Padding(10, 8, 10, 8),
            TextAlign = ContentAlignment.TopLeft,
        };

        var verdHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 4, 0, 0),
            MinimumSize = new Size(0, 90),
        };
        verdHost.Controls.Add(_verdict);

        // Порядок Dock: Fill первым в Controls, Top — сверху вниз в обратном порядке добавления.
        _bottom.Controls.Add(verdHost);   // Fill
        _bottom.Controls.Add(verdCap);    // Top (ниже compatibility)
        _bottom.Controls.Add(_compatibility);
        _bottom.Controls.Add(compCap);

        body.Controls.Add(columns);  // Fill
        body.Controls.Add(_bottom);  // Bottom
        body.Controls.Add(_summary); // Top
        body.Controls.Add(_headline);

        _card.Controls.Add(body);
        _card.Controls.Add(header);
        Controls.Add(_card);
    }

    private static Label SectionCaption(string text) => new()
    {
        Text = text,
        Font = UiTheme.CaptionFont,
        ForeColor = UiTheme.Caption,
        Dock = DockStyle.Top,
        Height = 18,
    };

    private static FlowLayoutPanel BulletColumn() => new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        Padding = new Padding(2),
    };

    private static GroupBox WrapGroup(string title, Control content)
    {
        var g = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Padding = new Padding(8),
            Margin = new Padding(4),
        };
        g.Controls.Add(content);
        return g;
    }

    public void Bind(GamerLicense license)
    {
        var p = license.Personality;
        _archetype.Text = $"Архетип: {p.Archetype}";
        _headline.Text = p.Headline;
        _summary.Text = p.Summary;
        Fill(_traits, p.Traits, UiTheme.Value);
        Fill(_strengths, p.Strengths, UiTheme.OpenedBorder);
        Fill(_risks, p.Risks, UiTheme.AccentRed);
        _compatibility.Text = p.Compatibility;
        _verdict.Text = p.Verdict;
        _verdict.ForeColor = p.Verdict.Contains("НЕ ", StringComparison.OrdinalIgnoreCase)
            ? UiTheme.AccentRed
            : UiTheme.OpenedBorder;
        _verdict.BackColor = p.Verdict.Contains("НЕ ", StringComparison.OrdinalIgnoreCase)
            ? UiTheme.Revoked
            : UiTheme.Opened;

        // Подгоняем высоту нижней панели под длину штампа.
        AdjustBottomForVerdict();
    }

    private void AdjustBottomForVerdict()
    {
        const int contentWidth = 800;
        using var g = CreateGraphics();
        using var format = new StringFormat { FormatFlags = StringFormatFlags.LineLimit };
        format.Trimming = StringTrimming.Word;
        var size = g.MeasureString(_verdict.Text, _verdict.Font, contentWidth, format);
        var verdictBlock = Math.Max(96, (int)Math.Ceiling(size.Height) + 28);
        // подписи + совместимость + штамп + отступы
        var needed = 18 + 52 + 18 + verdictBlock + 16;
        _bottom.Height = Math.Clamp(needed, 200, 320);

        // Карточка чуть выше, чтобы низ не упирался.
        var minCard = 560 + _bottom.Height;
        if (_card.Height < minCard)
            _card.Height = minCard;
    }

    private static void Fill(FlowLayoutPanel host, IReadOnlyList<string> items, Color color)
    {
        host.Controls.Clear();
        foreach (var item in items)
        {
            host.Controls.Add(new Label
            {
                Text = "• " + item,
                ForeColor = color,
                Font = UiTheme.SmallFont,
                AutoSize = true,
                MaximumSize = new Size(240, 0),
                Margin = new Padding(0, 0, 0, 8),
            });
        }
    }
}
