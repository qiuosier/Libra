using System;
using System.Collections.Generic;
using System.Drawing;
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
using Syncfusion.Pdf.Parsing;


namespace Libra.Class
{
    public class PdfModel
    {
        private PdfModelMS msPdf;
        private PdfModelSF sfPdf;
        private StorageFile pdfFile;
        private StorageFolder cacheFolder;
        private StorageFolder backupFolder;

        private const string CACHE_FOLDER = "Cache";
        private const string BACKUP_FOLDER = "Backup";

        private PdfModel(StorageFile pdfStorageFile)
        {
            pdfFile = pdfStorageFile;
        }

        public Windows.Data.Pdf.PdfDocument PdfDoc
        {
            get
            {
                return msPdf.PdfDoc;
            }
        }

        /// <summary>
        /// Return the page size ratio of SF Model / MS model
        /// </summary>
        /// <param name="pdfDoc"></param>
        /// <returns></returns>
        public double ScaleRatio()
        {
            PdfLoadedPage sfPage = sfPdf.PdfDoc.Pages[0] as PdfLoadedPage;
            Windows.Data.Pdf.PdfPage msPage = msPdf.PdfDoc.GetPage(0);
            return sfPage.Size.Width / msPage.Dimensions.MediaBox.Width;
        }

        public Task ExportPageImage(int pageNumber, InkCanvas inkCanvas, StorageFile saveFile)
        {
            return msPdf.ExportPageImage(pageNumber, inkCanvas, saveFile);
        }

        public int PageCount()
        {
            return msPdf.PageCount();
        }

        public Windows.Foundation.Size PageSize(int pageNumeber)
        {
            return msPdf.PageSize(pageNumeber);
        }

        public async Task<BitmapImage> RenderPageImage(int pageNumber, uint renderWidth)
        {
            // Load exiting annotations
            if (sfPdf.AnnotationCount(pageNumber) == 0)
                return await msPdf.RenderPageImage(pageNumber, renderWidth);
            else
            {
                StorageFile storageFile = await sfPdf.ExtractPageWithoutInking(pageNumber, cacheFolder);
                PdfModelMS pageDoc = await PdfModelMS.LoadFromFile(storageFile);
                return await pageDoc.RenderPageImage(1, renderWidth);
            }
        }

        public List<InkStroke> LoadInkAnnotations(int pageNumber)
        {
            return sfPdf.GetInkAnnotations(pageNumber);
        }

        public static async Task<PdfModel> LoadFromFile(StorageFile pdfStorageFile, StorageFolder dataFolder)
        {
            PdfModel pdfModel = new PdfModel(pdfStorageFile);
            // Load the file to Microsoft PDF document model
            // The Microsoft model is used to render the PDF pages.
            pdfModel.msPdf = await PdfModelMS.LoadFromFile(pdfStorageFile);
            // Return null if failed to load the file to Microsoft model
            if (pdfModel.msPdf == null) return null;
            // Load the file to Syncfusion PDF document model
            // The Syncfusion model is used to save ink annotations.
            if (pdfModel.msPdf.isPasswordProtected) 
            {
                pdfModel.sfPdf = await PdfModelSF.LoadFromFile(pdfStorageFile, pdfModel.msPdf.Password);
            }
            else pdfModel.sfPdf = await PdfModelSF.LoadFromFile(pdfStorageFile);
            // Return null if failed to load the file to Syncfusion model
            if (pdfModel.sfPdf == null) return null;
            
            // Create cache and backup folder
            pdfModel.cacheFolder = await dataFolder.CreateFolderAsync(CACHE_FOLDER, CreationCollisionOption.OpenIfExists);
            pdfModel.backupFolder = await dataFolder.CreateFolderAsync(BACKUP_FOLDER, CreationCollisionOption.OpenIfExists);
            return pdfModel;
        }

        /// <summary>
        /// Converts Windows UI Color to System Drawing Color.
        /// </summary>
        /// <param name="color">Windows UI Color</param>
        /// <returns>System Drawing Color</returns>
        private Color ColorFromUI(Windows.UI.Color color)
        {
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        /// <summary> 
        /// Save the ink annotations into the pdf file. 
        /// </summary> 
        /// <param name="inkManager"></param> 
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
            // Add ink annotations for each page
            foreach (KeyValuePair<int, InkStrokeContainer> entry in inkManager.InkDictionary)
            {
                // The key of the dictionary is page number, which is 1-based. Page index is 0-based.
                int pageIndex = entry.Key - 1;
                PdfLoadedPage sfPage = sfPdf.PdfDoc.Pages[pageIndex] as PdfLoadedPage;
                // Get page information from MS model
                Windows.Data.Pdf.PdfPage msPage = msPdf.PdfDoc.GetPage((uint)pageIndex);
                int rotation = (int)msPage.Rotation;
                // The page size returned from Syncfusion pdf is the media box size.
                double scaleRatio = sfPage.Size.Width / msPage.Dimensions.MediaBox.Width;

                // The ink canvas size is the same as crop box
                // Crop box could be smaller than media box
                // There will be an offset if the crop box is smaller than the media box.
                double xOffset = msPage.Dimensions.CropBox.Left * scaleRatio;
                double yOffset = msPage.Dimensions.CropBox.Top * scaleRatio;
                RectangleF rectangle = new RectangleF(0, 0, sfPage.Size.Width, sfPage.Size.Height);
                // Add each ink stroke to the page
                foreach (InkStroke stroke in entry.Value.GetStrokes())
                {
                    List<float> strokePoints = new List<float>();
                    foreach (InkPoint p in stroke.GetInkPoints())
                    {
                        float X = (float)(p.Position.X * scaleRatio + xOffset);
                        float Y = (float)(p.Position.Y * scaleRatio + yOffset);
                        switch (rotation)
                        {
                            case 0: // No rotation
                                {
                                    strokePoints.Add(X);
                                    strokePoints.Add(sfPage.Size.Height - Y);
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
                                    strokePoints.Add(sfPage.Size.Width - X);
                                    strokePoints.Add(Y);
                                    break;
                                }
                            case 3: // 270-degree rotation
                                {
                                    strokePoints.Add(sfPage.Size.Height - Y);
                                    strokePoints.Add(sfPage.Size.Width - X);
                                    break;
                                }
                        }
                    }
                    PdfInkAnnotation inkAnnotation = new PdfInkAnnotation(rectangle, strokePoints);
                    // Color
                    inkAnnotation.Color = new PdfColor(ColorFromUI(stroke.DrawingAttributes.Color));
                    // Size
                    inkAnnotation.BorderWidth = (int)Math.Round(stroke.DrawingAttributes.Size.Width * scaleRatio);
                    sfPage.Annotations.Add(inkAnnotation);
                    fileChanged = true;
                }
            }
            bool inkSaved = false;
            // Save the file only if there are changes.
            if (fileChanged)
            {
                try
                {
                    inkSaved = await sfPdf.PdfDoc.SaveAsync(pdfFile);
                }
                catch (Exception ex)
                {
                    App.NotifyUser(typeof(ViewerPage), "Error: \n" + ex.Message, true);
                }
            }
            //pdf.Close(true);
            return !(inkSaved ^ fileChanged);
        }

        public async Task ReloadFile()
        {
            msPdf = await PdfModelMS.LoadFromFile(pdfFile);
        }
    }
}
