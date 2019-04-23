using System.Windows;
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

    private void OnLoaded( object sender, RoutedEventArgs e ) {
      var hasEmail = !string.IsNullOrWhiteSpace( ViewModel.Email );
      var hasApiToken = !string.IsNullOrWhiteSpace( ViewModel.ApiToken );
      
      if ( !hasEmail ) {
        txtEmail.Focus();
      }
      else if( !hasApiToken ) {
        txtApiToken.Focus();
      }
      else {
        ViewModel.ConnectCommand.Execute( null );
      }
    }
  }
}
