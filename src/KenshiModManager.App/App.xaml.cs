using System.Windows;
namespace KenshiModManager.App;
public partial class App : Application {
 protected override void OnStartup(StartupEventArgs e) {
  base.OnStartup(e);
  try { var window=new MainWindow(AppOptions.Parse(e.Args)); MainWindow=window; window.Show(); }
  catch(Exception error) { MessageBox.Show(error.Message+"\n\nUse --game-path <folder>, --data-dir <folder> and optionally --read-only.","Unable to start Kenshi Mod Manager",MessageBoxButton.OK,MessageBoxImage.Error); Shutdown(1); }
 }
}
