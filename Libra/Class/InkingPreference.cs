using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;

namespace Libra.Class
{
    /// <summary>
    /// Contains user preferences for inking.
    /// </summary>
    public class InkingPreference
    {
        public const int CURRENT_INKING_PREF_VERSION = 2;
        private const string INKING_PREFERENCE_FILENAME = "_inkingPreference.xml";

        public InkingPreference()
        {
            penSize = 1;
            highlighterSize = 10;
            penColor = Colors.Red;
            highlighterColor = Colors.Yellow;
            drawingDevice = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Pen;
            this.version = CURRENT_INKING_PREF_VERSION;
        }

        public int penSize;
        public int highlighterSize;
        public Color penColor;
        public Color highlighterColor;
        public CoreInputDeviceTypes drawingDevice;
        public int version;

        public Size GetPenSize(double scale)
        {
            if (penSize == 0) return new Size(0.4 / scale, 0.4 / scale);
            else return new Size(penSize / scale, penSize / scale);
        }

        public Size GetHighlighterSize(double scale)
        {
            return new Size(highlighterSize / scale, highlighterSize / scale);
        }

        /// <summary>
        /// Load inking preference from file. A new one will be created none is loaded.
        /// This method will also create drawing attributes to be used by ink canvas.
        /// </summary>
        /// <returns></returns>
        public static async Task<InkingPreference> LoadDrawingPreference()
        {
            // Check drawing preference file
            AppEventSource.Log.Debug("ViewerPage: Checking previously saved drawing preference...");
            StorageFile file = await SuspensionManager.GetSavedFileAsync(INKING_PREFERENCE_FILENAME);
            InkingPreference inkingPreference = await
                SuspensionManager.DeserializeFromFileAsync(typeof(InkingPreference), file) as InkingPreference;
            // Discard the inking preference if it is not the current version
            if (inkingPreference != null && inkingPreference.version != InkingPreference.CURRENT_INKING_PREF_VERSION)
                inkingPreference = null;
            // Create drawing preference file if one was not loaded.
            if (inkingPreference == null)
            {
                AppEventSource.Log.Debug("ViewerPage: No saved drawing preference loaded. Creating a new one...");
                inkingPreference = new InkingPreference();
                await inkingPreference.SaveAsync();
            }
            return inkingPreference;
        }

        /// <summary>
        /// Save inking preference to file.
        /// </summary>
        /// <returns></returns>
        public async Task SaveAsync()
        {
            AppEventSource.Log.Debug("ViewerPage: Saving drawing preference...");
            try
            {
                StorageFile file = await
                    ApplicationData.Current.LocalFolder.CreateFileAsync(INKING_PREFERENCE_FILENAME, CreationCollisionOption.ReplaceExisting);
                await SuspensionManager.SerializeToFileAsync(this, typeof(InkingPreference), file);
            }
            catch (Exception ex)
            {
                App.NotifyUser(typeof(ViewerPage), "An Error occurred when saving inking preference.\n" + ex.Message);
            }
        }
    }
}
