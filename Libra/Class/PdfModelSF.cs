using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Interactive;
using Syncfusion.Pdf.Parsing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Input.Inking;

namespace Libra.Class
{
    /// <summary>
    /// Represent the Syncfusion PDF document model
    /// </summary>
    public class PdfModelSF
    {
        /// <summary>
        /// The loaded PDF document.
        /// </summary>
        private PdfLoadedDocument pdf;

        /// <summary>
        /// The PDF file.
        /// </summary>
        private StorageFile pdfFile;

        /// <summary>
        /// Private constructor.
        /// </summary>
        /// <param name="pdfStorageFile"></param>
        private PdfModelSF(StorageFile pdfStorageFile)
        {
            pdfFile = pdfStorageFile;
        }

        /// <summary>
        /// Return the page size ratio of SF Model / MS model
        /// </summary>
        /// <param name="pdfDoc"></param>
        /// <returns></returns>
        public double ScaleRatio(Windows.Data.Pdf.PdfDocument pdfDoc)
        {
            PdfLoadedPage sfPage = pdf.Pages[0] as PdfLoadedPage;
            Windows.Data.Pdf.PdfPage msPage = pdfDoc.GetPage(0);
            return sfPage.Size.Width / msPage.Dimensions.MediaBox.Width;
        }

        /// <summary>
        /// Outputs an asynchronous operation. 
        /// When the operation completes, a PdfModelSF object, representing the PDF, is returned.
        /// </summary>
        /// <param name="pdfStorageFile">The PDF file</param>
        /// <returns></returns>
        public static async Task<PdfModelSF> LoadFromFile(StorageFile pdfStorageFile, string password = null)
        {
            PdfModelSF file = new PdfModelSF(pdfStorageFile);
            file.pdf = new PdfLoadedDocument();
            // Load PDF from file
            try
            {
                if (password == null) await file.pdf.OpenAsync(pdfStorageFile);
                else await file.pdf.OpenAsync(pdfStorageFile, password);
            }
            catch (Exception ex)
            {
                // Failed to load the file
                App.NotifyUser(typeof(ViewerPage), "Failed to open the file.\n" + ex.Message, true);
                file = null;
            }
            return file;
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
        public async Task<bool> SaveInkingToPdf(InkingManager inkManager, Windows.Data.Pdf.PdfDocument pdfDoc)
        {
            // Indicate whether any ink annotation is added to the PDF file
            bool fileChanged = false;
            // Add ink annotations for each page
            foreach (KeyValuePair<int, InkStrokeContainer> entry in inkManager.InkDictionary)
            {
                // The key of the dictionary is page number, which is 1-based. Page index is 0-based.
                int pageIndex = entry.Key - 1;
                PdfLoadedPage sfPage = pdf.Pages[pageIndex] as PdfLoadedPage;
                // Get page information from MS model
                Windows.Data.Pdf.PdfPage msPage = pdfDoc.GetPage((uint)pageIndex);
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
                    inkAnnotation.Color = new PdfColor(fromUIColor(stroke.DrawingAttributes.Color));
                    // Size
                    inkAnnotation.BorderWidth = (int) Math.Round(stroke.DrawingAttributes.Size.Width * scaleRatio);
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
                    inkSaved = await pdf.SaveAsync(pdfFile);
                }
                catch (Exception ex)
                {
                    App.NotifyUser(typeof(ViewerPage), "Error: \n" + ex.Message, true);
                }
            }
            //pdf.Close(true);
            return !(inkSaved ^ fileChanged);
        }

        /// <summary>
        /// Convert Windows UI Color to System Drawing Color
        /// </summary>
        /// <param name="color">Windows UI Color</param>
        /// <returns>System Drawing Color</returns>
        private Color fromUIColor(Windows.UI.Color color)
        {
            return Color.FromArgb(color.A, color.R, color.G, color.B);
        }

        public int AnnotationCount(int pageNumber)
        {
            PdfLoadedPage loadedPage = pdf.Pages[pageNumber - 1] as PdfLoadedPage;
            return loadedPage.Annotations.Count;
        }

        public async Task<StorageFile> ExtractPageWithoutInking(int pageNumber, StorageFolder storageFolder)
        {
            PdfDocument pageDoc = new PdfDocument();
            StorageFile storageFile = await storageFolder.CreateFileAsync("page_" + pageNumber.ToString() + ".pdf", CreationCollisionOption.ReplaceExisting);
            pageDoc.ImportPageRange(pdf, pageNumber - 1, pageNumber - 1);
            pageDoc.Pages[0].Annotations.Clear();
            PdfLoadedPage page = pdf.Pages[pageNumber - 1] as PdfLoadedPage;
            foreach (PdfAnnotation annotation in page.Annotations)
            {
                Type c = annotation.GetType();
                if (!(annotation is PdfLoadedInkAnnotation))
                    pageDoc.Pages[0].Annotations.Add(annotation);
            }
            await pageDoc.SaveAsync(storageFile);
            pageDoc.Close();
            return storageFile;
        }
    }
}
