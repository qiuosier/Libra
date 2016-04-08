using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Input.Inking;

namespace Libra.Class
{
    public class InkManager : IInkingManager
    {
        Dictionary<int, List<InkStroke>> inAppInkStrokes;
        Dictionary<int, List<InkStroke>> removedInkStrokes;
        Dictionary<int, List<InkStroke>> inFileInkStrokes;
        InAppInking inAppInking;

        StorageFolder appFolder;

        /// <summary>
        /// A dictioary used to cache the inking
        /// </summary>
        private Dictionary<int, InkStrokeContainer> inkDictionary;

        public async Task<InkStrokeContainer> loadInking(int pageNumber)
        {
            // Load in app inking
            InkStrokeContainer inkStrokeContainer;
            if (!inkDictionary.TryGetValue(pageNumber, out inkStrokeContainer))
            {
                inkStrokeContainer = await InAppInking.LoadInkingFromFile(pageNumber, appFolder);
                inkDictionary.Add(pageNumber, inkStrokeContainer);
            }
            inAppInkStrokes[pageNumber] = new List<InkStroke>();
            inAppInkStrokes[pageNumber].AddRange(inkStrokeContainer.GetStrokes());
            // TODO: Add in file inking
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

        public async Task addStrokes(int pageNumber, IReadOnlyList<InkStroke> inkStrokes)
        {
            inAppInkStrokes[pageNumber].AddRange(inkStrokes);
            await saveInking(pageNumber);
        }

        public async Task eraseStrokes(int pageNumber, IReadOnlyList<InkStroke> inkStrokes)
        {
            foreach(InkStroke inkStroke in inkStrokes)
            {
                inAppInkStrokes[pageNumber].Remove(inkStroke);
            }
            await saveInking(pageNumber);
        }

        public static async Task<InkManager> InitializeInkManager(StorageFolder dataFolder)
        {
            InkManager inkManager = new InkManager(dataFolder);
            inkManager.inAppInking = await InAppInking.OpenInAppInking(dataFolder);
            return inkManager;
        }

        private InkManager(StorageFolder dataFolder)
        {
            appFolder = dataFolder;
            inkDictionary = new Dictionary<int, InkStrokeContainer>();
            inAppInkStrokes = new Dictionary<int, List<InkStroke>>();
            removedInkStrokes = new Dictionary<int, List<InkStroke>>();
            inFileInkStrokes = new Dictionary<int, List<InkStroke>>();
            return;
        }
    }
}
