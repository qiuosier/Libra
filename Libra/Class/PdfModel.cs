using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Input.Inking;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Interactive;
using System.IO;

namespace Libra.Class
{
    public class PdfModel
    {
        private PdfModelMS msPdf;
        private PdfModelSF sfPdf;
        private StorageFile pdfFile;
        private StorageFile backupFile;
        private StorageFolder cacheFolder;
        private StorageFolder backupFolder;

        private const string CACHE_FOLDER = "Cache";
        private const string BACKUP_FOLDER = "Backup";

        public double ScaleRatio { get; private set; }

        private PdfModel(StorageFile pdfStorageFile)
        {
            pdfFile = pdfStorageFile;
        }

        public Task ExportPageImage(int pageNumber, InkCanvas inkCanvas, StorageFile saveFile)
        {
            return msPdf.ExportPageImage(pageNumber, inkCanvas, saveFile);
        }

        public int PageCount
        {
            get
            {
                return msPdf.PageCount;
            }
        }

        public Size PageSize(int pageNumeber)
        {
            return msPdf.PageSize(pageNumeber);
        }

        public Windows.Data.Pdf.PdfPage GetPage(int pageNumber)
        {
            return msPdf.GetPage(pageNumber);
        }

        public async Task<BitmapImage> RenderPageImage(int pageNumber, uint renderWidth)
        {
            // Load exiting annotations
            if (sfPdf.AnnotationCount(pageNumber) == 0)
                return await msPdf.RenderPageImage(pageNumber, renderWidth);
            else
            {
                MemoryStream stream = sfPdf.ExtractPageWithoutInking(pageNumber);
                PdfModelMS pageDoc = await PdfModelMS.LoadFromStream(stream.AsRandomAccessStream());
                return await pageDoc.RenderPageImage(1, renderWidth);
            }
        }

        public List<InkStroke> LoadInFileInkAnnotations(int pageNumber)
        {
            List<PdfLoadedInkAnnotation> inkAnnotations = sfPdf.GetInkAnnotations(pageNumber);
            List<InkStroke> strokes = new List<InkStroke>();
            // Get page information from SF model
            PdfLoadedPage sfPage = sfPdf.GetPage(pageNumber);
            // Get page information from MS model
            Windows.Data.Pdf.PdfPage msPage = msPdf.GetPage(pageNumber);
            // Calculate page mapping
            PageMapping mapping = new PageMapping(msPage, sfPage);
            foreach (PdfLoadedInkAnnotation annotation in inkAnnotations)
            {
                strokes.Add(InkAnnotation2InkStroke(annotation, mapping));
            }
            return strokes;
        }

        public static async Task<PdfModel> LoadFromFile(StorageFile pdfStorageFile, StorageFolder dataFolder)
        {
            PdfModel pdfModel = new PdfModel(pdfStorageFile);
            await pdfModel.InitializeComponents(dataFolder);
            if (pdfModel.msPdf == null || pdfModel.sfPdf == null) return null;
            return pdfModel;
        }

        private async Task InitializeComponents(StorageFolder dataFolder)
        {
            // Create cache and backup folder
            cacheFolder = await dataFolder.CreateFolderAsync(CACHE_FOLDER, CreationCollisionOption.OpenIfExists);
            backupFolder = await dataFolder.CreateFolderAsync(BACKUP_FOLDER, CreationCollisionOption.OpenIfExists);
            // Delete existing backup copies
            foreach (StorageFile file in await backupFolder.GetFilesAsync())
            {
                try
                {
                    await file.DeleteAsync();
                }
                catch
                {

                }
            }

            // Create a backup copy
            backupFile = await pdfFile.CopyAsync(backupFolder, pdfFile.Name, NameCollisionOption.GenerateUniqueName);


            // Load the file to Microsoft PDF document model
            // The Microsoft model is used to render the PDF pages.
            msPdf = await PdfModelMS.LoadFromFile(backupFile);
            // Return null if failed to load the file to Microsoft model
            if (msPdf == null) return;
            // Load the file to Syncfusion PDF document model
            // The Syncfusion model is used to save ink annotations.
            if (msPdf.isPasswordProtected)
            {
                sfPdf = await PdfModelSF.LoadFromFile(pdfFile, msPdf.Password);
            }
            else sfPdf = await PdfModelSF.LoadFromFile(pdfFile);
            // Return null if failed to load the file to Syncfusion model
            if (sfPdf == null) return;

            ScaleRatio = sfPdf.GetPage(1).Size.Width / msPdf.GetPage(1).Dimensions.MediaBox.Width;
        }

        /// <summary>
        /// Converts Windows UI Color to System Drawing Color.
        /// </summary>
        /// <param name="color">Windows UI Color</param>
        /// <returns>System Drawing Color</returns>
        private System.Drawing.Color ColorFromUI(Windows.UI.Color color)
        {
            return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        private Windows.UI.Color ColorToUI(PdfColor color)
        {
            Windows.UI.Color c = Windows.UI.Color.FromArgb(255, color.R, color.G, color.B);
            return c;
        }

        /// <summary> 
        /// Save the ink annotations into the pdf file. 
        /// </summary> 
        /// <param name="inkDictionary"></param> 
        /// <returns></returns> 
        /// <remarks> 
        /// The page size returned from Syncfusion pdf is the media box size. 
        /// The page size displayed to the end user is the crop box size. 
        /// The size of the ink canvas is the same as the crop box size. 
        /// Syncfusion uses the bottom left corner as the origin, while ink canvas uses the top left corner. 
        /// </remarks> 
        public async Task<bool> SaveInkingToPdf(InkingManager inkManager)
        {
            // Indicate whether any ink annotation is added to the PDF file
            bool fileChanged = false;
            // Remove ereased ink annotations
            foreach (KeyValuePair<int, List<InkStroke>> entry in await inkManager.ErasedStrokesDictionary())
            {
                // The key of the dictionary is page number, which is 1-based.
                int pageNumber = entry.Key;
                PdfLoadedPage sfPage = sfPdf.GetPage(pageNumber);
                // Get page information from MS model
                Windows.Data.Pdf.PdfPage msPage = msPdf.GetPage(pageNumber);

                PageMapping mapping = new PageMapping(msPage, sfPage);
                List<PdfInkAnnotation> erasedAnnotations = new List<PdfInkAnnotation>();
                // Add each ink stroke to the page
                foreach (InkStroke stroke in entry.Value)
                {
                    PdfInkAnnotation inkAnnotation = InkStroke2InkAnnotation(stroke, mapping);
                    erasedAnnotations.Add(inkAnnotation);
                }
                fileChanged = sfPdf.RemoveInkAnnotations(sfPage, erasedAnnotations);
            }


            // Add new ink annotations
            foreach (KeyValuePair<int, InkStrokeContainer> entry in await inkManager.InAppInkDictionary())
            {
                PdfLoadedPage sfPage = sfPdf.GetPage(entry.Key);
                // Get page information from MS model
                Windows.Data.Pdf.PdfPage msPage = msPdf.GetPage(entry.Key);

                PageMapping mapping = new PageMapping(msPage, sfPage);
                
                // Add each ink stroke to the page
                foreach (InkStroke stroke in entry.Value.GetStrokes())
                {
                    PdfInkAnnotation inkAnnotation = InkStroke2InkAnnotation(stroke, mapping);
                    sfPage.Annotations.Add(inkAnnotation);
                    fileChanged = true;
                }
            }

            // Save the file only if there are changes.
            bool inkSaved = false;
            if (fileChanged)
            {
                try
                {
                    inkSaved = await sfPdf.SaveAsync();
                }
                catch (Exception ex)
                {
                    App.NotifyUser(typeof(ViewerPage), "Error: \n" + ex.Message, true);
                }
            }
            return !(inkSaved ^ fileChanged);
        }

        public async Task ReloadFile()
        {
            msPdf = await PdfModelMS.LoadFromFile(pdfFile);
        }

        private PdfInkAnnotation InkStroke2InkAnnotation(InkStroke stroke, PageMapping mapping)
        {
            List<float> strokePoints = new List<float>();
            foreach (InkPoint p in stroke.GetInkPoints())
            {
                float X = (float)(p.Position.X * mapping.ScaleRatio + mapping.Offset.X);
                float Y = (float)(p.Position.Y * mapping.ScaleRatio + mapping.Offset.Y);
                switch (mapping.Rotation)
                {
                    case 0: // No rotation
                        {
                            strokePoints.Add(X);
                            strokePoints.Add(mapping.PageSize.Height - Y);
                            break;
                        }
                    case 1: // 90-degree rotation
                        {
                            strokePoints.Add(Y);
                            strokePoints.Add(X);
                            break;
                        }
                    case 2: // 180-degree rotation
                        {
                            strokePoints.Add(mapping.PageSize.Width - X);
                            strokePoints.Add(Y);
                            break;
                        }
                    case 3: // 270-degree rotation
                        {
                            strokePoints.Add(mapping.PageSize.Height - Y);
                            strokePoints.Add(mapping.PageSize.Width - X);
                            break;
                        }
                }
            }
            PdfInkAnnotation inkAnnotation = new PdfInkAnnotation(mapping.Rectangle, strokePoints)
            {
                // Color
                Color = new PdfColor(ColorFromUI(stroke.DrawingAttributes.Color)),
                // Size
                BorderWidth = (int)Math.Round(stroke.DrawingAttributes.Size.Width * mapping.ScaleRatio)
            };
            return inkAnnotation;
        }

        private InkStroke InkAnnotation2InkStroke(PdfLoadedInkAnnotation inkAnnotation, PageMapping mapping)
        {
            List<float> strokePoints = inkAnnotation.InkList;
            InkStrokeBuilder builder = new InkStrokeBuilder();
            List<Point> InkPoints = new List<Point>();
            // Construct ink points
            for (int i = 0; i < strokePoints.Count; i = i + 2)
            {
                if ((i + 1) >= strokePoints.Count) {
                    // TODO: Something must be wrong.
                    break;
                }
                double X = 0, Y = 0;
                float W = strokePoints[i];
                float Z = strokePoints[i + 1];
                switch (mapping.Rotation)
                {
                    case 0: // No rotation
                        {
                            X = W;
                            Y = mapping.PageSize.Height - Z;
                            break;
                        }
                    case 1: // 90-degree rotation
                        {
                            X = Z;
                            Y = W;
                            break;
                        }
                    case 2: // 180-degree rotation
                        {
                            X = mapping.PageSize.Width - W;
                            Y = Z;
                            break;
                        }
                    case 3: // 270-degree rotation
                        {
                            X = mapping.PageSize.Width - Z;
                            Y = mapping.PageSize.Height - W;
                            break;
                        }
                }
                double pointX = (X - mapping.Offset.X) / mapping.ScaleRatio;
                double pointY = (Y - mapping.Offset.Y) / mapping.ScaleRatio;
                InkPoints.Add(new Point(pointX, pointY));
            }
            InkStroke stroke = builder.CreateStroke(InkPoints);
            InkDrawingAttributes drawingAttributes = new InkDrawingAttributes();
            drawingAttributes.FitToCurve = true;
            drawingAttributes.Color = ColorToUI(inkAnnotation.Color);
            stroke.DrawingAttributes = drawingAttributes;
            return stroke;
        }

        private class PageMapping
        {
            public Point Offset { get; private set; }
            public System.Drawing.SizeF PageSize { get; private set; }
            public System.Drawing.RectangleF Rectangle { get; private set; }
            // 0 = No Rotation
            // 1 = Rotated 90 degrees
            // 2 = Rotated 180 degrees
            // 3 = Rotated 270 degrees
            public int Rotation { get; private set; }
            public double ScaleRatio { get; private set; }

            public PageMapping(Windows.Data.Pdf.PdfPage msPage, PdfLoadedPage sfPage)
            {
                Rotation = (int)msPage.Rotation;
                // The page size returned from Syncfusion pdf is the media box size.
                ScaleRatio = sfPage.Size.Width / msPage.Dimensions.MediaBox.Width;

                // The ink canvas size is the same as crop box
                // Crop box could be smaller than media box
                // There will be an offset if the crop box is smaller than the media box.
                Offset = new Point(
                    msPage.Dimensions.CropBox.Left * ScaleRatio,
                    msPage.Dimensions.CropBox.Top * ScaleRatio
                );
                PageSize = new System.Drawing.SizeF(sfPage.Size.Width, sfPage.Size.Height);
                Rectangle = new System.Drawing.RectangleF(0, 0, sfPage.Size.Width, sfPage.Size.Height);
            }
        }
    }
}
