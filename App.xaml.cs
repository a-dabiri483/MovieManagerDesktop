using System.Configuration;
using System.Data;
using System.Windows;
using System.Configuration;
using System.Data;
using System.Windows;

namespace MovieManagerDesktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;

        var settings = MovieManagerDesktop.Services.SettingsManager.LoadSettings();
        ApplyTheme(settings.Theme ?? "Cyan", settings.IsDarkTheme);

        using (var db = new MovieManagerDesktop.Data.AppDbContext())
        {
            db.Database.EnsureCreated();
        }
    }

    private void ApplyTheme(string themeName, bool isDarkTheme)
    {
        var paletteHelper = new MaterialDesignThemes.Wpf.PaletteHelper();
        MaterialDesignThemes.Wpf.Theme theme;
        
        var baseTheme = isDarkTheme ? MaterialDesignThemes.Wpf.BaseTheme.Dark : MaterialDesignThemes.Wpf.BaseTheme.Light;
        System.Windows.Media.Color primaryColor;
        System.Windows.Media.Color secondaryColor;
        
            if (themeName == "Cyan")
            {
                primaryColor = System.Windows.Media.Color.FromRgb(0, 180, 216); // Cyan #00B4D8
                secondaryColor = System.Windows.Media.Color.FromRgb(58, 134, 255); // Blue #3A86FF
                theme = MaterialDesignThemes.Wpf.Theme.Create(baseTheme, primaryColor, secondaryColor);
            }
        else if (themeName == "MidnightBlue")
        {
            primaryColor = System.Windows.Media.Color.FromRgb(25, 118, 210);
            secondaryColor = System.Windows.Media.Color.FromRgb(3, 169, 244);
            theme = MaterialDesignThemes.Wpf.Theme.Create(baseTheme, primaryColor, secondaryColor);
        }
        else // OLEDBlack
        {
            primaryColor = System.Windows.Media.Color.FromRgb(33, 33, 33);
            secondaryColor = System.Windows.Media.Color.FromRgb(158, 158, 158);
            theme = MaterialDesignThemes.Wpf.Theme.Create(baseTheme, primaryColor, secondaryColor);
        }

        paletteHelper.SetTheme(theme);

        // Swap our custom DesignSystem light/dark resource
        var appDictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var existingLightDict = appDictionaries.FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("DesignSystem.Light.xaml"));
        
        if (isDarkTheme)
        {
            if (existingLightDict != null)
            {
                appDictionaries.Remove(existingLightDict);
            }
        }
        else
        {
            if (existingLightDict == null)
            {
                appDictionaries.Add(new System.Windows.ResourceDictionary { Source = new System.Uri("pack://application:,,,/MovieManagerDesktop;component/Themes/DesignSystem.Light.xaml") });
            }
        }
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        MovieManagerDesktop.Services.LoggerService.Error("Unhandled exception occurred in application.", e.Exception);
        System.Windows.MessageBox.Show($"خطای سیستمی رخ داد:\n{e.Exception.Message}\n\n{e.Exception.InnerException?.Message}", "خطای برنامه", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        e.Handled = true; 
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        // Fire-and-forget the backup task so it doesn't block UI excessively, but wait for it to complete if possible
        // Actually, since OnExit is synchronous mostly, a Task.Run and Wait will ensure it finishes before process death
        try 
        {
            await System.Threading.Tasks.Task.Run(async () => await MovieManagerDesktop.Services.BackupManager.RunBackupAsync());
        }
        catch { }
    }
}
