using Microsoft.Graphics.Canvas;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Interactive;
using Syncfusion.Pdf.Parsing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Input.Inking;

namespace Libra.Class
{
    public class PdfFile
    {
        private PdfLoadedDocument pdf;
        private StorageFile pdfStorage;
        private double sizeRatio;

        private PdfFile(StorageFile pdfStorageFile)
        {
            pdfStorage = pdfStorageFile;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdfStorageFile"></param>
        /// <returns></returns>
        public static async Task<PdfFile> OpenPdfFile(StorageFile pdfStorageFile, double firstPageWidth)
        {
            // TODO: Password protected file.
            PdfFile file = new PdfFile(pdfStorageFile);
            file.pdf = new PdfLoadedDocument();
            await file.pdf.OpenAsync(pdfStorageFile);
            file.sizeRatio = file.pdf.Pages[0].Size.Width / firstPageWidth;
            return file;
        }

        public async Task<bool> SaveInking(InkingCollection inking)
        {
            foreach (KeyValuePair<int, InkStrokeContainer> entry in inking)
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
                // Pen tip size
                RectangleF rectangle = new RectangleF(0, 0, page.Size.Width, page.Size.Height);
                foreach (InkStroke stroke in entry.Value.GetStrokes())
                {
                    List<float> strokePoints = new List<float>();
                    foreach (InkPoint p in stroke.GetInkPoints())
                    {
                        strokePoints.Add((float)(p.Position.X * sizeRatio));
                        strokePoints.Add(page.Size.Height - (float)(p.Position.Y * sizeRatio));
                    }
                    PdfInkAnnotation inkAnnotation = new PdfInkAnnotation(rectangle, strokePoints);
                    inkAnnotation.Color = new PdfColor(Color.FromArgb(Windows.UI.Colors.Red.A, Windows.UI.Colors.Red.R, Windows.UI.Colors.Red.G, Windows.UI.Colors.Red.B));
                    page.Annotations.Add(inkAnnotation);
                }
                //IRandomAccessStream inkStream = new InMemoryRandomAccessStream();
                //await entry.Value.SaveAsync(inkStream);
                //PdfAttachment attach = new PdfAttachment("inking.png", inkStream.AsStreamForRead());
                //attach.ModificationDate = DateTime.Now;
                //attach.MimeType = "PNG";
                //pdf.Attachments.Add(attach);
            }
            bool a = await pdf.Save();
            pdf.Close(true);
            return true;
        }
    }
}
