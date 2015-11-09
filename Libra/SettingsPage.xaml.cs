using Microsoft.AdMediator.Core.Models;
using System;
using Windows.Storage;
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
        private const int MIN_WINDOW_HEIGHT_FOR_ADS = 675;
        private const int WINDOW_CONTENT_HEIGHT = 530;

        public SettingsPage()
        {
            this.InitializeComponent();
            this.AdMediator_F5AAF9.AdSdkOptionalParameters[AdSdkNames.Smaato]["Width"] = 728;
            this.AdMediator_F5AAF9.AdSdkOptionalParameters[AdSdkNames.Smaato]["Height"] = 90;
            // Remove ads if purchased
            if (App.licenseInformation.ProductLicenses["removedAds"].IsActive)
                RemoveAds();
        }

        private async void OpenLocalFolder_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchFolderAsync(ApplicationData.Current.LocalFolder);
        }

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

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Window.Current.Bounds.Height > MIN_WINDOW_HEIGHT_FOR_ADS)
            {
                this.optionalPanelGrid.Visibility = Visibility.Visible;
                this.optionalPanelGrid.Height = Window.Current.Bounds.Height - WINDOW_CONTENT_HEIGHT;
            }
            else this.optionalPanelGrid.Visibility = Visibility.Collapsed;
        }
    }
}
