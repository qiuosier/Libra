using Libra.Dialog;
using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Libra.Class
{
    class MSPdfModel
    {
        public PdfDocument PdfDoc { get; private set; }

        private MSPdfModel()
        {

        }

        public int PageCount()
        {
            return (int)PdfDoc.PageCount;
        }

        public static async Task<MSPdfModel> LoadFromFile(StorageFile pdfStorageFile)
        {
            MSPdfModel msPdf = new MSPdfModel();
            try
            {
                // Try to load the file without a password
                msPdf.PdfDoc = await PdfDocument.LoadFromFileAsync(pdfStorageFile);
            }
            catch
            {
                // Ask the user to enter password
                PasswordContentDialog passwordDialog = new PasswordContentDialog();
                bool failedToLoad = false;
                if (await passwordDialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    // Try to load the file with a password
                    try
                    {
                        msPdf.PdfDoc = await PdfDocument.LoadFromFileAsync(pdfStorageFile, passwordDialog.Password);
                    }
                    catch
                    {
                        failedToLoad = true;
                    }
                }
                else
                {
                    failedToLoad = true;
                }
                // Notify the user and return to main page if failed to load the file.
                if (failedToLoad)
                {
                    App.NotifyUser(typeof(ViewerPage), "Failed to open the file.", true);
                    ViewerPage.Current.CloseAllViews();
                    return null;
                }
            }
            return msPdf;
        }

        public Size PageSize(int pageNumeber)
        {
            return PdfDoc.GetPage((uint)(pageNumeber - 1)).Size;
        }

        public async Task<BitmapImage> RenderPageImage(int pageNumber, uint renderWidth)
        {
            // Render pdf image
            InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();
            PdfPage page = PdfDoc.GetPage(Convert.ToUInt32(pageNumber - 1));
            PdfPageRenderOptions options = new PdfPageRenderOptions();
            options.DestinationWidth = renderWidth;
            await page.RenderToStreamAsync(stream, options);
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.SetSource(stream);
            return bitmapImage;
        }

        /// <summary>
        /// Save a rendered pdf page with inking to png file.
        /// </summary>
        /// <param name="pageNumber"></param>
        /// <param name="saveFile"></param>
        /// <returns></returns>
        public async Task Export_Page(int pageNumber, InkCanvas inkCanvas, StorageFile saveFile)
        {
            CanvasDevice device = CanvasDevice.GetSharedDevice();
            CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (int)inkCanvas.ActualWidth, (int)inkCanvas.ActualHeight, 96 * 2);

            // Render pdf page
            InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();
            PdfPage page = PdfDoc.GetPage(Convert.ToUInt32(pageNumber - 1));
            PdfPageRenderOptions options = new PdfPageRenderOptions();
            options.DestinationWidth = (uint)inkCanvas.ActualWidth * 2;
            await page.RenderToStreamAsync(stream, options);
            CanvasBitmap bitmap = await CanvasBitmap.LoadAsync(device, stream, 96 * 2);
            // Draw image with ink
            using (var ds = renderTarget.CreateDrawingSession())
            {
                ds.Clear(Windows.UI.Colors.White);
                ds.DrawImage(bitmap);
                ds.DrawInk(inkCanvas.InkPresenter.StrokeContainer.GetStrokes());
            }

            // Encode the image to the selected file on disk
            using (var fileStream = await saveFile.OpenAsync(FileAccessMode.ReadWrite))
                await renderTarget.SaveAsync(fileStream, CanvasBitmapFileFormat.Png, 1f);
        }

    }
}
