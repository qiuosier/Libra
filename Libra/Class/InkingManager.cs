using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Input.Inking;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Interactive;

namespace Libra.Class
{
    public class InkingManager
    {
        public static float HighlighterOpacity = 0.512f;

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

        public async Task<Dictionary<int, List<InkStroke>>> ErasedStrokesDictionary()
        {
            return await inAppInking.LoadErasedStrokesDictionary();
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
                inkStrokeContainer = await inAppInking.LoadInking(pageNumber);
                // Load in-file ink strokes
                List<InkStroke> inPageStrokes = pdfModel.LoadInFileInkAnnotations(pageNumber);
                // Check if any strokes has been deleted.
                List<InkStroke> erasedStrokes = await inAppInking.LoadErasedStrokes(pageNumber);
                // Remove erased strokes from in-file strokes.
                List<InkStroke> remainingStrokes = SubstractInkStrokes(inPageStrokes, erasedStrokes.Select(item => item.Clone()).ToList());
                inkStrokeContainer.AddStrokes(remainingStrokes);
                inkDictionary[pageNumber] = inkStrokeContainer;
            }
            return inkStrokeContainer;
        }

        public void AddStrokes(int pageNumber, InkStrokeContainer inkStrokeContainer, IReadOnlyList<InkStroke> inkStrokes)
        {
            inAppInking.AddStrokes(pageNumber, inkStrokeContainer, inkStrokes);
            inkDictionary[pageNumber] = inkStrokeContainer;
        }

        public void EraseStrokes(int pageNumber, InkStrokeContainer inkStrokeContainer, IReadOnlyList<InkStroke> inkStrokes)
        {
            inAppInking.EraseStrokes(pageNumber, inkStrokeContainer, inkStrokes);
            inkDictionary[pageNumber] = inkStrokeContainer;
        }

        public async Task RemoveInAppInking()
        {
            await inAppInking.RemoveInking();
            inAppInking = await InAppInking.InitializeInking(appFolder);
        }

        private List<InkStroke> SubstractInkStrokes(List<InkStroke> strokes, List<InkStroke> erasedStrokes)
        {
            List<InkStroke> remainingStrokes = new List<InkStroke>();
            foreach(InkStroke stroke in strokes)
            {
                InkStroke matched = null;
                foreach(InkStroke eStroke in erasedStrokes)
                {
                    if (MatchInkStrokes(stroke, eStroke))
                    {
                        matched = eStroke;
                        break;
                    }
                }
                if (matched == null)
                {
                    remainingStrokes.Add(stroke);
                }
                else
                {
                    erasedStrokes.Remove(matched);
                }
            }
            return remainingStrokes;
        }

        private bool MatchInkStrokes(InkStroke stroke1, InkStroke stroke2)
        {
            // Color
            if (!stroke1.DrawingAttributes.Color.Equals(stroke2.DrawingAttributes.Color))
                return false;
            // Points
            IReadOnlyList<InkPoint> points1 = stroke1.GetInkPoints();
            IReadOnlyList<InkPoint> points2 = stroke2.GetInkPoints();
            if (points1.Count != points2.Count) return false;
            for (int i = 0; i < points1.Count; i++)
            {
                double xDiff = points1[i].Position.X - points2[i].Position.X;
                double yDiff = points1[i].Position.Y - points2[i].Position.Y;
                double threshold = 0.5;

                if (Math.Abs(xDiff) > threshold || Math.Abs(yDiff) > threshold)
                    return false;
            }
            return true;
        }

        public static InkDrawingAttributes HighlighterDrawingAttributes(Windows.UI.Color color, Size size)
        {
            InkDrawingAttributes drawingAttributes = new InkDrawingAttributes
            {
                Color = color,
                Size = size,
                PenTip = PenTipShape.Rectangle,
                DrawAsHighlighter = true,
                PenTipTransform = System.Numerics.Matrix3x2.Identity,
                IgnorePressure = true,
                FitToCurve = false,
            };
            return drawingAttributes;
        }

        public static InkDrawingAttributes PencilDrawingAttributes(Windows.UI.Color color, Size size)
        {
            InkDrawingAttributes drawingAttributes = new InkDrawingAttributes
            {
                Color = color,
                Size = size,
                PenTip = PenTipShape.Circle,
                DrawAsHighlighter = false,
                PenTipTransform = System.Numerics.Matrix3x2.Identity,
                IgnorePressure = false,
                FitToCurve = true,
            };
            return drawingAttributes;
        }
    }
}
