using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;
using NavigationMenu;
using System.Diagnostics.Tracing;

namespace Libra
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        private StorageFileEventListener libraListener;

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
            AppEventSource.Log.Info("***** App is starting *****");
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
                shell = new NavigationPage();

                // Register the Frame in navigation page to suspension manager
                SuspensionManager.RegisterFrame(shell.AppFrame);
                AppEventSource.Log.Debug("App: AppFrame registered in suspension manager.");

                // Set the default language
                shell.Language = Windows.Globalization.ApplicationLanguages.Languages[0];

                shell.AppFrame.NavigationFailed += OnNavigationFailed;

                //if (e.PreviousExecutionState == ApplicationExecutionState.Terminated || e.PreviousExecutionState == ApplicationExecutionState.ClosedByUser)
                {
                    // Load state from previously suspended application
                    AppEventSource.Log.Debug("App: Checking previously suspended state.");
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
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            AppEventSource.Log.Critical("App: Failed to load Page " + e.SourcePageType.FullName);
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
            AppEventSource.Log.Info("App: Suspending");
            // Save application state and stop any background activity
            await SuspensionManager.SaveSessionAsync();
            AppEventSource.Log.Info("App: Suspension Completed.");
            libraListener.Flush();
            deferral.Complete();
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            AppEventSource.Log.Error("App: Unhandled Exception. " + e.Message);
            libraListener.Flush();
        }
    }
}
