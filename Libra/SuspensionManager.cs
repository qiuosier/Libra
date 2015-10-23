using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Streams;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
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
        //private static ViewerState _viewerState = new ViewerState();
        private static List<Type> _knownTypes = new List<Type>();
        private const string sessionStateFilename = "_sessionState.xml";
        private static Frame appFrame;
        public static Dictionary<int, InkStrokeContainer> inkStrokeDictionary;
        private const string inkStrokeFilename = "_inkStroke.zip";
        public static bool Restoring = false;
        /// <summary>
        /// Provides access to global session state for the current session.  This state is
        /// serialized by <see cref="SaveAsync"/> and restored by
        /// <see cref="RestoreAsync"/>, so values must be serializable by
        /// <see cref="DataContractSerializer"/> and should be as compact as possible.  Strings
        /// and other self-contained data types are strongly recommended.
        /// </summary>
        public static ViewerState PageViewerState
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
                appFrame.Navigate(typeof(MainPage), null, new Windows.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo());

                if (PageViewerState != null)
                {
                    // Serialize the session state synchronously to avoid asynchronous access to shared
                    // state
                    MemoryStream viewerData = new MemoryStream();
                    DataContractSerializer serializer = new DataContractSerializer(typeof(ViewerState), _knownTypes);
                    serializer.WriteObject(viewerData, PageViewerState);

                    // Get an output stream for the SessionState file and write the state asynchronously
                    StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(sessionStateFilename, CreationCollisionOption.ReplaceExisting);
                    using (Stream fileStream = await file.OpenStreamForWriteAsync())
                    {
                        viewerData.Seek(0, SeekOrigin.Begin);
                        await viewerData.CopyToAsync(fileStream);
                    }

                    using (MemoryStream inkData = new MemoryStream())
                    {
                        using (ZipArchive archive = new ZipArchive(inkData, ZipArchiveMode.Create, true))
                        {
                            foreach (KeyValuePair<int, InkStrokeContainer> entry in inkStrokeDictionary)
                            {
                                ZipArchiveEntry inkFile = archive.CreateEntry(entry.Key.ToString() + ".gif");
                                using (var entryStream = inkFile.Open().AsOutputStream())
                                    await entry.Value.SaveAsync(entryStream);
                                
                            }
                        }
                        StorageFile inkArchiveFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(inkStrokeFilename, CreationCollisionOption.ReplaceExisting);
                        using (Stream inkArchiveStream = await inkArchiveFile.OpenStreamForWriteAsync())
                        {
                            inkData.Seek(0, SeekOrigin.Begin);
                            await inkData.CopyToAsync(inkArchiveStream);
                        }
                    }
                }
            }
            catch (Exception e)
            {
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
            //PageViewerState = new ViewerState();

            try
            {
                // Get the input stream for the SessionState file
                StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync(sessionStateFilename);
                using (IInputStream inStream = await file.OpenSequentialReadAsync())
                {
                    // Deserialize the Session State
                    DataContractSerializer serializer = new DataContractSerializer(typeof(ViewerState), _knownTypes);
                    PageViewerState = (ViewerState)serializer.ReadObject(inStream.AsStreamForRead());
                }

                // 
                if (PageViewerState.pdfToken != null)
                {
                    // Restore ink strokes
                    inkStrokeDictionary = new Dictionary<int, InkStrokeContainer>();
                    StorageFile inkArchiveFile = await ApplicationData.Current.LocalFolder.GetFileAsync(inkStrokeFilename);
                    using (IInputStream inkArchiveStream = await inkArchiveFile.OpenSequentialReadAsync())
                    {
                        using (ZipArchive archive = new ZipArchive(inkArchiveStream.AsStreamForRead(), ZipArchiveMode.Read))
                        {
                            foreach (ZipArchiveEntry inkFile in archive.Entries)
                            {
                                using (var entryStream = inkFile.Open().AsInputStream())
                                {
                                    InkStrokeContainer inkStrokeContainer = new InkStrokeContainer();
                                    await inkStrokeContainer.LoadAsync(entryStream);
                                    inkStrokeDictionary.Add(Convert.ToInt32(inkFile.Name.Substring(0, inkFile.Name.Length - 4)), inkStrokeContainer);
                                }
                            }
                        }
                    }

                    Restoring = true;
                    StorageFile pdfFile = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(PageViewerState.pdfToken);
                    appFrame.Navigate(typeof(ViewerPage), pdfFile);
                }

                // Clean up the files

            }
            catch (Exception e)
            {
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