using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Input.Inking;

namespace Libra.Class
{
    public class InkingManager
    {
        private Dictionary<int, List<InkStroke>> removedInkStrokes;
        private Dictionary<int, List<InkStroke>> inFileInkStrokes;

        private InAppInking inAppInking;
        private StorageFolder appFolder;
        private PdfModel pdfModel;

        /// <summary>
        /// A dictioary used to cache the inking
        /// </summary>
        private Dictionary<int, InkStrokeContainer> inkDictionary;

        private InkingManager(StorageFolder dataFolder)
        {
            appFolder = dataFolder;
            removedInkStrokes = new Dictionary<int, List<InkStroke>>();
            inFileInkStrokes = new Dictionary<int, List<InkStroke>>();
            inkDictionary = new Dictionary<int, InkStrokeContainer>();
            return;
        }

        public static async Task<InkingManager> InitializeInking(StorageFolder dataFolder, PdfModel pdfModel)
        {
            InkingManager inkManager = new InkingManager(dataFolder);
            inkManager.inAppInking = await InAppInking.InitializeInking(dataFolder);
            inkManager.pdfModel = pdfModel;
            return inkManager;
        }

        public async Task<Dictionary<int, InkStrokeContainer>> InAppInkDictionary()
        {
            return await inAppInking.LoadInkDictionary();
        }

        public async Task<InkStrokeContainer> LoadInking(int pageNumber)
        {
            // Load inking from Ink Dictionary if exist
            InkStrokeContainer inkStrokeContainer;
            if (!inkDictionary.TryGetValue(pageNumber, out inkStrokeContainer))
            {
                // Try to load inking from app data folder if inking not found in Ink Dictionary.
                // A new ink stroke container will be returned if no inking found.
                inkStrokeContainer = await inAppInking.LoadFromFile(pageNumber);
                // Add In-File inking to the ink container
                inkStrokeContainer.AddStrokes(pdfModel.LoadInFileInkAnnotations(pageNumber));
                inkDictionary[pageNumber] = inkStrokeContainer;
            }
            return inkStrokeContainer;
        }

        public void AddStrokes(int pageNumber, InkStrokeContainer inkStrokeContainer, IReadOnlyList<InkStroke> inkStrokes)
        {
            inAppInking.AddStrokes(pageNumber, inkStrokeContainer, inkStrokes);
        }

        public void EraseStrokes(int pageNumber, InkStrokeContainer inkStrokeContainer, IReadOnlyList<InkStroke> inkStrokes)
        {
            inAppInking.EraseStrokes(pageNumber, inkStrokeContainer, inkStrokes);
        }

        public async Task RemoveInAppInking()
        {
            foreach (StorageFile inkFile in await inAppInking.InkingFolder.GetFilesAsync())
            {
                await inkFile.DeleteAsync();
            }
            inAppInking = await InAppInking.InitializeInking(appFolder);
        }
    }
}
