using System;
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
using System.Collections.Generic;
using Windows.UI.Popups;

namespace Libra
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        // App settings keys
        public const string REOPEN_FILE = "reopenFile";
        public const string RESTORE_VIEW = "restoreView";
        public const string SHOW_RECENT_FILES = "showRecentFiles";
        public const string DEBUG_LOGGING = "debugLogging";
        public const string INKING_WARNING = "inkingWarning";
        public const string ERASER_WARNING = "eraserWarning";
        public const string TUTORIAL = "showTutorial";

        private const string NOTIFICATION_OK = "OK";
        private const string LOG_FILE_NAME = "LibraAppLog";

        private StorageFileEventListener libraListener;
        public static LicenseInformation licenseInformation;

        public static Dictionary<string, object> AppSettings { get; set; }

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

            // Get the license info
            // The next line is commented out for testing.
            // licenseInformation = CurrentApp.LicenseInformation;

            // The next line is commented out for production/release.       
            licenseInformation = CurrentApp.LicenseInformation;

            // Initialize App settings
            AppSettings = new Dictionary<string, object>();
            AppSettings.Add(REOPEN_FILE, true);
            AppSettings.Add(RESTORE_VIEW, true);
            AppSettings.Add(SHOW_RECENT_FILES, true);
            AppSettings.Add(DEBUG_LOGGING, true);
            AppSettings.Add(INKING_WARNING, true);
            AppSettings.Add(ERASER_WARNING, true);
            AppSettings.Add(TUTORIAL, true);
            

            // Load App settings
            List<string> keys = new List<string>(AppSettings.Keys);
            foreach (string key in keys)
            {
                object obj = ApplicationData.Current.RoamingSettings.Values[key];
                if (obj != null)
                {
                    AppSettings[key] = obj;
                }
            }

            // Enable Logging
            EventLevel eLevel;
            if ((bool)AppSettings[App.DEBUG_LOGGING])
                eLevel = EventLevel.Verbose;
            else eLevel = EventLevel.Informational;
            libraListener = new StorageFileEventListener(LOG_FILE_NAME);
            libraListener.EnableEvents(AppEventSource.Log, eLevel);
            AppEventSource.Log.Info("********** App is starting **********");
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

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated || 
                    (e.PreviousExecutionState == ApplicationExecutionState.ClosedByUser && (bool)App.AppSettings[REOPEN_FILE]))
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
                if ((bool)AppSettings[App.TUTORIAL])
                {
                    // Show tutorial if it is starting for the first time.
                    AppEventSource.Log.Info("Starting App for the first time.");
                    shell.AppFrame.Navigate(typeof(TutorialPage), e.Arguments, new Windows.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo());
                }
                else
                {
                    // When the navigation stack isn't restored, navigate to the first page
                    // suppressing the initial entrance animation.
                    AppEventSource.Log.Info("App: Suspended state not found or not restored. Navigating to MainPage.");
                    shell.AppFrame.Navigate(typeof(MainPage), e.Arguments, new Windows.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo());
                }
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
#if DEBUG
            AppEventSource.Log.Error(
                "Total inkCanvas created: " + inkcanvasCount.ToString() +
                "\n Total inkCanvas removed: " + inkcanvasRemoved.ToString() +
                "\n Active inkCanvas: " + inkcanvasActive.ToString() +
                "\n Total image created: " + imageCount.ToString() +
                "\n Total image removed: " + imageRemoved.ToString() +
                "\n Pages Queue: " + pageQueueCount.ToString()
                );
#endif
            libraListener.Flush();
        }

        public static async System.Threading.Tasks.Task<bool> RemoveAdsClick(object sender, RoutedEventArgs e)
        {
            if (!CurrentApp.LicenseInformation.ProductLicenses["removedAds"].IsActive)
            {
                try
                {
                    PurchaseResults result = await CurrentApp.RequestProductPurchaseAsync("removedAds");
                    if (result.Status == ProductPurchaseStatus.Succeeded)
                    {
                        // Update license information
                        licenseInformation = CurrentApp.LicenseInformation;
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

        /// <summary>
        /// Show a dialog with notification message.
        /// </summary>
        /// <param name="message">The message to be displayed to the user.</param>
        public static async void NotifyUser(Type sender, string message, bool logMessage = false)
        {
            MessageDialog messageDialog = new MessageDialog(message);
            messageDialog.Commands.Add(new UICommand(NOTIFICATION_OK, null, 0));
            await messageDialog.ShowAsync();
            if (logMessage) AppEventSource.Log.Error(sender.ToString() + ": " + message);
        }

#if DEBUG
        // DEBUG ONLY VARIABLES
        public static int inkcanvasCount = 0;
        public static int imageCount = 0;
        public static int inkcanvasActive = 0;
        public static int imageActive = 0;
        public static int inkcanvasRemoved = 0;
        public static int imageRemoved = 0;
        public static int pageQueueCount = 0;
#endif
    }
}
