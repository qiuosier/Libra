using Libra.Class;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Streams;
using Windows.UI.Popups;
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
        private const string FILENAME_SESSION_STATE = "_sessionState.xml";
        public const string FILENAME_VIEWER_STATE = "_viewerState.xml";
        private static Frame appFrame;
        public static bool IsSuspending { get; set; }

        public static StorageFile pdfFile { get; set; }

        /// <summary>
        /// List of custom types provided to the <see cref="DataContractSerializer"/> when
        /// reading and writing session state.  Initially empty, additional types may be
        /// added to customize the serialization process.
        /// </summary>
        public static List<Type> KnownTypes { get; } = new List<Type>();

        /// <summary>
        /// Provides access to global session state for the current session.  This state is
        /// serialized by <see cref="SaveSessionAsync"/> and restored by
        /// <see cref="RestoreSessionAsync"/>, so values must be serializable by
        /// <see cref="DataContractSerializer"/> and should be as compact as possible.  Strings
        /// and other self-contained data types are strongly recommended.
        /// </summary>
        public static SessionState AppSessionState { get; set; }

        /// <summary>
        /// Stores the state of each viewer
        /// </summary>
        public static Dictionary<Guid, ViewerState> ViewerStateDictionary { get; set; }

        public static void RegisterFrame(Frame frame)
        {
            appFrame = frame;
        }

        /// <summary>
        /// Save the current <see cref="AppSessionState"/>.  Any <see cref="Frame"/> instances
        /// registered with <see cref="RegisterFrame"/> will also preserve their current
        /// navigation stack, which in turn gives their active <see cref="Page"/> an opportunity
        /// to save its state.
        /// </summary>
        /// <returns>An asynchronous task that reflects when session state has been saved.</returns>
        public static async Task SaveSessionAsync()
        {
            IsSuspending = true;
            if (AppSessionState != null)
            {
                // Move away from the current page (to a blank page), and then go back, so that the OnNavigatedFrom will be called.
                appFrame.Navigate(typeof(BlankPage), null, new Windows.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo());
                appFrame.GoBack();
                await SaveViewerAsync();
                AppEventSource.Log.Debug("Suspension: Saving session state to file...");
                try
                {
                    StorageFile file = await
                        ApplicationData.Current.LocalFolder.CreateFileAsync(FILENAME_SESSION_STATE, CreationCollisionOption.ReplaceExisting);
                    await SerializeToFileAsync(AppSessionState, typeof(SessionState), file);
                }
                catch (Exception ex)
                {
                    App.NotifyUser(typeof(SuspensionManager), "An Error occurred when saving session state.\n" + ex.Message);
                }
            }
            IsSuspending = false;
        }

        /// <summary>
        /// Restores previously saved <see cref="AppSessionState"/>.  Any <see cref="Frame"/> instances
        /// registered with <see cref="RegisterFrame"/> will also restore their prior navigation
        /// state, which in turn gives their active <see cref="Page"/> an opportunity restore its
        /// state.
        /// </summary>
        /// <param name="sessionBaseKey">An optional key that identifies the type of session.
        /// This can be used to distinguish between multiple application launch scenarios.</param>
        /// <returns>An asynchronous task that reflects when session state has been read.  The
        /// content of <see cref="AppSessionState"/> should not be relied upon until this task
        /// completes.</returns>
        public static async Task RestoreSessionAsync(String sessionBaseKey = null)
        {
            AppEventSource.Log.Debug("Suspension: Checking previously saved session state...");
            AppSessionState = await DeserializeFromFileAsync(typeof(SessionState), await GetSavedFileAsync(FILENAME_SESSION_STATE), true) as SessionState;

            if (AppSessionState != null && AppSessionState.Version == SessionState.CURRENT_SESSION_VERSION && AppSessionState.FileToken != null)
            {
                if (AppSessionState.ViewerMode == 1)
                {
                    try
                    {
                        StorageFile pdfFile = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(AppSessionState.FileToken);
                        AppEventSource.Log.Info("Suspension: Reopening " + pdfFile.Name);
                        SuspensionManager.pdfFile = pdfFile;
                        appFrame.Navigate(typeof(ViewerPage));
                    }
                    catch (Exception ex)
                    {
                        App.NotifyUser(typeof(SuspensionManager), "Failed to reopen file: " + pdfFile.Name + "\n" + ex.Message);
                    }
                }
                else
                {
                    AppSessionState = null;
                    AppEventSource.Log.Info("Suspension: Viewer was not active when the App was suspended.");
                }
            }
            else AppEventSource.Log.Debug("Suspension: Previously saved session state not restored.");
        }

        public static async Task SaveViewerAsync()
        {
            if (ViewerStateDictionary != null && ViewerStateDictionary.Count > 0)
            {
                try
                {
                    // Get the pdfToken and open the data folder
                    StorageFolder dataFolder = await
                        ApplicationData.Current.LocalFolder.CreateFolderAsync(AppSessionState.FileToken, CreationCollisionOption.OpenIfExists);
                    StorageFile file = await dataFolder.CreateFileAsync(FILENAME_VIEWER_STATE, CreationCollisionOption.ReplaceExisting);
                    AppEventSource.Log.Debug("Suspension: Saving viewer state to " + dataFolder.Name);
                    await SerializeToFileAsync(ViewerStateDictionary, typeof(Dictionary<Guid, ViewerState>), file);
                }
                catch (Exception ex)
                {
                    App.NotifyUser(typeof(SuspensionManager), "An Error occurred when saving view settings.\n" + ex.Message);
                }
            }
            else
            {
                // Delete view state file if no viewer state (views are closed)
                if (AppSessionState != null && AppSessionState.FileToken != null)
                {
                    try
                    {
                        StorageFolder dataFolder = await
                            ApplicationData.Current.LocalFolder.CreateFolderAsync(AppSessionState.FileToken, CreationCollisionOption.OpenIfExists);
                        StorageFile file = await dataFolder.CreateFileAsync(FILENAME_VIEWER_STATE, CreationCollisionOption.ReplaceExisting);
                        await file.DeleteAsync();
                    }
                    catch (Exception ex)
                    {
                        App.NotifyUser(typeof(SuspensionManager), "Error occurred when saving view settings.\n" + ex.Message);
                    }
                }
            }
        }

        public static async Task LoadViewerAsync()
        {
            if (!(bool)App.AppSettings[App.RESTORE_VIEW]) return;
            // Get the pdfToken and open the data folder
            StorageFolder dataFolder = await
                ApplicationData.Current.LocalFolder.CreateFolderAsync(AppSessionState.FileToken, CreationCollisionOption.OpenIfExists);
            // Check viewer state file
            AppEventSource.Log.Debug("ViewerPage: Checking previously saved viewer state...");
            StorageFile file = await GetSavedFileAsync(FILENAME_VIEWER_STATE, dataFolder);
            ViewerStateDictionary = await
                DeserializeFromFileAsync(typeof(Dictionary<Guid, ViewerState>), file) as Dictionary<Guid, ViewerState>;
            // Make sure there no null viewer state in the dictionary
            if (ViewerStateDictionary != null)
            {
                // Use a list to keep the null viewer state
                List<Guid> entryToRemove = new List<Guid>();
                // Go through the dictionary
                foreach (Guid key in ViewerStateDictionary.Keys)
                {
                    if (ViewerStateDictionary[key] == null)
                    {
                        // Add null entry to removal list
                        entryToRemove.Add(key);
                    }
                }
                // Remove the null entry
                foreach (Guid key in entryToRemove)
                {
                    ViewerStateDictionary.Remove(key);
                    AppEventSource.Log.Debug("NavigationPage: Null Viewer State removed, GUID = " + key.ToString());
                }
            }
            // Create a new viewer state dictionary if none is loaded
            if (ViewerStateDictionary == null || ViewerStateDictionary.Count == 0)
            {
                ViewerStateDictionary = new Dictionary<Guid, ViewerState>();
                ViewerStateDictionary.Add(Guid.NewGuid(), null);
            }
        }

        public static async Task SerializeToFileAsync(Object obj, Type objectType, StorageFile file)
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
                using (Stream fileStream = await file.OpenStreamForWriteAsync())
                {
                    viewerData.Seek(0, SeekOrigin.Begin);
                    await viewerData.CopyToAsync(fileStream);
                }
                AppEventSource.Log.Debug("Suspension: Object saved to file. " + file.Name);
            }
            catch (Exception e)
            {
                AppEventSource.Log.Error("Suspension: Error when serializing object. Exception: " + e.Message);
                MessageDialog messageDialog = new MessageDialog("Error when saving" + objectType.ToString() + " to file: " + file.Name + "\n" + e.Message);
                messageDialog.Commands.Add(new UICommand("OK", null, 0));
                await messageDialog.ShowAsync();
            }
        }

        public static async Task<Object> DeserializeFromFileAsync(Type objectType, StorageFile file, bool deleteFile = false)
        {
            if (file == null) return null;
            try
            {
                Object obj;
                AppEventSource.Log.Debug("Suspension: Checking file..." + file.Name);
                // Get the input stream for the file
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
                    deleteFile = false;
                    AppEventSource.Log.Info("Suspension: File deleted. " + file.Name);
                }
                return obj;
            }
            catch (Exception e)
            {
                AppEventSource.Log.Error("Suspension: Error when deserializing object. Exception: " + e.Message);
                MessageDialog messageDialog = new MessageDialog("Error when deserializing App settings: " + file.Name + "\n" 
                    + "The PDF file is not affected.\n " + "Details: \n" + e.Message);
                messageDialog.Commands.Add(new UICommand("Reset Settings", null, 0));
                messageDialog.Commands.Add(new UICommand("Ignore", null, 1));
                IUICommand command = await messageDialog.ShowAsync();
                switch ((int)command.Id)
                {
                    case 0:
                        // Delete file
                        deleteFile = true;
                        break;
                    default:
                        deleteFile = false;
                        break;
                }
                return null;
            }
            finally
            {
                // Delete the file if error occured
                if (deleteFile)
                {
                    await file.DeleteAsync();
                    AppEventSource.Log.Info("Suspension: File deleted due to error. " + file.Name);
                }
            }
        }

        public static async Task<StorageFile> GetSavedFileAsync(string filename, StorageFolder folder = null)
        {
            if (folder == null) folder = ApplicationData.Current.LocalFolder;
            StorageFile file = null;
            try
            {
                file = await folder.GetFileAsync(filename);
            }
            catch (Exception e)
            {
                if (e is FileNotFoundException)
                {
                    AppEventSource.Log.Debug("Suspension: Previously saved file not found. ");
                }
                else App.NotifyUser(typeof(SuspensionManager), "Failed the access file.\n" + e.Message);
            }
            return file;
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
