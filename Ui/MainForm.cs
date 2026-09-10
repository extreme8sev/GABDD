using Microsoft.Extensions.Configuration;
using SteamFun.Domain;
using SteamFun.Services;
using SteamFun.Steam;

namespace SteamFun.Ui;

public sealed class MainForm : Form
{
    private readonly TextBox _steamIdBox;
    private readonly ComboBox _friendsCombo;
    private readonly ComboBox _genderCombo;
    private readonly Button _loadFriendsButton;
    private readonly Button _issueButton;
    private readonly Label _statusLabel;
    private readonly TabControl _tabs;
    private readonly FrontLicensePanel _front;
    private readonly BackLicensePanel _back;
    private readonly TopGamesPanel _top;
    private readonly PersonalityPanel _personality;
    private readonly ReceiptPanel _receipt;

    private string? _apiKey;
    private CancellationTokenSource? _loadCts;
    private bool _suppressFriendSelect;
    private bool _suppressGenderSelect;
    private SteamProfileSnapshot? _lastSnapshot;

    public MainForm()
    {
        Text = "ГАБДД · Удостоверение геймера";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(900, 720);
        Size = new Size(980, 800);
        Font = new Font("Segoe UI", 10f);
        BackColor = Color.FromArgb(245, 246, 248);

        var top = new Panel
        {
            Dock = DockStyle.Top,
            Height = 92,
            BackColor = Color.White,
            Padding = new Padding(12, 10, 12, 8),
        };

        var idLabel = new Label
        {
            Text = "Steam64ID",
            AutoSize = true,
            Location = new Point(12, 16),
            ForeColor = UiTheme.Caption,
        };

        _steamIdBox = new TextBox
        {
            PlaceholderText = "76561198113831896",
            Text = "76561198113831896",
            Width = 200,
            Location = new Point(100, 12),
        };

        _issueButton = new Button
        {
            Text = "Выдать удостоверение",
            Width = 180,
            Height = 32,
            Location = new Point(316, 10),
            FlatStyle = FlatStyle.System,
        };
        _issueButton.Click += async (_, _) => await IssueAsync();

        var genderLabel = new Label
        {
            Text = "Обращение",
            AutoSize = true,
            Location = new Point(512, 16),
            ForeColor = UiTheme.Caption,
        };

        _genderCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 72,
            Location = new Point(600, 12),
        };
        _genderCombo.Items.AddRange([
            Pronouns.DisplayName(GenderAddress.He),
            Pronouns.DisplayName(GenderAddress.She),
            Pronouns.DisplayName(GenderAddress.They),
        ]);
        _genderCombo.SelectedIndexChanged += GenderComboOnSelectedIndexChanged;

        var friendsLabel = new Label
        {
            Text = "Друзья",
            AutoSize = true,
            Location = new Point(12, 54),
            ForeColor = UiTheme.Caption,
        };

        _friendsCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 480,
            Location = new Point(100, 50),
            Enabled = false,
        };
        _friendsCombo.SelectedIndexChanged += FriendsComboOnSelectedIndexChanged;

        _loadFriendsButton = new Button
        {
            Text = "Обновить друзей",
            Width = 160,
            Height = 30,
            Location = new Point(600, 48),
            FlatStyle = FlatStyle.System,
        };
        _loadFriendsButton.Click += async (_, _) => await LoadFriendsAsync(forceRefresh: true);

        _friendsCombo.FormattingEnabled = true;
        _friendsCombo.Format += (_, e) =>
        {
            if (e.ListItem is SteamFriend f)
                e.Value = f.ToString();
        };

        top.Controls.Add(idLabel);
        top.Controls.Add(_steamIdBox);
        top.Controls.Add(_issueButton);
        top.Controls.Add(genderLabel);
        top.Controls.Add(_genderCombo);
        top.Controls.Add(friendsLabel);
        top.Controls.Add(_friendsCombo);
        top.Controls.Add(_loadFriendsButton);

        _statusLabel = new Label
        {
            Text = "Готов к опросу профиля. Друзей: список должен быть публичным.",
            AutoSize = false,
            Dock = DockStyle.Bottom,
            Height = 26,
            Padding = new Padding(12, 4, 12, 4),
            ForeColor = Color.DimGray,
            BackColor = Color.White,
        };

        _front = new FrontLicensePanel();
        _back = new BackLicensePanel();
        _top = new TopGamesPanel();
        _personality = new PersonalityPanel();
        _receipt = new ReceiptPanel();

        _tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(8, 8) };
        _tabs.TabPages.Add(Wrap("Лицевая сторона", _front));
        _tabs.TabPages.Add(Wrap("Оборотная сторона", _back));
        _tabs.TabPages.Add(Wrap("Психопортрет", _personality));
        _tabs.TabPages.Add(Wrap("Посмотри на себя", _top));
        _tabs.TabPages.Add(Wrap("Кассовый чек", _receipt));

        Controls.Add(_tabs);
        Controls.Add(_statusLabel);
        Controls.Add(top);

        AcceptButton = _issueButton;
        Load += (_, _) =>
        {
            ResolveApiKey();
            RestoreGenderFromCache();
            RestoreFriendsFromCache();
        };
        FormClosed += (_, _) => _loadCts?.Cancel();
    }

    private void RestoreGenderFromCache()
    {
        _suppressGenderSelect = true;
        _genderCombo.SelectedIndex = (int)FriendsCache.TryLoadGenderAddress();
        if (_genderCombo.SelectedIndex < 0)
            _genderCombo.SelectedIndex = 0;
        _suppressGenderSelect = false;
    }

    private GenderAddress CurrentGender() =>
        _genderCombo.SelectedIndex switch
        {
            1 => GenderAddress.She,
            2 => GenderAddress.They,
            _ => GenderAddress.He,
        };

    private async void GenderComboOnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressGenderSelect)
            return;

        var gender = CurrentGender();
        FriendsCache.SaveGenderAddress(gender);

        if (_lastSnapshot is null)
            return;

        try
        {
            var license = LicenseBuilder.Build(_lastSnapshot, gender);
            await BindLicenseAsync(license, CancellationToken.None);
            _statusLabel.Text = $"Тексты обновлены ({Pronouns.DisplayName(gender)}): {license.Profile.PersonaName}";
            _statusLabel.ForeColor = Color.ForestGreen;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Не удалось пересобрать тексты.";
            _statusLabel.ForeColor = Color.Firebrick;
            MessageBox.Show(this, ex.Message, "ГАБДД", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static TabPage Wrap(string title, Control content)
    {
        var page = new TabPage(title) { Padding = new Padding(0), UseVisualStyleBackColor = true };
        page.Controls.Add(content);
        return page;
    }

    private void ResolveApiKey()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddUserSecrets(typeof(Program).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        _apiKey =
            config["Steam:ApiKey"]
            ?? config["STEAM_API_KEY"]
            ?? Environment.GetEnvironmentVariable("STEAM_API_KEY");

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _statusLabel.Text = "Нет API-ключа. Выполни: dotnet user-secrets set \"Steam:ApiKey\" \"...\"";
            _statusLabel.ForeColor = Color.Firebrick;
            _issueButton.Enabled = false;
            _loadFriendsButton.Enabled = false;
        }
    }

    private void RestoreFriendsFromCache()
    {
        var lastOwner = FriendsCache.TryLoadLastOwnerSteamId();
        if (!string.IsNullOrWhiteSpace(lastOwner) && lastOwner.Length == 17)
            _steamIdBox.Text = lastOwner;

        var ownerId = _steamIdBox.Text.Trim();
        if (ownerId.Length != 17 || !ownerId.All(char.IsDigit))
            return;

        var cached = FriendsCache.TryLoad(ownerId, out var savedAt);
        if (cached is null || cached.Count == 0)
        {
            _statusLabel.Text = "Друзья ещё не в кэше — нажми «Обновить друзей».";
            return;
        }

        BindFriendsCombo(cached, preferSteamId: ownerId);
        var friendCount = Math.Max(0, cached.Count - 1);
        var when = savedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "?";
        _statusLabel.Text = $"Друзья из кэша ({friendCount}): {when}. «Обновить друзей» — с сети.";
        _statusLabel.ForeColor = Color.DimGray;
    }

    private void BindFriendsCombo(IReadOnlyList<SteamFriend> friends, string? preferSteamId = null)
    {
        _suppressFriendSelect = true;
        _friendsCombo.DataSource = null;
        _friendsCombo.DataSource = friends.ToList();
        _friendsCombo.DisplayMember = nameof(SteamFriend.PersonaName);
        _friendsCombo.Enabled = friends.Count > 0;

        if (friends.Count == 0)
        {
            _friendsCombo.SelectedIndex = -1;
            _suppressFriendSelect = false;
            return;
        }

        var index = 0;
        if (!string.IsNullOrWhiteSpace(preferSteamId))
        {
            var found = friends.ToList().FindIndex(f => f.SteamId64 == preferSteamId);
            if (found >= 0)
                index = found;
        }

        _friendsCombo.SelectedIndex = index;
        _suppressFriendSelect = false;

        if (_friendsCombo.SelectedItem is SteamFriend selected)
            _steamIdBox.Text = selected.SteamId64;
    }

    private void FriendsComboOnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressFriendSelect)
            return;

        if (_friendsCombo.SelectedItem is SteamFriend friend)
        {
            _steamIdBox.Text = friend.SteamId64;
            FriendsCache.SaveLastOwnerSteamId(
                // владелец списка — первый в кэше текущего комбо, если есть; иначе текущий id
                (_friendsCombo.DataSource as List<SteamFriend>)?.FirstOrDefault()?.SteamId64
                ?? friend.SteamId64);
        }
    }

    private async Task LoadFriendsAsync(bool forceRefresh)
    {
        var steamId = _steamIdBox.Text.Trim();
        // Если в комбо уже есть список, «обновить» нужно от владельца списка (первый элемент),
        // а не от выбранного друга.
        if (_friendsCombo.DataSource is List<SteamFriend> { Count: > 0 } existing)
            steamId = existing[0].SteamId64;
        else if (_friendsCombo.Items.Count > 0 && _friendsCombo.Items[0] is SteamFriend firstItem)
            steamId = firstItem.SteamId64;

        if (steamId.Length != 17 || !steamId.All(char.IsDigit))
        {
            MessageBox.Show(this,
                "Сначала укажи свой Steam64ID — друзей грузим от этого аккаунта.",
                "ГАБДД", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!forceRefresh)
        {
            var cached = FriendsCache.TryLoad(steamId, out var savedAt);
            if (cached is { Count: > 0 })
            {
                BindFriendsCombo(cached, preferSteamId: _steamIdBox.Text.Trim());
                var when = savedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "?";
                _statusLabel.Text = $"Друзья из кэша ({Math.Max(0, cached.Count - 1)}): {when}";
                _statusLabel.ForeColor = Color.ForestGreen;
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
            return;

        _loadFriendsButton.Enabled = false;
        _statusLabel.ForeColor = Color.DimGray;
        _statusLabel.Text = "Обновляем список друзей из Steam…";
        UseWaitCursor = true;

        try
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "cache");
            using var client = new SteamApiClient(_apiKey, cacheDir);
            var friends = await Task.Run(async () => await client.FetchFriendsAsync(steamId));

            BindFriendsCombo(friends, preferSteamId: steamId);

            var friendCount = Math.Max(0, friends.Count - 1);
            _statusLabel.Text = friendCount == 0
                ? "Друзей не найдено (или список пуст). Ты в списке один. Кэш сохранён."
                : $"Обновлено друзей: {friendCount}. Кэш сохранён — следующий запуск мгновенный.";
            _statusLabel.ForeColor = Color.ForestGreen;
        }
        catch (Exception ex)
        {
            // Если сеть упала — покажем старый кэш, если есть.
            var cached = FriendsCache.TryLoad(steamId, out _);
            if (cached is { Count: > 0 })
            {
                BindFriendsCombo(cached);
                _statusLabel.Text = "Сеть недоступна — показан старый кэш друзей.";
                _statusLabel.ForeColor = Color.DarkOrange;
            }
            else
            {
                _statusLabel.Text = "Не удалось загрузить друзей.";
                _statusLabel.ForeColor = Color.Firebrick;
            }

            MessageBox.Show(this, ex.Message, "Друзья Steam",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
            _loadFriendsButton.Enabled = true;
        }
    }

    private async Task IssueAsync()
    {
        var steamId = _steamIdBox.Text.Trim();
        if (steamId.Length != 17 || !steamId.All(char.IsDigit))
        {
            MessageBox.Show(this, "Нужен Steam64ID — ровно 17 цифр.", "ГАБДД",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            MessageBox.Show(this, "Не задан Steam Web API key.", "ГАБДД",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        _issueButton.Enabled = false;
        _loadFriendsButton.Enabled = false;
        _statusLabel.ForeColor = Color.DimGray;
        _statusLabel.Text = "Опрашиваем Steam…";
        UseWaitCursor = true;

        try
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "cache");
            using var client = new SteamApiClient(_apiKey, cacheDir);
            var snapshot = await Task.Run(async () => await client.FetchProfileAsync(steamId, ct), ct);
            _lastSnapshot = snapshot;
            var license = LicenseBuilder.Build(snapshot, CurrentGender());
            FriendsCache.SaveGenderAddress(CurrentGender());
            await BindLicenseAsync(license, ct);
            _statusLabel.Text = $"Готово: {license.Profile.PersonaName} · LVL {license.Profile.SteamLevel}";
            _statusLabel.ForeColor = Color.ForestGreen;
            _tabs.SelectedIndex = 0;
        }
        catch (OperationCanceledException)
        {
            _statusLabel.Text = "Отменено.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = "Ошибка опроса.";
            _statusLabel.ForeColor = Color.Firebrick;
            MessageBox.Show(this, ex.Message, "Ошибка Steam API",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _issueButton.Enabled = true;
            _loadFriendsButton.Enabled = true;
        }
    }

    private async Task BindLicenseAsync(GamerLicense license, CancellationToken ct)
    {
        await _front.BindAsync(license, ct);
        _back.Bind(license);
        _personality.Bind(license);
        _top.Bind(license);
        _receipt.Bind(license);
    }
}
