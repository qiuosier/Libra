﻿using System;
using System.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;
using NavigationMenu;
using System.Diagnostics.Tracing;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.ApplicationModel.Store;

namespace Libra
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        private StorageFileEventListener libraListener;
        public static LicenseInformation licenseInformation;
        //public static LicenseInformation LicenseInformation { get { return _licenseInformation; } }

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            Microsoft.ApplicationInsights.WindowsAppInitializer.InitializeAsync(
                Microsoft.ApplicationInsights.WindowsCollectors.Metadata |
                Microsoft.ApplicationInsights.WindowsCollectors.Session);
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            this.UnhandledException += OnUnhandledException;

            // Enable Logging
            libraListener = new StorageFileEventListener("LibraAppLog");
            libraListener.EnableEvents(AppEventSource.Log, EventLevel.Verbose);
            AppEventSource.Log.Info("********** App is starting **********");

            // Get the license info
            // The next line is commented out for testing.
            // licenseInformation = CurrentApp.LicenseInformation;

            // The next line is commented out for production/release.       
            licenseInformation = CurrentAppSimulator.LicenseInformation;
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected async override void OnLaunched(LaunchActivatedEventArgs e)
        {

#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
            {
                //this.DebugSettings.EnableFrameRateCounter = true;
            }
#endif

            NavigationPage shell = Window.Current.Content as NavigationPage;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (shell == null)
            {
                // Create a navigation page to act as the navigation context and navigate to the first page
                shell = CreateNewNavigationPage();

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated || e.PreviousExecutionState == ApplicationExecutionState.ClosedByUser)
                {
                    // Load state from previously suspended application
                    //AppEventSource.Log.Debug("App: Checking previously suspended state...");
                    await SuspensionManager.RestoreSessionAsync();
                }
            }
            else
            {
                AppEventSource.Log.Info("App: Window content is not null. App is already running.");
            }

            // Place our app shell in the current Window
            Window.Current.Content = shell;

            if (shell.AppFrame.Content == null)
            {
                // When the navigation stack isn't restored, navigate to the first page
                // suppressing the initial entrance animation.
                AppEventSource.Log.Info("App: Suspended state not found or not restored. Navigating to MainPage.");
                shell.AppFrame.Navigate(typeof(MainPage), e.Arguments, new Windows.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo());
            }

            // Ensure the current window is active
            Window.Current.Activate();
        }

        /// <summary>
        /// Create a new navigation page and register it in the suspension manager. Language selection is also handled here.
        /// </summary>
        /// <returns></returns>
        private NavigationPage CreateNewNavigationPage()
        {
            // Create a navigation page to act as the navigation context and navigate to the first page
            NavigationPage shell = new NavigationPage();
            shell.AppFrame.NavigationFailed += OnNavigationFailed;
            // Register the Frame in navigation page to suspension manager
            SuspensionManager.RegisterFrame(shell.AppFrame);
            AppEventSource.Log.Debug("App: AppFrame registered in suspension manager.");

            // Set the default language
            shell.Language = Windows.Globalization.ApplicationLanguages.Languages[0];

            return shell;
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            AppEventSource.Log.Info("App: Suspending...");
            // Log the time used in suspension
            System.Diagnostics.Stopwatch suspensionWatch = new System.Diagnostics.Stopwatch();
            suspensionWatch.Start();
            // Save application state
            await SuspensionManager.SaveSessionAsync();
            suspensionWatch.Stop();
            AppEventSource.Log.Info("App: Suspension Completed in " + suspensionWatch.Elapsed.TotalSeconds.ToString() + " seconds.");
            // Save log file
            libraListener.Flush();
            deferral.Complete();
        }

        /// <summary>
        /// Navigate to viewer page when the app is activated by openning a pdf file. Initialize the navigation page if necessary.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnFileActivated(FileActivatedEventArgs e)
        {
            AppEventSource.Log.Debug("App: Activated by opening pdf file.");
            NavigationPage shell = Window.Current.Content as NavigationPage;

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (shell == null)
            {
                // Create a navigation page to act as the navigation context and navigate to the first page
                shell = CreateNewNavigationPage();
            }

            // Place our app shell in the current Window
            Window.Current.Content = shell;

            // Only open the first file, if there are more than one files
            StorageFile pdfFile = (StorageFile) e.Files[0];

            // Update recent file list
            StorageApplicationPermissions.MostRecentlyUsedList.Add(pdfFile, pdfFile.Name + MainPage.MRU_DELIMITER + DateTime.Now.ToString());
            SuspensionManager.pdfFile = pdfFile;
            shell.AppFrame.Navigate(typeof(ViewerPage));

            // Ensure the current window is active
            Window.Current.Activate();
        }

        /// <summary>
        /// Use this to log unhandled exceptions.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e != null)
            {
                Exception exception = e.Exception;
                if (exception is NullReferenceException && exception.ToString().ToUpper().Contains("SOMA"))
                {
                    AppEventSource.Log.Debug("Handled Smaato null reference exception " + e.Message);
                    e.Handled = true;
                    return;
                }
            }
            AppEventSource.Log.Error("App: Unhandled Exception. " + sender.ToString() + e.Message);
            libraListener.Flush();
        }

        public static async System.Threading.Tasks.Task<bool> RemoveAdsClick(object sender, RoutedEventArgs e)
        {
            if (!CurrentAppSimulator.LicenseInformation.ProductLicenses["removedAds"].IsActive)
            {
                try
                {
                    PurchaseResults result = await CurrentAppSimulator.RequestProductPurchaseAsync("removedAds");
                    if (result.Status == ProductPurchaseStatus.Succeeded)
                    {
                        // Update license information
                        licenseInformation = CurrentAppSimulator.LicenseInformation;
                        return true;
                    }
                    else return false;
                }
                catch (Exception)
                {
                    // The in-app purchase was not completed because 
                    // an error occurred.
                    return false;
                }
            }
            else
            {
                // The customer already owns this feature.
                return true;
            }
        }
    }
}
