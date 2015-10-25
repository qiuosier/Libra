﻿using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Libra
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const string MRU_DELIMITER = "<###>";
        private const string PREFIX_RECENT_FILE = "RecentFile#";
        //private CultureInfo culture = new CultureInfo("en-us");
        private ObservableCollection<RecentFile> mruFiles = new ObservableCollection<RecentFile>();

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // Show most recent files
            mruFiles = new ObservableCollection<RecentFile>();
            AccessListEntryView mruEntries = StorageApplicationPermissions.MostRecentlyUsedList.Entries;
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
            if (SuspensionManager.LastViewerState != null)
            {
                SuspensionManager.LastViewerState.IsCurrentView = false;
            }
        }

        private async void RecentFileItem_Click(object sender, RoutedEventArgs e)
        {
            RecentFile file = (RecentFile)((HyperlinkButton)e.OriginalSource).DataContext;
            StorageFile pdfFile = await StorageApplicationPermissions.MostRecentlyUsedList.GetFileAsync(file.mruToken);
            // Update recent file list
            StorageApplicationPermissions.MostRecentlyUsedList.Add(pdfFile, pdfFile.Name + MRU_DELIMITER + DateTime.Now.ToString());
            this.Frame.Navigate(typeof(ViewerPage), pdfFile);
        }

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
                StorageApplicationPermissions.MostRecentlyUsedList.Add(pdfFile, pdfFile.Name + MRU_DELIMITER + DateTime.Now.ToString());
                this.Frame.Navigate(typeof(ViewerPage), pdfFile);
            }
        }
    }
}
