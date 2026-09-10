using SteamFun.Domain;
using SteamFun.Services;

namespace SteamFun.Ui;

internal sealed class TopGamesPanel : UserControl
{
    private readonly FlowLayoutPanel _list;

    public TopGamesPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(245, 246, 248);
        AutoScroll = true;
        Padding = new Padding(12);

        _list = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(4),
        };
        Controls.Add(_list);
    }

    public void Bind(GamerLicense license)
    {
        _list.Controls.Clear();

        _list.Controls.Add(new Label
        {
            Text = "Посмотри на себя",
            Font = new Font("Segoe UI Semibold", 16f, FontStyle.Bold),
            ForeColor = UiTheme.AccentRed,
            AutoSize = true,
            Margin = new Padding(4, 4, 0, 4),
        });

        _list.Controls.Add(new Label
        {
            Text = "Клички по пробегу: легенды, брошенные и «купил — забыл»",
            Font = UiTheme.CaptionFont,
            ForeColor = UiTheme.Caption,
            AutoSize = true,
            Margin = new Padding(4, 0, 0, 14),
        });

        var roasts = GameRoastBuilder.Build(license.Profile, take: 10);
        if (roasts.Count == 0)
        {
            _list.Controls.Add(new Label
            {
                Text = "Библиотека пуста — смотреть не на что.",
                ForeColor = UiTheme.Caption,
                AutoSize = true,
            });
            return;
        }

        foreach (var roast in roasts)
        {
            var ach = license.Profile.Top10Achievements.FirstOrDefault(a => a.AppId == roast.Game.AppId);
            var achText = ach is null
                ? null
                : ach.StatsPrivate
                    ? "ачивки скрыты"
                    : $"{ach.Unlocked}/{ach.Total} ачивок";
            _list.Controls.Add(CreateRow(roast, achText));
        }
    }

    private static Control CreateRow(GameRoast roast, string? achText)
    {
        var row = new Panel
        {
            Width = 800,
            Height = 88,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = Color.White,
            Padding = new Padding(12, 10, 12, 10),
        };
        row.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(210, 210, 210));
            e.Graphics.DrawRectangle(pen, 0, 0, row.Width - 1, row.Height - 1);
            using var accent = new Pen(UiTheme.AccentRed, 3);
            e.Graphics.DrawLine(accent, 0, 0, 0, row.Height);
        };

        var badge = new Label
        {
            Text = roast.Badge,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            ForeColor = UiTheme.AccentRed,
            Location = new Point(14, 8),
            AutoSize = true,
            MaximumSize = new Size(560, 20),
        };

        var name = new Label
        {
            Text = roast.GameName,
            Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
            ForeColor = UiTheme.Value,
            Location = new Point(14, 30),
            AutoSize = true,
            MaximumSize = new Size(560, 24),
            AutoEllipsis = true,
        };

        var joke = new Label
        {
            Text = achText is null ? roast.Joke : $"{roast.Joke}  ·  {achText}",
            Font = new Font("Segoe UI", 9f, FontStyle.Italic),
            ForeColor = UiTheme.Caption,
            Location = new Point(14, 56),
            AutoSize = true,
            MaximumSize = new Size(620, 22),
            AutoEllipsis = true,
        };

        var hours = new Label
        {
            Text = roast.HoursText,
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            ForeColor = UiTheme.OpenedBorder,
            AutoSize = true,
            Location = new Point(680, 28),
        };

        row.Controls.Add(badge);
        row.Controls.Add(name);
        row.Controls.Add(joke);
        row.Controls.Add(hours);
        return row;
    }
}
