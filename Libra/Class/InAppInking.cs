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
        private const string ERASED_FOLDER = "Erased";

        private bool isSavingInking = false;
        private Queue<int> inkingChangedPagesQueue;

        private StorageFolder appFolder;
        private StorageFolder InkingFolder;
        private StorageFolder ErasedFolder;

        private Dictionary<int, List<InkStroke>> inkStrokesDict;
        private Dictionary<int, List<InkStroke>> erasedStrokesDict;

        private InAppInking(StorageFolder dataFolder)
        {
            appFolder = dataFolder;
            inkingChangedPagesQueue = new Queue<int>();
            inkStrokesDict = new Dictionary<int, List<InkStroke>>();
            erasedStrokesDict = new Dictionary<int, List<InkStroke>>();
        }

        public static async Task<InAppInking> InitializeInking(StorageFolder dataFolder)
        {
            InAppInking inking = new InAppInking(dataFolder)
            {
                InkingFolder = await dataFolder.CreateFolderAsync(INKING_FOLDER, CreationCollisionOption.OpenIfExists),
                ErasedFolder = await dataFolder.CreateFolderAsync(ERASED_FOLDER, CreationCollisionOption.OpenIfExists)
            };
            return inking;
        }


        /// <summary>
        /// Loads inking from file in the app data folder.
        /// </summary>
        /// <param name="pageNumber"></param>
        /// <returns></returns>
        public async Task<InkStrokeContainer> LoadInking(int pageNumber)
        {
            InkStrokeContainer inkStrokeContainer = new InkStrokeContainer();
            if (await InkingFolder.TryGetItemAsync(InkFileName(pageNumber)) is StorageFile inkFile)
            {
                inkStrokeContainer = await LoadInkFile(inkFile, pageNumber);
                inkStrokesDict[pageNumber] = new List<InkStroke>(inkStrokeContainer.GetStrokes());
            }
            return inkStrokeContainer;
        }

        public async Task<List<InkStroke>> LoadErasedStrokes(int pageNumber)
        {
            if (!erasedStrokesDict.TryGetValue(pageNumber, out List<InkStroke> erasedStrokes))
            {
                erasedStrokes = new List<InkStroke>();
                InkStrokeContainer inkStrokeContainer = new InkStrokeContainer();
                if (await ErasedFolder.TryGetItemAsync(ErasedInkFileName(pageNumber)) is StorageFile inkFile)
                {
                    inkStrokeContainer = await LoadInkFile(inkFile, pageNumber);
                    erasedStrokes.AddRange(inkStrokeContainer.GetStrokes());
                    erasedStrokesDict[pageNumber] = erasedStrokes;
                }
            }
            return erasedStrokes;
        }

        private async Task<InkStrokeContainer> LoadInkFile(StorageFile inkFile, int pageNumber)
        {
            InkStrokeContainer inkStrokeContainer = new InkStrokeContainer();
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
            return inkStrokeContainer;
        }

        public async Task<Dictionary<int, InkStrokeContainer>> LoadInkDictionary()
        {
            Dictionary<int, InkStrokeContainer> inkDictionary = new Dictionary<int, InkStrokeContainer>();
            Dictionary<int, Task<InkStrokeContainer>> taskDictionary = new Dictionary<int, Task<InkStrokeContainer>>();
            foreach (StorageFile inkFile in await InkingFolder.GetFilesAsync())
            {
                int pageNumber = 0;
                try
                {
                    pageNumber = Convert.ToInt32(inkFile.Name.Substring(0, inkFile.Name.Length - 4));
                }
                catch (Exception e)
                {
                    AppEventSource.Log.Error("In App Inking: Invalid inking file name " + inkFile.Name + ". " + e.Message);
                    continue;
                }
                taskDictionary[pageNumber] = LoadInking(pageNumber);
            }
            foreach (KeyValuePair<int, Task<InkStrokeContainer>> entry in taskDictionary)
            {
                inkDictionary[entry.Key] = await entry.Value;
            }
            return inkDictionary;
        }

        public async Task<Dictionary<int, List<InkStroke>>> LoadErasedStrokesDictionary()
        {
            Dictionary<int, List<InkStroke>> inkDictionary = new Dictionary<int, List<InkStroke>>();
            Dictionary<int, Task<List<InkStroke>>> taskDictionary = new Dictionary<int, Task<List<InkStroke>>>();
            foreach (StorageFile inkFile in await ErasedFolder.GetFilesAsync())
            {
                int pageNumber = 0;
                try
                {
                    pageNumber = Convert.ToInt32(inkFile.Name.Substring(0, inkFile.Name.Length - 4));
                }
                catch (Exception e)
                {
                    AppEventSource.Log.Error("In App Inking: Invalid inking file name " + inkFile.Name + ". " + e.Message);
                    continue;
                }
                taskDictionary[pageNumber] = LoadErasedStrokes(pageNumber);
            }
            foreach (KeyValuePair<int, Task<List<InkStroke>>> entry in taskDictionary)
            {
                inkDictionary[entry.Key] = await entry.Value;
            }
            return inkDictionary;
        }

        public async Task<List<int>> GetPageNumbersWithInking(StorageFolder folder)
        {
            List<int> pageNumberList = new List<int>();
            foreach (StorageFile inkFile in await folder.GetFilesAsync())
            {
                int pageNumber = 0;
                try
                {
                    pageNumber = Convert.ToInt32(inkFile.Name.Substring(0, inkFile.Name.Length - 4));
                }
                catch (Exception e)
                {
                    AppEventSource.Log.Error("In App Inking: Invalid erased inking file name " + inkFile.Name + ". " + e.Message);
                    continue;
                }
                if (pageNumber > 0) pageNumberList.Add(pageNumber);
            }
            return pageNumberList;
        }

        public void AddStrokes(int pageNumber, InkStrokeContainer inkStrokeContainer, IReadOnlyList<InkStroke> inkStrokes)
        {
            if (!inkStrokesDict.ContainsKey(pageNumber))
            {
                inkStrokesDict[pageNumber] = new List<InkStroke>(inkStrokes);
            }
            else
            {
                List<InkStroke> strokesInPage = inkStrokesDict[pageNumber];
                foreach (InkStroke stroke in inkStrokes)
                {
                    if (!strokesInPage.Contains(stroke))
                    {
                        strokesInPage.Add(stroke);
                        inkStrokesDict[pageNumber] = strokesInPage;
                    }
                }
            }
            SaveInking(pageNumber);
        }

        public bool EraseStrokes(int pageNumber, InkStrokeContainer inkStrokeContainer, IReadOnlyList<InkStroke> inkStrokes)
        {
            bool strokeRemoved = false;
            bool inkingChanged = false;
            List<InkStroke> strokesToBeRemoved = new List<InkStroke>();
            if (inkStrokesDict.TryGetValue(pageNumber, out List<InkStroke> inAppStrokes))
            {
                foreach (InkStroke inkStroke in inkStrokes)
                {
                    strokeRemoved = inAppStrokes.Remove(inkStroke);
                    if (!strokeRemoved) strokesToBeRemoved.Add(inkStroke);
                    else inkingChanged = true;
                }
                if (inkingChanged) SaveInking(pageNumber);
            }
            else
            {
                strokesToBeRemoved.AddRange(inkStrokes);
            }
            if (strokesToBeRemoved.Count > 0)
            {
                if (!erasedStrokesDict.TryGetValue(pageNumber, out List<InkStroke> erasedStrokes))
                {
                    erasedStrokes = new List<InkStroke>();
                }
                erasedStrokes.AddRange(strokesToBeRemoved);
                erasedStrokesDict[pageNumber] = erasedStrokes;
                SaveInking(-pageNumber);
            }
            return strokeRemoved;
        }

        private void SaveInking(int pageNumber)
        {
            SaveInkingAsync(pageNumber).ContinueWith(
                t => AppEventSource.Log.Debug("SaveInking Error: " + t.Exception.Message),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        private async Task SaveInkingAsync(int pageNumber)
        {
            // Enqueue the page if it is not already in the queue
            if (!this.inkingChangedPagesQueue.Contains(pageNumber))
                this.inkingChangedPagesQueue.Enqueue(pageNumber);
            // Invoke save inking only if SaveInking is not running.
            // This will prevent running multiple saving instance at the same time.
            if (!this.isSavingInking)
                await SaveInkingInQueue();
        }

        private async Task SaveInkingInQueue()
        {
            this.isSavingInking = true;
            while (this.inkingChangedPagesQueue.Count > 0)
            {
                int pageNumber = this.inkingChangedPagesQueue.Dequeue();
                await SaveInkStrokes(pageNumber);
            }
            this.isSavingInking = false;
        }

        private async Task SaveInkStrokes(int pageNumber)
        {
            Dictionary<int, List<InkStroke>> strokesDict;
            StorageFolder folder;
            string filename;
            if (pageNumber > 0)
            {
                strokesDict = inkStrokesDict;
                folder = InkingFolder;
                filename = InkFileName(pageNumber);
            }
            else
            {
                pageNumber = -pageNumber;
                strokesDict = erasedStrokesDict;
                folder = ErasedFolder;
                filename = ErasedInkFileName(pageNumber);
            }
            // Save inking from a page to a file
            // Do nothing if ink strokes are not loaded into the inkStrokesDict
            if (strokesDict.TryGetValue(pageNumber, out List<InkStroke> inkStrokes))
            {
                try
                {
                    await SaveStrokesToFile(inkStrokes, folder, filename);
                }
                catch (Exception ex)
                {
                    string message = "An error occurred when saving inking for page " + pageNumber.ToString();
                    message += ".\n" + ex.Message;
                    App.NotifyUser(typeof(ViewerPage), message, true);
                }
            }
        }

        private async Task SaveStrokesToFile(List<InkStroke> inkStrokes, StorageFolder folder, string filename)
        {
            StorageFile inkFile;
            if (inkStrokes.Count > 0)
            {
                InkStrokeContainer container = new InkStrokeContainer();
                foreach (InkStroke stroke in inkStrokes)
                {
                    container.AddStroke(stroke.Clone());
                }
                inkFile = await folder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
                using (IRandomAccessStream inkStream = await inkFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await container.SaveAsync(inkStream);
                }
                AppEventSource.Log.Debug("InAppInking: Inking saved to " + filename);
            }
            else
            {
                // Delete the file if there is no ink strokes.
                inkFile = await folder.TryGetItemAsync(filename) as StorageFile;
                if (inkFile != null)
                {
                    await inkFile.DeleteAsync();
                    AppEventSource.Log.Debug("InAppInking: Inking file removed. " + filename);
                }
            }
        }

        public async Task RemoveInking()
        {
            foreach (StorageFile inkFile in await InkingFolder.GetFilesAsync())
            {
                await inkFile.DeleteAsync();
            }
            foreach (StorageFile inkFile in await ErasedFolder.GetFilesAsync())
            {
                await inkFile.DeleteAsync();
            }
        }

        private string InkFileName(int pageNumber)
        {
            return pageNumber.ToString() + EXT_INKING;
        }

        private string ErasedInkFileName(int pageNumber)
        {
            return pageNumber.ToString() + EXT_INKING;
        }
    }
}
