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
    public class InAppInking
    {
        private const string EXT_INKING = ".gif";
        private const string INKING_FOLDER = "Inking";

        private bool isSavingInking = false;
        private Queue<int> inkingChangedPagesQueue;

        private StorageFolder appFolder;
        public StorageFolder InkingFolder { get; private set; }

        private Dictionary<int, List<InkStroke>> inkStrokesDict;

        private InAppInking(StorageFolder dataFolder)
        {
            this.appFolder = dataFolder;
            this.inkingChangedPagesQueue = new Queue<int>();
        }

        public static async Task<InAppInking> InitializeInking(StorageFolder dataFolder)
        {
            InAppInking inking = new InAppInking(dataFolder);
            inking.InkingFolder = await GetInkingFolder(dataFolder);
            return inking;
        }

        private static async Task<StorageFolder> GetInkingFolder(StorageFolder dataFolder)
        {
            StorageFolder inkingFolder = await dataFolder.CreateFolderAsync(INKING_FOLDER, CreationCollisionOption.OpenIfExists);
            return inkingFolder;
        }

        /// <summary>
        /// Loads inking from file in the app data folder.
        /// </summary>
        /// <param name="pageNumber"></param>
        /// <returns></returns>
        public async Task<InkStrokeContainer> LoadFromFile(int pageNumber)
        {
            InkStrokeContainer inkStrokeContainer = new InkStrokeContainer();
            StorageFile inkFile = await InkingFolder.TryGetItemAsync(pageNumber.ToString() + EXT_INKING) as StorageFile;
            if (inkFile != null)
            {
                try
                {
                    using (var inkStream = await inkFile.OpenSequentialReadAsync())
                    {
                        await inkStrokeContainer.LoadAsync(inkStream);
                    }
                    inkStrokesDict[pageNumber] = new List<InkStroke>();
                    inkStrokesDict[pageNumber].AddRange(inkStrokeContainer.GetStrokes());
                    AppEventSource.Log.Debug("InAppInking: Inking for page " + pageNumber.ToString() + " loaded.");
                }
                catch (Exception e)
                {
                    // TODO: File in use?
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
            return inkStrokeContainer;
        }

        public void AddStrokes(int pageNumber, InkStrokeContainer inkStrokeContainer, IReadOnlyList<InkStroke> inkStrokes)
        {
            if (!inkStrokesDict.ContainsKey(pageNumber))
            {
                inkStrokesDict[pageNumber] = new List<InkStroke>(inkStrokes);
            }
            else inkStrokesDict[pageNumber].AddRange(inkStrokes);
            SaveInking(pageNumber).ContinueWith(
                t => AppEventSource.Log.Debug("SaveInking Error: " + t.Exception.Message), 
                TaskContinuationOptions.OnlyOnFaulted);
        }

        public void EraseStrokes(int pageNumber, InkStrokeContainer inkStrokeContainer, IReadOnlyList<InkStroke> inkStrokes)
        {
            if (inkStrokesDict.TryGetValue(pageNumber, out List<InkStroke> inAppStrokes))
            {
                foreach (InkStroke inkStroke in inkStrokes)
                {
                    inAppStrokes.Remove(inkStroke);
                }
                SaveInking(pageNumber).ContinueWith(
                t => AppEventSource.Log.Debug("SaveInking Error: " + t.Exception.Message),
                TaskContinuationOptions.OnlyOnFaulted);
            }
        }

        public async Task SaveInking(int pageNumber)
        {
            // Enqueue the page if it is not already in the queue
            if (!this.inkingChangedPagesQueue.Contains(pageNumber))
                this.inkingChangedPagesQueue.Enqueue(pageNumber);
            // Invoke save inking only if SaveInking is not running.
            // This will prevent running multiple saving instance at the same time.
            if (!this.isSavingInking)
                await SaveInkingQueue();
        }

        private async Task SaveInkingQueue()
        {
            this.isSavingInking = true;
            while (this.inkingChangedPagesQueue.Count > 0)
            {
                int pageNumber = this.inkingChangedPagesQueue.Dequeue();
                await SaveToFile(pageNumber);
            }
            this.isSavingInking = false;
        }

        private async Task SaveToFile(int pageNumber)
        {
            // Save inking from a page to a file
            // Do nothing if ink strokes are not loaded into the inkStrokesDict
            if (inkStrokesDict.TryGetValue(pageNumber, out List<InkStroke> inkStrokes))
            {
                StorageFile inkFile;
                string filename = pageNumber.ToString() + EXT_INKING;
                try
                {
                    if (inkStrokes.Count > 0)
                    {
                        InkStrokeContainer container = new InkStrokeContainer();
                        foreach (InkStroke stroke in inkStrokes)
                        {
                            container.AddStroke(stroke.Clone());
                        }
                        inkFile = await this.InkingFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
                        using (IRandomAccessStream inkStream = await inkFile.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            await container.SaveAsync(inkStream);
                        }
                        AppEventSource.Log.Debug("InAppInking: Inking for page " + pageNumber + " saved.");
                    }
                    else
                    {
                        // Delete the file if there is no ink strokes.
                        inkFile = await InkingFolder.TryGetItemAsync(filename) as StorageFile;
                        if (inkFile != null) await inkFile.DeleteAsync();
                        AppEventSource.Log.Debug("InAppInking: Inking for page " + pageNumber + " removed.");
                    }
                }
                catch (Exception ex)
                {
                    App.NotifyUser(typeof(ViewerPage), "An error occurred when saving inking. \n" + ex.Message, true);
                }
            }
        }
    }
}
