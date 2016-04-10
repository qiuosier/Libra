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
        private StorageFile pdfStorage;
        private double sizeRatio;
        private bool rotated;
        

        private SFPdfModel(StorageFile pdfStorageFile)
        {
            pdfStorage = pdfStorageFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdfStorageFile"></param>
        /// <returns></returns>
        public static async Task<SFPdfModel> LoadFromFile(StorageFile pdfStorageFile, Windows.Foundation.Size msSize)
        {
            // TODO: Password protected file.
            SFPdfModel file = new SFPdfModel(pdfStorageFile);
            file.pdf = new PdfLoadedDocument();
            await file.pdf.OpenAsync(pdfStorageFile);
            SizeF sfSize = file.pdf.Pages[0].Size;
            if ((sfSize.Width > sfSize.Height && msSize.Height > msSize.Width) ||
                (sfSize.Width < sfSize.Height && msSize.Height < msSize.Width))
            {
                file.rotated = true;
                file.sizeRatio = sfSize.Width / msSize.Height;
            }
            else
            {
                file.sizeRatio = sfSize.Width / msSize.Width;
            }
            return file;
        }

        public async Task<bool> SaveInkingToPdf(InkingManager inkManager)
        {
            foreach (KeyValuePair<int, InkStrokeContainer> entry in inkManager.InkDictionary)
            {
                int pageIndex = entry.Key - 1;
                PdfLoadedPage page = pdf.Pages[pageIndex] as PdfLoadedPage;
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
                RectangleF rectangle = new RectangleF(0, 0, page.Size.Width, page.Size.Height);
                foreach (InkStroke stroke in entry.Value.GetStrokes())
                {
                    List<float> strokePoints = new List<float>();
                    foreach (InkPoint p in stroke.GetInkPoints())
                    {
                        
                        if (rotated)
                        {
                            float X = (float)(p.Position.X * sizeRatio);
                            float Y = (float)(p.Position.Y * sizeRatio);
                            strokePoints.Add(Y);
                            strokePoints.Add(X);
                        }
                        else
                        {
                            float X = (float)(p.Position.X * sizeRatio);
                            float Y = page.Size.Height - (float)(p.Position.Y * sizeRatio);
                            strokePoints.Add(X);
                            strokePoints.Add(Y);
                        }
                    }
                    PdfInkAnnotation inkAnnotation = new PdfInkAnnotation(rectangle, strokePoints);
                    inkAnnotation.Color = new PdfColor(Color.FromArgb(Windows.UI.Colors.Red.A, Windows.UI.Colors.Red.R, Windows.UI.Colors.Red.G, Windows.UI.Colors.Red.B));
                    inkAnnotation.BorderWidth = (int)(stroke.DrawingAttributes.Size.Width * sizeRatio);
                    page.Annotations.Add(inkAnnotation);
                }
            }
            bool a = await pdf.Save();
            pdf.Close(true);
            // Remove inking from the app
            await inkManager.RemoveInAppInking();
            return a;
        }
    }
}
