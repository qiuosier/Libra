using Microsoft.AdMediator.Core.Models;
using System;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Libra
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        /// <summary>
        /// Do not display ads if the window height is smaller than this number
        /// </summary>
        private const int MIN_WINDOW_HEIGHT_FOR_ADS = 650;

        public SettingsPage()
        {
            this.InitializeComponent();
            this.AdMediator_F5AAF9.AdSdkOptionalParameters[AdSdkNames.Smaato]["Width"] = 728;
            this.AdMediator_F5AAF9.AdSdkOptionalParameters[AdSdkNames.Smaato]["Height"] = 90;
            // Remove ads if purchased
            if (App.licenseInformation.ProductLicenses["removedAds"].IsActive)
                RemoveAds();
            // Load current settings
            this.toggleSwitchLogging.IsOn = (bool)App.AppSettings["debugLogging"];
            this.toggleSwitchReopenFile.IsOn = (bool)App.AppSettings["reopenFile"];
            this.toggleSwitchRestoreView.IsOn = (bool)App.AppSettings["restoreView"];
            this.toggleSwitchShowRecentFiles.IsOn = (bool)App.AppSettings["showRecentFiles"];
        }

        /// <summary>
        /// Open local app data folder
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void OpenLocalFolder_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchFolderAsync(ApplicationData.Current.LocalFolder);
        }

        /// <summary>
        /// Event handler for clicking remove ads button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void RemoveAdBtn_Click(object sender, RoutedEventArgs e)
        {
            if (await App.RemoveAdsClick(sender, e)) RemoveAds();
        }

        /// <summary>
        /// Remove Ads
        /// </summary>
        private void RemoveAds()
        {
            this.AdMediator_F5AAF9.Visibility = Visibility.Collapsed;
            this.RemoveAdBtn.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Adjust the UI based on the window height.
        /// This event handler does the same thing as adaptive trigger.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Window.Current.Bounds.Height > MIN_WINDOW_HEIGHT_FOR_ADS)
            {
                this.optionalPanelGrid.Visibility = Visibility.Visible;
                this.optionalPanelGrid.Height = Window.Current.Bounds.Height - this.settingsPanel.ActualHeight - 70;
            }
            else this.optionalPanelGrid.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Clear recently used files
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ClearRecentlyUsedFilesBtn_Click(object sender, RoutedEventArgs e)
        {
            StorageApplicationPermissions.MostRecentlyUsedList.Clear();
            this.ClearRecentlyUsedFilesBtn.IsEnabled = false;
        }

        private void toggleSwitchLogging_Toggled(object sender, RoutedEventArgs e)
        {
            App.AppSettings["debugLogging"] = this.toggleSwitchLogging.IsOn;
            ApplicationData.Current.RoamingSettings.Values["debugLogging"] = this.toggleSwitchLogging.IsOn;
        }

        private void toggleSwitchReopenFile_Toggled(object sender, RoutedEventArgs e)
        {
            App.AppSettings["reopenFile"] = this.toggleSwitchReopenFile.IsOn;
            ApplicationData.Current.RoamingSettings.Values["reopenFile"] = this.toggleSwitchReopenFile.IsOn;
        }

        private void toggleSwitchRestoreView_Toggled(object sender, RoutedEventArgs e)
        {
            App.AppSettings["restoreView"] = this.toggleSwitchRestoreView.IsOn;
            ApplicationData.Current.RoamingSettings.Values["restoreView"] = this.toggleSwitchRestoreView.IsOn;
        }

        private void toggleSwitchShowRecentFiles_Toggled(object sender, RoutedEventArgs e)
        {
            App.AppSettings["showRecentFiles"] = this.toggleSwitchShowRecentFiles.IsOn;
            ApplicationData.Current.RoamingSettings.Values["showRecentFiles"] = this.toggleSwitchShowRecentFiles.IsOn;
        }
    }
}
