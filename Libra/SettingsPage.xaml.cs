using Microsoft.AdMediator.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Libra
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
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
    }
}
