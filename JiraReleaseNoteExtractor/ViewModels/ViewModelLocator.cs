namespace JiraReleaseNoteExtractor.ViewModels {
  internal class ViewModelLocator {
    public MainWindowViewModel MainWindow { get; }

    public ViewModelLocator() {
      MainWindow = new MainWindowViewModel();
    }
  }
}
