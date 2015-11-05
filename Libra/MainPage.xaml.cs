using System;
using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;


namespace Libra
{
    /// <summary>
    /// A home page for the App. This page is shown when the app is launched without openning any file.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public const string MRU_DELIMITER = "<###>";
        private const string PREFIX_RECENT_FILE = "RecentFile#";
        //private CultureInfo culture = new CultureInfo("en-us");
        private ObservableCollection<RecentFile> mruFiles = new ObservableCollection<RecentFile>();

        public MainPage()
        {
            this.InitializeComponent();
            AppEventSource.Log.Debug("MainPage: Initialized.");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // Show most recent files
            mruFiles = new ObservableCollection<RecentFile>();
            AccessListEntryView mruEntries = StorageApplicationPermissions.MostRecentlyUsedList.Entries;
            // If no recent file
            if (mruEntries.Count == 0)
            {
                this.recentFileTitle.Text = "No Recent File.";
                AppEventSource.Log.Debug("MainPage: No recent file found.");
            }
            else
            {
                // Show a list of recent used file
                for (int i = 0; i < mruEntries.Count; i++)
                {
                    AccessListEntry entry = mruEntries[i];
                    RecentFile file = new RecentFile(entry.Token);
                    string[] split = entry.Metadata.Split(new string[] { MRU_DELIMITER }, 2, StringSplitOptions.RemoveEmptyEntries);
                    file.Filename = split[0];
                    file.LastAccessTime = Convert.ToDateTime(split[1]);
                    file.Identifier = PREFIX_RECENT_FILE + i.ToString();
                    mruFiles.Add(file);
                    if (i == 10) break;
                }
                this.RecentFileList.DataContext = mruFiles;
                AppEventSource.Log.Debug("MainPage: Recent files added.");
            }
        }

        /// <summary>
        /// Open a recent used file when an item is clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RecentFileItem_Click(object sender, RoutedEventArgs e)
        {
            AppEventSource.Log.Debug("MainPage: Recent file clicked.");
            RecentFile file = (RecentFile)((HyperlinkButton)e.OriginalSource).DataContext;
            StorageFile pdfFile = await StorageApplicationPermissions.MostRecentlyUsedList.GetFileAsync(file.mruToken);
            // Update recent file list
            StorageApplicationPermissions.MostRecentlyUsedList.Add(pdfFile, pdfFile.Name + MRU_DELIMITER + DateTime.Now.ToString());
            SuspensionManager.pdfFile = pdfFile;
            this.Frame.Navigate(typeof(ViewerPage));
        }

        /// <summary>
        /// Open a file picker for the user to select a pdf file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OpenNew_Click(object sender, RoutedEventArgs e)
        {
            // Select a pdf file
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            openPicker.FileTypeFilter.Add(".pdf");
            StorageFile pdfFile = await openPicker.PickSingleFileAsync();
            // Add file to recent file list
            if (pdfFile != null)
            {
                AppEventSource.Log.Debug("MainPage: Opening new file.");
                StorageApplicationPermissions.MostRecentlyUsedList.Add(pdfFile, pdfFile.Name + MRU_DELIMITER + DateTime.Now.ToString());
                SuspensionManager.pdfFile = pdfFile;
                this.Frame.Navigate(typeof(ViewerPage));
            }
        }
    }
}
