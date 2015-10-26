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
        public static SessionState sessionState
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
        public static async Task SaveAsync()
        {
            appFrame.Navigate(typeof(BlankPage), null, new Windows.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo());

            if (sessionState != null)
            {
                AppEventSource.Log.Debug("Suspension: Saving session state to file.");
                await SerializeToFile(sessionState, typeof(SessionState), sessionStateFilename);
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
        public static async Task RestoreAsync(String sessionBaseKey = null)
        {
            //try
            //{
                AppEventSource.Log.Debug("Suspension: Checking previously saved session state.");
                sessionState = await DeserializeFromFile(typeof(SessionState), sessionStateFilename, true) as SessionState;

                if (sessionState != null && sessionState.FileToken != null)
                {
                    if (sessionState.ViewerMode == 1)
                    {
                        StorageFile pdfFile = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(sessionState.FileToken);
                        AppEventSource.Log.Info("Suspension: Restoring " + pdfFile.Name);
                        appFrame.Navigate(typeof(ViewerPage), pdfFile);
                    }
                    else
                    {
                        sessionState = null;
                        AppEventSource.Log.Info("Suspension: Viewer was not active when the App was suspended.");
                    }
                }
                else AppEventSource.Log.Warn("Suspension: Previously saved viewer state does not contain file token.");

            //}
            //catch (Exception e)
            //{
            //}
        }

        public static async Task SerializeToFile(Object obj, Type objectType, string filename)
        {
            try
            {
                AppEventSource.Log.Debug("Suspension: Serializing object...");
                // Serialize the session state synchronously to avoid asynchronous access to shared state
                MemoryStream viewerData = new MemoryStream();
                DataContractSerializer serializer = new DataContractSerializer(objectType);
                serializer.WriteObject(viewerData, obj);
                AppEventSource.Log.Debug("Suspension: Object serialized to memory.");
                // Get an output stream for the SessionState file and write the state asynchronously
                StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
                using (Stream fileStream = await file.OpenStreamForWriteAsync())
                {
                    viewerData.Seek(0, SeekOrigin.Begin);
                    await viewerData.CopyToAsync(fileStream);
                }
                AppEventSource.Log.Debug("Suspension: Object saved to file.");
            }
            catch (Exception e)
            {
                AppEventSource.Log.Error("Suspension: Error when serializing object. Exception: " + e.Message);
                throw new SuspensionManagerException(e);
            }
        }

        public static async Task<Object> DeserializeFromFile(Type objectType, string filename, bool deleteFile = false)
        {
            try
            {
                AppEventSource.Log.Debug("Suspension: Checking file...");
                // Get the input stream for the file
                StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync(filename);
                Object obj;
                using (IInputStream inStream = await file.OpenSequentialReadAsync())
                {
                    // Deserialize the Session State
                    DataContractSerializer serializer = new DataContractSerializer(objectType);
                    obj = serializer.ReadObject(inStream.AsStreamForRead());
                }
                AppEventSource.Log.Debug("Suspension: Object loaded from file.");


                // Delete the file
                if (deleteFile)
                {
                    await file.DeleteAsync();
                    AppEventSource.Log.Info("Suspension: File deleted.");
                }

                return obj;
            }
            catch (Exception e)
            {
                if (e is FileNotFoundException)
                {
                    AppEventSource.Log.Debug("Suspension: File not found.");
                    return null;
                }
                AppEventSource.Log.Error("Suspension: Error when deserializing object. Exception: " + e.Message);
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
