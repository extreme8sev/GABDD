using SteamFun.Domain;
using SteamFun.Services;

namespace SteamFun.Ui;

internal sealed class ReceiptPanel : UserControl
{
    private readonly TextBox _receipt;
    private readonly Label _hint;

    public ReceiptPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(232, 228, 220);
        Padding = new Padding(16);

        _hint = new Label
        {
            Text = "Цены — текущие из Steam Store (RU). Первый опрос библиотеки прогревает кэш цен.",
            Font = UiTheme.CaptionFont,
            ForeColor = UiTheme.Caption,
            Dock = DockStyle.Top,
            Height = 22,
        };

        _receipt = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 10f),
            BackColor = Color.FromArgb(250, 247, 240),
            ForeColor = Color.FromArgb(30, 30, 30),
            BorderStyle = BorderStyle.FixedSingle,
            Text = "Выдайте удостоверение — напечатаем чек.",
        };

        Controls.Add(_receipt);
        Controls.Add(_hint);
    }

    public void Bind(GamerLicense license)
    {
        var receipt = ReceiptBuilder.Build(license.Profile);
        _receipt.Text = ReceiptBuilder.FormatText(receipt);

        var priced = license.Profile.Games.Count(g => g.StorePriceRub is > 0);
        _hint.Text = priced == 0
            ? "Цен пока нет в кэше — перевыдай права (Store RU). Free/снятые с продажи не считаются."
            : $"С ценой: {priced} из {license.FleetSize}. *Средние/текущие витринные цены, не чек покупки.";
    }
}
