using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml.Controls;

namespace Libra.Class
{
    public class InkingCollection : Dictionary<int, InkStrokeContainer>
    {
        private const string EXT_INKING = ".gif";
        private const string INKING_FOLDER = "Inking";

        private StorageFolder inkingFolder;
        public Dictionary<int, InkCanvas> ActiveInkCanvas
        {
            get; private set;
        }

        private InkingCollection()
        {
            ActiveInkCanvas = new Dictionary<int, InkCanvas>();
        }

        public static async Task<InkingCollection> LoadInkingCollection(StorageFolder dataFolder)
        {
            InkingCollection inkingCollection = new InkingCollection();
            inkingCollection.inkingFolder = await dataFolder.CreateFolderAsync(INKING_FOLDER, CreationCollisionOption.OpenIfExists);
            await inkingCollection.LoadInking();
            return inkingCollection;
        }

        /// <summary>
        /// Load inking from files to inking dictionary.
        /// </summary>
        /// <returns></returns>
        private async Task LoadInking()
        {
            System.Diagnostics.Stopwatch inkingLoadingWatch = new System.Diagnostics.Stopwatch();
            inkingLoadingWatch.Start();
            AppEventSource.Log.Debug("ViewerPage: Checking inking ...");
            // TODO: Need to check if the inking is suitable for the file/page.
            //
            //
            foreach (StorageFile inkFile in await inkingFolder.GetFilesAsync())
            {
                int pageNumber = 0;
                try
                {
                    pageNumber = Convert.ToInt32(inkFile.Name.Substring(0, inkFile.Name.Length - 4));
                    InkStrokeContainer inkStrokeContainer = new InkStrokeContainer();
                    using (var inkStream = await inkFile.OpenSequentialReadAsync())
                    {
                        await inkStrokeContainer.LoadAsync(inkStream);
                    }
                    this.Add(pageNumber, inkStrokeContainer);
                    AppEventSource.Log.Debug("ViewerPage: Inking for page " + pageNumber.ToString() + " loaded.");
                }
                catch (Exception e)
                {
                    string errorMsg = "Error when loading inking for page " + pageNumber.ToString() + "\n Exception: " + e.Message;
                    AppEventSource.Log.Error("ViewerPage: " + errorMsg);
                    int userResponse = await App.NotifyUserWithOptions(errorMsg, new string[] { "Remove Inking", "Ignore" });
                    switch (userResponse)
                    {
                        case 0: // Delete inking file
                            await inkFile.DeleteAsync();
                            AppEventSource.Log.Error("ViewerPage: File deleted.");
                            break;
                        default: break;
                    }
                    return;
                }
            }
            inkingLoadingWatch.Stop();
            AppEventSource.Log.Info("ViewerPage: Inking loaded in " + inkingLoadingWatch.Elapsed.TotalSeconds.ToString() + " seconds.");
        }

        public async Task SaveInking(int pageNumber)
        {
            // Save inking from a page to a file
            try
            {
                StorageFile inkFile = await this.inkingFolder.CreateFileAsync(
                    pageNumber.ToString() + EXT_INKING, CreationCollisionOption.ReplaceExisting);
                using (IRandomAccessStream inkStream = await inkFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    InkStrokeContainer inkStrokeContainer;
                    if (this.TryGetValue(pageNumber, out inkStrokeContainer))
                    {
                        await inkStrokeContainer.SaveAsync(inkStream);
                    }
                }
                AppEventSource.Log.Debug("ViewerPage: Inking for page " + pageNumber + " saved.");
            }
            catch (Exception ex)
            {
                App.NotifyUser(typeof(ViewerPage), "An error occurred when saving inking. \n" + ex.Message, true);
            }
        }
    }
}
