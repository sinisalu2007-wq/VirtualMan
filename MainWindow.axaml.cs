using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using VirtualMan.Systems;

namespace VirtualMan;

public partial class MainWindow : Window
{
    private readonly VirtualPerson _person = new();

    public MainWindow()
    {
        InitializeComponent();
        UpdateUI();
    }

    private void OnTickClicked(object? sender, RoutedEventArgs e)
    {
        _person.RunTick();
        UpdateUI();
    }

    private void OnRestoreClicked(object? sender, RoutedEventArgs e)
    {
        _person.Reset();
        UpdateUI();
    }

    // Direct Anatomical Click Handlers to Crash/Toggle Organs
    private void OnHeartClicked(object? sender, RoutedEventArgs e) { ToggleOrCrash("Heart"); }
    private void OnLungsClicked(object? sender, RoutedEventArgs e) { ToggleOrCrash("Lungs"); }
    private void OnBrainstemClicked(object? sender, RoutedEventArgs e) { ToggleOrCrash("Brainstem"); }
    private void OnLiverClicked(object? sender, RoutedEventArgs e) { ToggleOrCrash("Liver"); }
    private void OnKidneysClicked(object? sender, RoutedEventArgs e) { ToggleOrCrash("Kidneys"); }
    private void OnPancreasClicked(object? sender, RoutedEventArgs e) { ToggleOrCrash("Pancreas"); }
    private void OnStomachClicked(object? sender, RoutedEventArgs e) { ToggleOrCrash("Stomach"); }
    private void OnEarClicked(object? sender, RoutedEventArgs e) { ToggleOrCrash("Ear"); }

    private void ToggleOrCrash(string organName)
    {
        var organ = _person.FindOrgan(organName);
        if (organ != null)
        {
            if (organ.IsFunctional) organ.Crash();
            else organ.Restore();
        }
        _person.RunTick();
        UpdateUI();
    }

    private void UpdateUI()
    {
        // 1. Update Top Status Banner
        TopStatusBanner.Text = _person.IsAlive ? "Status: SYSTEM NOMINAL / ACTIVE" : "Status: CRITICAL SYSTEM HALT (MODS)";
        TopStatusBanner.Foreground = _person.IsAlive ? Brushes.LightGreen : Brushes.IndianRed;

        // 2. Update Anatomical Button Colors & Status Text
        UpdateOrganButton(BtnHeart, "Heart");
        UpdateOrganButton(BtnLungs, "Lungs");
        UpdateOrganButton(BtnBrainstem, "Brainstem");
        UpdateOrganButton(BtnLiver, "Liver");
        UpdateOrganButton(BtnKidneys, "Kidneys");
        UpdateOrganButton(BtnPancreas, "Pancreas");
        UpdateOrganButton(BtnStomach, "Stomach");
        UpdateOrganButton(BtnEar, "Ear");

        // 3. Update Gait / Locomotor Feedback
        var locomotor = _person.Systems.OfType<LocomotorSystem>().FirstOrDefault();
        string gait = locomotor?.EvaluateWalk(_person.Systems.ToList()) ?? "Normal";
        AvatarGaitText.Text = $"Gait State: {gait}";

        // 4. Dynamically Rebuild Left Column System Cards from Backend Engine
        SystemCardsContainer.Children.Clear();
        foreach (var system in _person.Systems)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#252D3D")),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10)
            };

            var stack = new StackPanel { Spacing = 4 };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var sysTitle = new TextBlock
            {
                Text = system.SystemName.ToUpper(),
                FontWeight = FontWeight.Bold,
                FontSize = 11,
                Foreground = Brushes.White
            };
            Grid.SetColumn(sysTitle, 0);

            var sysStatus = new TextBlock
            {
                Text = system.IsOperatingNormally() ? "● NOMINAL" : "● FAILED",
                FontSize = 10,
                FontWeight = FontWeight.Bold,
                Foreground = system.IsOperatingNormally() ? Brushes.LightGreen : Brushes.IndianRed
            };
            Grid.SetColumn(sysStatus, 1);

            headerGrid.Children.Add(sysTitle);
            headerGrid.Children.Add(sysStatus);
            stack.Children.Add(headerGrid);

            // Add Organs under this system with health & metrics
            foreach (var organ in system.Organs)
            {
                string extra = "";
                if (organ is PancreasOrgan p) extra = $" [Energy: {p.EnergyReserve}%]";
                else if (organ is LiverOrgan l) extra = $" [Toxins: {l.ToxicityLevel}%]";
                else if (organ is KidneyOrgan k) extra = $" [Toxins: {k.ToxinLevel}%]";

                var organText = new TextBlock
                {
                    Text = $" • {organ.Name}: {organ.Health}%{extra}",
                    FontSize = 11,
                    Foreground = organ.IsFunctional ? Brushes.LightGray : Brushes.IndianRed
                };
                stack.Children.Add(organText);
            }

            card.Child = stack;
            SystemCardsContainer.Children.Add(card);
        }

        // 5. Push Latest Log to Telemetry Panel
        if (!string.IsNullOrEmpty(_person.LastLog))
        {
            LogContainer.Children.Insert(0, new TextBlock
            {
                Text = $"[{DateTime.Now:HH:mm:ss}] {_person.LastLog}",
                FontSize = 11,
                Foreground = _person.IsAlive ? Brushes.LightGray : Brushes.IndianRed,
                TextWrapping = TextWrapping.Wrap
            });
        }
    }

    private void UpdateOrganButton(Button btn, string organName)
    {
        var organ = _person.FindOrgan(organName);
        if (organ != null)
        {
            btn.Content = $"{organName}\n{organ.Health}%";

            if (organ.IsFunctional)
            {
                var color = Color.Parse(GetOrganDefaultColor(organName));
                btn.Background = new SolidColorBrush(color);
            }
            else
            {
                btn.Background = Brushes.IndianRed;
            }

            btn.Foreground = Brushes.White;
        }
    }

    private string GetOrganDefaultColor(string organName) => organName switch
    {
        "Heart" => "#E53E3E",
        "Lungs" => "#3182CE",
        "Brainstem" => "#48BB78",
        "Liver" => "#38A169",
        "Stomach" => "#D69E2E",
        "Kidneys" => "#805AD5",
        "Pancreas" => "#ED8936",
        "Ear" => "#4A5568",
        _ => "#4A5568"
    };
}
