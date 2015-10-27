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
        private const string viewerStateExt = ".xml";
        private static Frame appFrame;

        /// <summary>
        /// Provides access to global session state for the current session.  This state is
        /// serialized by <see cref="SaveSessionAsync"/> and restored by
        /// <see cref="RestoreSessionAsync"/>, so values must be serializable by
        /// <see cref="DataContractSerializer"/> and should be as compact as possible.  Strings
        /// and other self-contained data types are strongly recommended.
        /// </summary>
        public static SessionState sessionState
        {
            get;
            set;
        }

        public static ViewerState viewerState
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
        /// Save the current <see cref="sessionState"/>.  Any <see cref="Frame"/> instances
        /// registered with <see cref="RegisterFrame"/> will also preserve their current
        /// navigation stack, which in turn gives their active <see cref="Page"/> an opportunity
        /// to save its state.
        /// </summary>
        /// <returns>An asynchronous task that reflects when session state has been saved.</returns>
        public static async Task SaveSessionAsync()
        {
            appFrame.Navigate(typeof(BlankPage), null, new Windows.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo());
            await SaveViewerAsync();
            if (sessionState != null)
            {
                AppEventSource.Log.Debug("Suspension: Saving session state to file...");
                await SerializeToFileAsync(sessionState, typeof(SessionState), sessionStateFilename);
            }
        }

        /// <summary>
        /// Restores previously saved <see cref="sessionState"/>.  Any <see cref="Frame"/> instances
        /// registered with <see cref="RegisterFrame"/> will also restore their prior navigation
        /// state, which in turn gives their active <see cref="Page"/> an opportunity restore its
        /// state.
        /// </summary>
        /// <param name="sessionBaseKey">An optional key that identifies the type of session.
        /// This can be used to distinguish between multiple application launch scenarios.</param>
        /// <returns>An asynchronous task that reflects when session state has been read.  The
        /// content of <see cref="sessionState"/> should not be relied upon until this task
        /// completes.</returns>
        public static async Task RestoreSessionAsync(String sessionBaseKey = null)
        {
            AppEventSource.Log.Debug("Suspension: Checking previously saved session state...");
            sessionState = await DeserializeFromFileAsync(typeof(SessionState), sessionStateFilename, true) as SessionState;

            if (sessionState != null && sessionState.FileToken != null)
            {
                if (sessionState.ViewerMode == 1)
                {
                    StorageFile pdfFile = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(sessionState.FileToken);
                    AppEventSource.Log.Info("Suspension: Reopening " + pdfFile.Name);
                    appFrame.Navigate(typeof(ViewerPage), pdfFile);
                }
                else
                {
                    sessionState = null;
                    AppEventSource.Log.Info("Suspension: Viewer was not active when the App was suspended.");
                }
            }
            else AppEventSource.Log.Warn("Suspension: Previously saved session state does not contain file token.");
        }

        public static async Task SaveViewerAsync()
        {
            if (viewerState != null)
            {
                string viewerStateFilename = viewerState.pdfToken + viewerStateExt;
                AppEventSource.Log.Debug("Suspension: Saving viewer state to file...");
                await SerializeToFileAsync(viewerState, typeof(ViewerState), viewerStateFilename);
                viewerState = null;
            }
        }

        public static async Task SerializeToFileAsync(Object obj, Type objectType, string filename)
        {
            try
            {
                AppEventSource.Log.Debug("Suspension: Serializing object..." + objectType.ToString());
                // Serialize the session state synchronously to avoid asynchronous access to shared state
                MemoryStream viewerData = new MemoryStream();
                DataContractSerializer serializer = new DataContractSerializer(objectType);
                serializer.WriteObject(viewerData, obj);
                AppEventSource.Log.Debug("Suspension: Object serialized to memory. " + objectType.ToString());
                // Get an output stream for the SessionState file and write the state asynchronously
                StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
                using (Stream fileStream = await file.OpenStreamForWriteAsync())
                {
                    viewerData.Seek(0, SeekOrigin.Begin);
                    await viewerData.CopyToAsync(fileStream);
                }
                AppEventSource.Log.Debug("Suspension: Object saved to file. " + filename);
            }
            catch (Exception e)
            {
                AppEventSource.Log.Error("Suspension: Error when serializing object. Exception: " + e.Message);
                throw new SuspensionManagerException(e);
            }
        }

        public static async Task<Object> DeserializeFromFileAsync(Type objectType, string filename, bool deleteFile = false)
        {
            bool fileExist = false;
            try
            {
                AppEventSource.Log.Debug("Suspension: Checking file..." + filename);
                // Get the input stream for the file
                StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync(filename);
                fileExist = true;
                Object obj;
                using (IInputStream inStream = await file.OpenSequentialReadAsync())
                {
                    // Deserialize the Session State
                    DataContractSerializer serializer = new DataContractSerializer(objectType);
                    obj = serializer.ReadObject(inStream.AsStreamForRead());
                }
                AppEventSource.Log.Debug("Suspension: Object loaded from file. " + objectType.ToString());


                // Delete the file
                if (deleteFile)
                {
                    await file.DeleteAsync();
                    fileExist = false;
                    AppEventSource.Log.Info("Suspension: File deleted. " + filename);
                }

                return obj;
            }
            catch (Exception e)
            {
                if (e is FileNotFoundException)
                {
                    AppEventSource.Log.Debug("Suspension: File not found.");
                    fileExist = false;
                    return null;
                }
                AppEventSource.Log.Error("Suspension: Error when deserializing object. Exception: " + e.Message);
                deleteFile = true;
                throw new SuspensionManagerException(e);
            }
            finally
            {
                // Delete the file if error occured
                if (fileExist)
                {
                    StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync(filename);
                    await file.DeleteAsync();
                    AppEventSource.Log.Info("Suspension: File deleted due to error.");
                }
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
