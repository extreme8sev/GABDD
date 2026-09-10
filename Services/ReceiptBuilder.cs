using System.Text;
using SteamFun.Domain;

namespace SteamFun.Services;

public sealed record ReceiptLine(string Name, int PriceRub);

public sealed record SteamReceipt(
    string PersonaName,
    string SteamId64,
    DateTimeOffset IssuedAt,
    IReadOnlyList<ReceiptLine> TopLines,
    int HiddenGamesCount,
    int HiddenGamesSumRub,
    int LibraryCount,
    int PricedCount,
    int NeverPlayedCount,
    int NeverPlayedSumRub,
    int? MostExpensiveMistakeRub,
    string? MostExpensiveMistakeName,
    int TotalRub,
    double TotalHours,
    int DaysLived,
    int EnergyDrinks,
    int EnergyDrinkPriceRub,
    int? RubPerHour,
    string LoyaltyTitle,
    string Footnote);

public static class ReceiptBuilder
{
    /// <summary>Шуточная цена банки для перевода «ИТОГО» в энергетики.</summary>
    public const int EnergyDrinkPriceRub = 89;

    public static SteamReceipt Build(SteamProfileSnapshot profile)
    {
        var priced = profile.Games
            .Where(g => g.StorePriceRub is > 0)
            .Select(g => (Game: g, Price: g.StorePriceRub!.Value))
            .OrderByDescending(x => x.Price)
            .ThenBy(x => x.Game.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var top = priced.Take(14)
            .Select(x => new ReceiptLine(Short(x.Game.Name, 34), x.Price))
            .ToList();

        var hidden = priced.Skip(14).ToList();
        var hiddenSum = hidden.Sum(x => x.Price);
        var totalRub = priced.Sum(x => x.Price);

        var never = profile.Games
            .Where(g => g.PlaytimeForeverMinutes <= 0 && g.StorePriceRub is > 0)
            .ToList();
        var neverSum = never.Sum(g => g.StorePriceRub!.Value);
        var worst = never.OrderByDescending(g => g.StorePriceRub).FirstOrDefault();

        var totalHours = profile.Games.Sum(g => g.PlaytimeForeverMinutes) / 60.0;
        var daysLived = (int)Math.Round(totalHours / 24.0);
        var drinks = totalRub > 0 ? totalRub / EnergyDrinkPriceRub : 0;
        int? rubPerHour = totalHours >= 1 && totalRub > 0
            ? (int)Math.Round(totalRub / totalHours)
            : null;

        var years = profile.AccountCreated is null
            ? 0
            : (int)((DateTimeOffset.Now - profile.AccountCreated.Value).TotalDays / 365.25);

        var loyalty = years >= 12 || totalHours >= 8000 ? "ВЕТЕРАН"
            : years >= 7 || totalHours >= 3000 ? "ПОСТОЯЛЕЦ"
            : years >= 3 || totalHours >= 500 ? "ПОКУЧЕН"
            : "НОВИЧОК У КАССЫ";

        return new SteamReceipt(
            PersonaName: profile.PersonaName,
            SteamId64: profile.SteamId64,
            IssuedAt: DateTimeOffset.Now,
            TopLines: top,
            HiddenGamesCount: hidden.Count,
            HiddenGamesSumRub: hiddenSum,
            LibraryCount: profile.Games.Count,
            PricedCount: priced.Count,
            NeverPlayedCount: never.Count,
            NeverPlayedSumRub: neverSum,
            MostExpensiveMistakeRub: worst?.StorePriceRub,
            MostExpensiveMistakeName: worst is null ? null : Short(worst.Name, 28),
            TotalRub: totalRub,
            TotalHours: totalHours,
            DaysLived: daysLived,
            EnergyDrinks: drinks,
            EnergyDrinkPriceRub: EnergyDrinkPriceRub,
            RubPerHour: rubPerHour,
            LoyaltyTitle: loyalty,
            Footnote: "* Цены: Store RU → Store US → SteamSpy (×95 ₽/$). Не чек покупки — витринная оценка.");
    }

    public static string FormatText(SteamReceipt r)
    {
        const int width = 42;
        var sb = new StringBuilder();
        sb.AppendLine(Center("STEAM / КАССОВЫЙ ЧЕК", width));
        sb.AppendLine(Center("GABEN INDUSTRIES · VALVE Corp.", width));
        sb.AppendLine(Center("КАССА №13 · СМЕНА: ВЕЧНАЯ", width));
        sb.AppendLine(new string('─', width));
        sb.AppendLine($"  {r.PersonaName}");
        sb.AppendLine($"  ID {r.SteamId64}");
        sb.AppendLine($"  {r.IssuedAt.ToLocalTime():dd.MM.yyyy, HH:mm}");
        sb.AppendLine(new string('─', width));

        foreach (var line in r.TopLines)
            sb.AppendLine(MoneyLine(line.Name, line.PriceRub, width));

        if (r.HiddenGamesCount > 0)
        {
            sb.AppendLine(MoneyLine(
                $"…и ещё {r.HiddenGamesCount} игр",
                r.HiddenGamesSumRub,
                width));
        }

        sb.AppendLine(new string('─', width));
        sb.AppendLine(PadPair("Игр в библиотеке", r.LibraryCount.ToString(), width));
        sb.AppendLine(PadPair("Из них с ценой", r.PricedCount.ToString(), width));
        sb.AppendLine(PadPair(
            $"Не запущено ни разу",
            $"{r.NeverPlayedCount}  ({Fmt(r.NeverPlayedSumRub)} ₽)",
            width));

        if (r.MostExpensiveMistakeRub is int mistake)
        {
            sb.AppendLine(PadPair(
                "Самая дорогая ошибка",
                $"{Fmt(mistake)} ₽",
                width));
            if (r.MostExpensiveMistakeName is not null)
                sb.AppendLine($"  «{r.MostExpensiveMistakeName}»");
        }

        sb.AppendLine(new string('═', width));
        sb.AppendLine(PadPair("ИТОГО", $"{Fmt(r.TotalRub)} ₽", width));
        sb.AppendLine(new string('═', width));
        sb.AppendLine(r.Footnote);
        sb.AppendLine();
        sb.AppendLine(PadPair("Прожито в играх", $"{r.DaysLived} дн.", width));
        sb.AppendLine(PadPair(
            "Хватило бы на",
            $"{Fmt(r.EnergyDrinks)} эн. × {r.EnergyDrinkPriceRub} ₽",
            width));
        if (r.RubPerHour is int pph)
            sb.AppendLine(PadPair("Цена часа радости", $"{pph} ₽/ч", width));
        sb.AppendLine(PadPair("Преданность Gaben", r.LoyaltyTitle, width));
        sb.AppendLine();
        sb.AppendLine(Center("★ ОПЛАЧЕНО ДУШОЙ ★", width));
        sb.AppendLine(Center("СПАСИБО ЗА ПОКУПКУ!", width));
        sb.AppendLine(Center("Обмен и возврат души не предусмотрен.", width));
        return sb.ToString();
    }

    private static string MoneyLine(string name, int rub, int width)
    {
        var price = $"{Fmt(rub)} ₽";
        var maxName = Math.Max(8, width - price.Length - 1);
        if (name.Length > maxName)
            name = name[..(maxName - 1)] + "…";
        return name.PadRight(maxName) + " " + price;
    }

    private static string PadPair(string left, string right, int width)
    {
        var space = width - left.Length - right.Length;
        if (space < 1)
            return left + " " + right;
        return left + new string(' ', space) + right;
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width) return text;
        var pad = (width - text.Length) / 2;
        return new string(' ', pad) + text;
    }

    private static string Fmt(int rub) => rub.ToString("N0", new System.Globalization.CultureInfo("ru-RU"));

    private static string Short(string name, int max)
    {
        name = name.Replace("™", "").Replace("®", "").Trim();
        return name.Length <= max ? name : name[..(max - 1)] + "…";
    }
}
