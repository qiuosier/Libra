using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;

namespace Libra
{
    /// <summary>
    /// SuspensionManager captures global session state to simplify process lifetime management
    /// for an application.  Note that session state will be automatically cleared under a variety
    /// of conditions and should only be used to store information that would be convenient to
    /// carry across sessions, but that should be discarded when an application crashes or is
    /// upgraded.
    /// </summary>
    internal sealed class SuspensionManager
    {
        private static List<Type> _knownTypes = new List<Type>();
        private const string sessionStateFilename = "_sessionState.xml";
        private static Frame appFrame;

        /// <summary>
        /// Provides access to global session state for the current session.  This state is
        /// serialized by <see cref="SaveAsync"/> and restored by
        /// <see cref="RestoreAsync"/>, so values must be serializable by
        /// <see cref="DataContractSerializer"/> and should be as compact as possible.  Strings
        /// and other self-contained data types are strongly recommended.
        /// </summary>
        public static ViewerState LastViewerState
        {
            get;
            set;
        }

        /// <summary>
        /// List of custom types provided to the <see cref="DataContractSerializer"/> when
        /// reading and writing session state.  Initially empty, additional types may be
        /// added to customize the serialization process.
        /// </summary>
        public static List<Type> KnownTypes
        {
            get { return _knownTypes; }
        }

        public static void RegisterFrame(Frame frame)
        {
            appFrame = frame;
        }

        /// <summary>
        /// Save the current <see cref="SessionState"/>.  Any <see cref="Frame"/> instances
        /// registered with <see cref="RegisterFrame"/> will also preserve their current
        /// navigation stack, which in turn gives their active <see cref="Page"/> an opportunity
        /// to save its state.
        /// </summary>
        /// <returns>An asynchronous task that reflects when session state has been saved.</returns>
        public static async Task SaveAsync()
        {
            try
            {
                appFrame.Navigate(typeof(BlankPage), null, new Windows.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo());

                if (LastViewerState != null)
                {
                    AppEventSource.Log.Debug("Suspension: Saving viewer state.");
                    // Serialize the session state synchronously to avoid asynchronous access to shared
                    // state
                    MemoryStream viewerData = new MemoryStream();
                    DataContractSerializer serializer = new DataContractSerializer(typeof(ViewerState), _knownTypes);
                    serializer.WriteObject(viewerData, LastViewerState);
                    AppEventSource.Log.Debug("Suspension: Viewer state serialized to memory.");
                    // Get an output stream for the SessionState file and write the state asynchronously
                    StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(sessionStateFilename, CreationCollisionOption.ReplaceExisting);
                    using (Stream fileStream = await file.OpenStreamForWriteAsync())
                    {
                        viewerData.Seek(0, SeekOrigin.Begin);
                        await viewerData.CopyToAsync(fileStream);
                    }
                    AppEventSource.Log.Debug("Suspension: Viewer state saved to file.");
                }
            }
            catch (Exception e)
            {
                AppEventSource.Log.Error("Suspension: Error when saving. Exception: " + e.ToString());
                throw new SuspensionManagerException(e);
            }
        }

        /// <summary>
        /// Restores previously saved <see cref="SessionState"/>.  Any <see cref="Frame"/> instances
        /// registered with <see cref="RegisterFrame"/> will also restore their prior navigation
        /// state, which in turn gives their active <see cref="Page"/> an opportunity restore its
        /// state.
        /// </summary>
        /// <param name="sessionBaseKey">An optional key that identifies the type of session.
        /// This can be used to distinguish between multiple application launch scenarios.</param>
        /// <returns>An asynchronous task that reflects when session state has been read.  The
        /// content of <see cref="SessionState"/> should not be relied upon until this task
        /// completes.</returns>
        public static async Task RestoreAsync(String sessionBaseKey = null)
        {
            try
            {
                AppEventSource.Log.Debug("Suspension: Checking previously saved viewer state.");
                // Get the input stream for the SessionState file
                StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync(sessionStateFilename);
                using (IInputStream inStream = await file.OpenSequentialReadAsync())
                {
                    // Deserialize the Session State
                    DataContractSerializer serializer = new DataContractSerializer(typeof(ViewerState), _knownTypes);
                    LastViewerState = (ViewerState)serializer.ReadObject(inStream.AsStreamForRead());
                }
                AppEventSource.Log.Debug("Suspension: Previously saved viewer loaded.");
                // 
                if (LastViewerState.pdfToken != null)
                {
                    if (LastViewerState.IsCurrentView)
                    {
                        LastViewerState.IsRestoring = true;
                        StorageFile pdfFile = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(LastViewerState.pdfToken);
                        AppEventSource.Log.Info("Suspension: Restoring " + pdfFile.Name);
                        appFrame.Navigate(typeof(ViewerPage), pdfFile);
                    }
                    else
                    {
                        LastViewerState = null;
                        AppEventSource.Log.Info("Suspension: Viewer was not active when the App was suspended.");
                    }
                }
                else AppEventSource.Log.Warn("Suspension: Previously saved viewer state does not contain file token.");

                // Clean up the files
                await file.DeleteAsync();
                AppEventSource.Log.Info("Suspension: Previously saved viewer state file deleted.");
            }
            catch (Exception e)
            {
                if (e is FileNotFoundException)
                {
                    AppEventSource.Log.Debug("Suspension: No previously saved viewer state found.");
                    return;
                }
                AppEventSource.Log.Error("Suspension: Error when restoring. Exception: " + e.ToString());
                throw new SuspensionManagerException(e);
            }
        }
    }
    public class SuspensionManagerException : Exception
    {
        public SuspensionManagerException()
        {
        }

        public SuspensionManagerException(Exception e)
            : base("SuspensionManager failed", e)
        {

        }
    }
}
