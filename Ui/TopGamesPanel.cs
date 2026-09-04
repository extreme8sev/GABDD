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
        var top = PlayActivity.RankByActivity(license.Profile.Games, 10);
        if (top.Count == 0)
        {
            top = license.Profile.Games
                .OrderByDescending(g => g.PlaytimeForeverMinutes)
                .Take(10)
                .ToList();
        }

        _list.Controls.Add(new Label
        {
            Text = "Сортировка: сначала недавняя активность (не вечные часы)",
            Font = UiTheme.CaptionFont,
            ForeColor = UiTheme.Caption,
            AutoSize = true,
            Margin = new Padding(4, 0, 0, 10),
        });

        for (var i = 0; i < top.Count; i++)
        {
            var g = top[i];
            var ach = license.Profile.Top10Achievements.FirstOrDefault(a => a.AppId == g.AppId);
            var achText = ach is null
                ? "?"
                : ach.StatsPrivate
                    ? "нет данных"
                    : $"{ach.Unlocked}/{ach.Total}";
            var cats = string.Join(" · ", GenreMapper.Map(g).OrderBy(c => c.ToString()));
            var activity = PlayActivity.FormatLastPlayed(g) ?? "давно / нет даты";

            _list.Controls.Add(CreateRow(i + 1, g, achText, cats, activity));
        }
    }

    private static Control CreateRow(
        int index, OwnedGameInfo game, string achievements, string cats, string activity)
    {
        var row = new Panel
        {
            Width = 780,
            Height = 78,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.White,
            Padding = new Padding(10),
        };
        row.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(210, 210, 210));
            e.Graphics.DrawRectangle(pen, 0, 0, row.Width - 1, row.Height - 1);
        };

        var idx = new Label
        {
            Text = $"{index}.",
            Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold),
            ForeColor = UiTheme.Caption,
            Location = new Point(10, 22),
            AutoSize = true,
        };

        var name = new Label
        {
            Text = game.Name,
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            ForeColor = UiTheme.Value,
            Location = new Point(44, 8),
            AutoSize = true,
            MaximumSize = new Size(500, 24),
            AutoEllipsis = true,
        };

        var hours = new Label
        {
            Text = $"{game.PlaytimeForeverMinutes / 60.0:0.#} ч",
            Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
            ForeColor = UiTheme.OpenedBorder,
            AutoSize = true,
            Location = new Point(650, 10),
        };

        var meta = new Label
        {
            Text =
                $"{activity} · 14 дн.: {game.Playtime2WeeksMinutes / 60.0:0.#} ч · ачивки: {achievements} · {cats}",
            Font = UiTheme.SmallFont,
            ForeColor = UiTheme.Caption,
            Location = new Point(44, 40),
            AutoSize = true,
            MaximumSize = new Size(700, 28),
            AutoEllipsis = true,
        };

        row.Controls.Add(idx);
        row.Controls.Add(name);
        row.Controls.Add(hours);
        row.Controls.Add(meta);
        return row;
    }
}
