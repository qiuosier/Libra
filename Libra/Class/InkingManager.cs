using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Input.Inking;

namespace Libra.Class
{
    public class InkingManager : IInkingManager
    {
        private Dictionary<int, List<InkStroke>> inAppInkStrokes;
        private Dictionary<int, List<InkStroke>> removedInkStrokes;
        private Dictionary<int, List<InkStroke>> inFileInkStrokes;

        private InAppInking inAppInking;

        private StorageFolder appFolder;

        public StorageFolder InkingFolder
        {
            get
            {
                return inAppInking.InkingFolder;
            }
        }

        /// <summary>
        /// A dictioary used to cache the inking
        /// </summary>
        public Dictionary<int, InkStrokeContainer> InkDictionary
        {
            get
            {
                return inAppInking.InkDictionary;
            }
        }

        public InkStrokeContainer loadInking(int pageNumber)
        {
            // Load inking if exist
            InkStrokeContainer inkStrokeContainer;
            if(!InkDictionary.TryGetValue(pageNumber, out inkStrokeContainer))
                inkStrokeContainer = new InkStrokeContainer();
            return inkStrokeContainer;
        }

        public async Task saveInking(int pageNumber, InkStrokeContainer inkStrokeContainer = null)
        {
            InkStrokeContainer container = new InkStrokeContainer();
            List<InkStroke> inAppStrokes = new List<InkStroke>();
            inAppInkStrokes.TryGetValue(pageNumber, out inAppStrokes);
            foreach (InkStroke inkStroke in inAppStrokes)
            {
                container.AddStroke(inkStroke.Clone());
            }
            await inAppInking.saveInking(pageNumber, container);
            // TODO: Save removed ink strokes
        }

        public async Task addStrokes(int pageNumber, InkStrokeContainer inkStrokeContainer, IReadOnlyList<InkStroke> inkStrokes)
        {
            if (!inAppInkStrokes.ContainsKey(pageNumber))
            {
                inAppInkStrokes[pageNumber] = new List<InkStroke>(inkStrokes);
            }
            else inAppInkStrokes[pageNumber].AddRange(inkStrokes);
            await saveInking(pageNumber);
        }

        public async Task eraseStrokes(int pageNumber, InkStrokeContainer inkStrokeContainer, IReadOnlyList<InkStroke> inkStrokes)
        {
            foreach(InkStroke inkStroke in inkStrokes)
            {
                inAppInkStrokes[pageNumber].Remove(inkStroke);
            }
            await saveInking(pageNumber);
        }

        public static async Task<InkingManager> InitializeInking(StorageFolder dataFolder)
        {
            InkingManager inkManager = new InkingManager(dataFolder);
            inkManager.inAppInking = await InAppInking.InitializeInking(dataFolder);
            foreach (KeyValuePair<int, InkStrokeContainer> entry in inkManager.InkDictionary)
            {
                inkManager.inAppInkStrokes[entry.Key] = new List<InkStroke>(entry.Value.GetStrokes());
            }
            // TODO: Initialize in-file strokes
            return inkManager;
        }

        private InkingManager(StorageFolder dataFolder)
        {
            appFolder = dataFolder;
            inAppInkStrokes = new Dictionary<int, List<InkStroke>>();
            removedInkStrokes = new Dictionary<int, List<InkStroke>>();
            inFileInkStrokes = new Dictionary<int, List<InkStroke>>();
            return;
        }

        public async Task RemoveInAppInking()
        {
            foreach (StorageFile inkFile in await InkingFolder.GetFilesAsync())
            {
                await inkFile.DeleteAsync();
            }
            foreach (List<InkStroke> list in inAppInkStrokes.Values)
            {
                list.Clear();
            }
        }
    }
}
