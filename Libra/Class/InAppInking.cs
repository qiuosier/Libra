using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Input.Inking;

namespace Libra.Class
{
    public class InAppInking : IInkingManager
    {
        private const string EXT_INKING = ".gif";
        private const string INKING_FOLDER = "Inking";

        private bool isSavingInking = false;

        private StorageFolder appFolder;
        private StorageFolder inkingFolder;

        /// <summary>
        /// A dictioary used to cache the inking
        /// </summary>
        private Dictionary<int, InkStrokeContainer> inkDictionary;

        private Queue<int> inkingChangedPagesQueue;

        private InAppInking(StorageFolder dataFolder)
        {
            this.appFolder = dataFolder;
            this.inkingChangedPagesQueue = new Queue<int>();
            this.inkDictionary = new Dictionary<int, InkStrokeContainer>();
        }

        public async Task<InkStrokeContainer> loadInking(int pageNumber)
        {
            // Load inking if exist
            InkStrokeContainer inkStrokeContainer;
            if (!inkDictionary.TryGetValue(pageNumber, out inkStrokeContainer))
            {
                StorageFile inkFile = await inkingFolder.TryGetItemAsync(pageNumber.ToString() + EXT_INKING) as StorageFile;
                if (inkFile != null)
                {
                    inkStrokeContainer = new InkStrokeContainer();
                    try
                    {
                        using (var inkStream = await inkFile.OpenSequentialReadAsync())
                        {
                            await inkStrokeContainer.LoadAsync(inkStream);
                        }
                        AppEventSource.Log.Debug("InAppInking: Inking for page " + pageNumber.ToString() + " loaded.");
                    }
                    catch (Exception e)
                    {
                        string errorMsg = "Error when loading inking for page " + pageNumber.ToString() + "\n Exception: " + e.Message;
                        AppEventSource.Log.Error("In App Inking: " + errorMsg);
                        int userResponse = await App.NotifyUserWithOptions(errorMsg, new string[] { "Remove Inking", "Ignore" });
                        switch (userResponse)
                        {
                            case 0: // Delete inking file
                                await inkFile.DeleteAsync();
                                AppEventSource.Log.Error("InAppInking: File deleted.");
                                break;
                            default: break;
                        }
                    }
                }
            }
            return inkStrokeContainer;
        }

        public async Task saveInking(int pageNumber, InkStrokeContainer inkStrokes)
        {
            // Remove item in dictionary, it will return false if item not found
            inkDictionary.Remove(pageNumber);
            // Add new ink strokes to dictionary, if any
            if (inkStrokes.GetStrokes().Count > 0)
            {
                inkDictionary.Add(pageNumber, inkStrokes);
                AppEventSource.Log.Debug("InAppInking: Ink strokes for page " + pageNumber.ToString() + " saved to dictionary.");
            }
            // Enqueue the page if it is not already in the queue
            if (!this.inkingChangedPagesQueue.Contains(pageNumber))
                this.inkingChangedPagesQueue.Enqueue(pageNumber);
            // Invoke save inking only if SaveInking is not running.
            // This will prevent running multiple saving instance at the same time.
            if (!this.isSavingInking)
                await SaveInkingQueue();
        }

        private async Task saveToFile(int pageNumber)
        {
            // Save inking from a page to a file
            try
            {
                StorageFile inkFile;
                InkStrokeContainer inkStrokeContainer;
                string filename = pageNumber.ToString() + EXT_INKING;
                if (inkDictionary.TryGetValue(pageNumber, out inkStrokeContainer))
                {
                    inkFile = await this.inkingFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
                    using (IRandomAccessStream inkStream = await inkFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        await inkStrokeContainer.SaveAsync(inkStream);
                    }
                }
                else
                {
                    inkFile = await inkingFolder.TryGetItemAsync(filename) as StorageFile;
                    if (inkFile != null) await inkFile.DeleteAsync();
                }
                AppEventSource.Log.Debug("InAppInking: Inking for page " + pageNumber + " saved.");
            }
            catch (Exception ex)
            {
                App.NotifyUser(typeof(ViewerPage), "An error occurred when saving inking. \n" + ex.Message, true);
            }
        }

        private async Task SaveInkingQueue()
        {
            this.isSavingInking = true;
            while (this.inkingChangedPagesQueue.Count > 0)
            {
                int pageNumber = this.inkingChangedPagesQueue.Dequeue();
                // Save ink strokes to dictionary
                //SaveInkCanvas(pageNumber);
                await saveToFile(pageNumber);
            }
            this.isSavingInking = false;
        }

        public static async Task<InAppInking> OpenInAppInking(StorageFolder dataFolder)
        {
            InAppInking inking = new InAppInking(dataFolder);
            inking.inkingFolder = await dataFolder.CreateFolderAsync(INKING_FOLDER, CreationCollisionOption.OpenIfExists);
            return inking;
        }
    }
}
