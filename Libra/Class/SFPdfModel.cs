using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Interactive;
using Syncfusion.Pdf.Parsing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml.Media.Imaging;

namespace Libra.Class
{
    public class SFPdfModel
    {
        private PdfLoadedDocument pdf;
        private StorageFile pdfFile;

        private SFPdfModel(StorageFile pdfStorageFile)
        {
            pdfFile = pdfStorageFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdfStorageFile"></param>
        /// <returns></returns>
        public static async Task<SFPdfModel> LoadFromFile(StorageFile pdfStorageFile)
        {
            // TODO: Password protected file.
            SFPdfModel file = new SFPdfModel(pdfStorageFile);
            file.pdf = new PdfLoadedDocument();
            await file.pdf.OpenAsync(pdfStorageFile);
            return file;
        }

        /// <summary>
        /// Save the ink annotations into the pdf file.
        /// </summary>
        /// <param name="inkManager"></param>
        /// <returns></returns>
        /// <remarks>
        /// The page size returned from Syncfusion pdf is the media box size.
        /// Syncfusion uses the bottom left corner as the origin, while ink canvas uses the top left corner.
        /// </remarks>
        public async Task<bool> SaveInkingToPdf(InkingManager inkManager, Windows.Data.Pdf.PdfDocument pdfDoc)
        {
            bool fileChanged = false;
            foreach (KeyValuePair<int, InkStrokeContainer> entry in inkManager.InkDictionary)
            {
                int pageIndex = entry.Key - 1;
                PdfLoadedPage sfPage = pdf.Pages[pageIndex] as PdfLoadedPage;
                Windows.Data.Pdf.PdfPage msPage = pdfDoc.GetPage((uint)pageIndex);
                int rotation = (int)msPage.Rotation;
                double scaleRatio = sfPage.Size.Width / msPage.Dimensions.MediaBox.Width;
                double xOffset = msPage.Dimensions.TrimBox.Left * scaleRatio;
                double yOffset = msPage.Dimensions.TrimBox.Top * scaleRatio;

                // Save ink strokes as image
                // File cannot be loaded in this app again???
                //PdfPageLayer layer = page.Layers.Add();
                //PdfGraphics graphics = layer.Graphics;
                //CanvasDevice device = CanvasDevice.GetSharedDevice();
                //CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, page.Size.Width, page.Size.Height, 96*2);
                //using (var ds = renderTarget.CreateDrawingSession())
                //{
                //    ds.Clear(Windows.UI.Colors.Transparent);
                //    ds.DrawInk(entry.Value.GetStrokes());
                //}
                //IRandomAccessStream inkStream = new InMemoryRandomAccessStream();
                //await renderTarget.SaveAsync(inkStream, CanvasBitmapFileFormat.Png);
                //PdfImage image = PdfImage.FromStream(inkStream.AsStreamForRead());
                //graphics.DrawImage(image, new System.Drawing.PointF(0, 0));

                // Save ink strokes and free hand drawing
                // The following information will be lost:
                // Pressure
                // Pen shape
                RectangleF rectangle = new RectangleF(0, 0, sfPage.Size.Width, sfPage.Size.Height);
                foreach (InkStroke stroke in entry.Value.GetStrokes())
                {
                    List<float> strokePoints = new List<float>();
                    foreach (InkPoint p in stroke.GetInkPoints())
                    {
                        float X = (float)(p.Position.X * scaleRatio + xOffset);
                        float Y = (float)(p.Position.Y * scaleRatio + yOffset);
                        switch (rotation)
                        {
                            
                            case 0:
                                {
                                    strokePoints.Add(X);
                                    strokePoints.Add(sfPage.Size.Height - Y);
                                    break;
                                }
                            case 1:
                                {
                                    strokePoints.Add(Y);
                                    strokePoints.Add(X);
                                    break;
                                }
                            case 2:
                                {
                                    strokePoints.Add(sfPage.Size.Width - X);
                                    strokePoints.Add(Y);
                                    break;
                                }
                            case 3:
                                {
                                    strokePoints.Add(sfPage.Size.Height - Y);
                                    strokePoints.Add(sfPage.Size.Width - X);
                                    break;
                                }
                        }
                    }
                    PdfInkAnnotation inkAnnotation = new PdfInkAnnotation(rectangle, strokePoints);
                    inkAnnotation.Color = new PdfColor(Color.FromArgb(Windows.UI.Colors.Red.A, Windows.UI.Colors.Red.R, Windows.UI.Colors.Red.G, Windows.UI.Colors.Red.B));
                    inkAnnotation.BorderWidth = (int)(stroke.DrawingAttributes.Size.Width * scaleRatio);
                    sfPage.Annotations.Add(inkAnnotation);
                    fileChanged = true;
                }
            }
            bool status = false;
            // Save the file only if there are changes.
            if (fileChanged)
            {
                // TODO: handle access denied exception
                status = await pdf.SaveAsync(pdfFile);
            }
                
            //pdf.Close(true);
            // Remove inking from the app
            await inkManager.RemoveInAppInking();
            return status;
        }
    }
}
