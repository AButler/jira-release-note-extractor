using System.Windows.Input;
using JiraReleaseNoteExtractor.ViewModels;

namespace JiraReleaseNoteExtractor {
  public partial class MainWindow {
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    public MainWindow() {
      InitializeComponent();
    }

    private void TxtPasswordOnKeyDown( object sender, KeyEventArgs e ) {
      if ( e.Key != Key.Enter ) {
        return;
      }

      ViewModel.ConnectCommand.Execute( null );
    }
  }
}
