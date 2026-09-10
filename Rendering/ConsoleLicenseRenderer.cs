using System.Text;
using SteamFun.Domain;
using SteamFun.Services;

namespace SteamFun.Rendering;

public static class ConsoleLicenseRenderer
{
    public static void Render(GamerLicense license)
    {
        var p = license.Profile;
        var sb = new StringBuilder();

        sb.AppendLine();
        sb.AppendLine(Bar());
        sb.AppendLine("  УДОСТОВЕРЕНИЕ ГЕЙМЕРА · ГАБДД");
        sb.AppendLine("  Габеновская инспекция безопасности игрового движения");
        sb.AppendLine(Bar());
        sb.AppendLine($"  серия STEAM   № GL {SerialFromSteamId(p.SteamId64)}");
        sb.AppendLine($"  КЛАСС: STEAM LVL {p.SteamLevel}");
        sb.AppendLine();
        sb.AppendLine("  --- ЛИЦЕВАЯ СТОРОНА ---");
        sb.AppendLine($"  1.  НИКНЕЙМ .............. {p.PersonaName}");
        sb.AppendLine($"  2.  ГРАЖДАНСТВО .......... {license.Citizenship ?? "—"}");
        sb.AppendLine($"  3.  ЗА. ВЫДАНО ........... {FormatDate(p.AccountCreated)}");
        sb.AppendLine($"  4.  ДЕЙСТВИТЕЛЬНО ДО ..... {license.ValidUntil ?? "—"}");
        sb.AppendLine($"  5.  СТАЖ ВОЖДЕНИЯ ........ {FormatTenure(p.AccountCreated)}");
        sb.AppendLine($"  6.  МЕСТО ЖИТЕЛЬСТВА ..... {license.Residence ?? "—"}");
        sb.AppendLine($"  7.  ТРАНСМИССИЯ .......... {license.Transmission ?? "—"}");
        sb.AppendLine($"  8.  ГРУППА КРОВИ ......... {license.BloodType ?? "—"}");
        sb.AppendLine($"  9.  ПАРК СРЕДСТВ ......... {license.FleetSize} ед. (без запуска: {license.NeverPlayedCount})");
        sb.AppendLine($"  10. ПРОБЕГ ОБЩИЙ ......... {license.TotalHours:0.#} ч  (14 дн.: {license.HoursLast14Days:0.#} ч)");
        sb.AppendLine($"  11. VAC-УЧЁТ ............. {FormatVac(p)}");
        sb.AppendLine($"  12. ОСОБЫЕ ПРИМЕТЫ ....... {license.SpecialMarks ?? "—"}");
        sb.AppendLine();
        sb.AppendLine($"  Штрихкод / Steam64ID: {p.SteamId64}");
        sb.AppendLine();
        sb.AppendLine("  Категории (лицевая):");
        AppendCategoryGrid(sb, license.Categories);
        sb.AppendLine();
        sb.AppendLine("  --- ОБОРОТНАЯ СТОРОНА ---");
        sb.AppendLine("  КАТЕГОРИИ ДОПУСКА:");
        foreach (var c in license.Categories)
        {
            sb.AppendLine(
                $"    {c.Code,-2} {c.TitleRu,-14} {StatusRu(c.Status),-10}  " +
                $"игр:{c.OwnedGames,3}  часов:{c.TotalHours,6:0.#}");
            if (c.RevokeReason is not null)
                sb.AppendLine($"       → {c.RevokeReason}");
        }

        sb.AppendLine();
        sb.AppendLine("  УЧЁТ НАРУШЕНИЙ:");
        if (license.Penalties.Violations.Count == 0)
        {
            sb.AppendLine("    (чисто)");
        }
        else
        {
            foreach (var v in license.Penalties.Violations)
                sb.AppendLine($"    +{v.Points}  {v.Description}");
        }

        sb.AppendLine();
        sb.AppendLine(
            $"  ШТРАФНЫХ БАЛЛОВ: {license.Penalties.TotalPoints} / {license.Penalties.MaxPoints}  " +
            $"(до лишения: {license.Penalties.RemainingUntilRevocation})");

        sb.AppendLine();
        sb.AppendLine("  ОСОБЫЕ ОТМЕТКИ:");
        foreach (var note in license.SpecialNotes)
            sb.AppendLine($"    • {note}");

        sb.AppendLine();
        sb.AppendLine("  МЕДКОМИССИЯ ГАБДД:");
        foreach (var note in license.MedicalNotes)
            sb.AppendLine($"    • {note}");

        sb.AppendLine();
        sb.AppendLine("  --- TOP-10 ПО ЧАСАМ + АЧИВКИ ---");
        var top = p.Games.OrderByDescending(g => g.PlaytimeForeverMinutes).Take(10).ToList();
        for (var i = 0; i < top.Count; i++)
        {
            var g = top[i];
            var ach = p.Top10Achievements.FirstOrDefault(a => a.AppId == g.AppId);
            var achText = ach is null
                ? "?"
                : ach.StatsPrivate
                    ? "нет данных / скрыто"
                    : $"{ach.Unlocked}/{ach.Total}";
            var genres = g.Genres.Count == 0 ? "—" : string.Join(", ", g.Genres);
            var tags = g.Tags.Count == 0 ? "—" : string.Join(", ", g.Tags.Take(8));
            var cats = string.Join(",", GenreMapper.Map(g).OrderBy(c => c.ToString()));
            sb.AppendLine(
                $"  {i + 1,2}. {g.Name}  —  {g.PlaytimeForeverMinutes / 60.0:0.#} ч  | ачивки: {achText}");
            sb.AppendLine($"      genres: {genres}");
            sb.AppendLine($"      tags:   {tags}");
            sb.AppendLine($"      → кат:  {(cats.Length == 0 ? "—" : cats)}");
        }

        sb.AppendLine();
        sb.AppendLine("  --- СВОДКА ТЕГОВ (top-25) ---");
        var tagStats = p.Games
            .SelectMany(g => g.Tags.DefaultIfEmpty("(без тегов)"))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .Take(25);
        foreach (var g in tagStats)
            sb.AppendLine($"    {g.Count(),4} × {g.Key}");

        sb.AppendLine(Bar());
        Console.WriteLine(sb.ToString());
    }

    private static void AppendCategoryGrid(StringBuilder sb, IReadOnlyList<GameCategory> categories)
    {
        foreach (var chunk in categories.Chunk(4))
        {
            sb.Append("   ");
            foreach (var c in chunk)
            {
                var mark = c.Status switch
                {
                    CategoryStatus.Opened => "[*]",
                    CategoryStatus.Revoked => "[X]",
                    _ => "[ ]",
                };
                sb.Append($"{mark}{c.Code}:{c.TitleRu,-12} ");
            }

            sb.AppendLine();
        }

        sb.AppendLine("   [*] открыта   [X] лишён   [ ] не открыта");
    }

    private static string StatusRu(CategoryStatus s) => s switch
    {
        CategoryStatus.Opened => "открыта",
        CategoryStatus.Revoked => "ЛИШЁН",
        _ => "не открыта",
    };

    private static string FormatVac(SteamProfileSnapshot p) =>
        p.VacBanned ? $"состоит ({p.NumberOfVacBans} VAC)" : "не состоит";

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

    private static string SerialFromSteamId(string steamId64)
    {
        // шуточный номер из хвоста id
        if (steamId64.Length < 8) return steamId64;
        var tail = steamId64[^8..];
        return $"{tail[..4]} {tail[4..]}";
    }

    private static string Bar() => new string('═', 64);
}
