﻿using Libra.Dialog;
using Microsoft.Graphics.Canvas;
using System;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace Libra.Class
{
    /// <summary>
    /// Represent the Microsoft PDF document model
    /// </summary>
    class PdfModelMS
    {
        /// <summary>
        /// The loaded PDF document
        /// </summary>
        private PdfDocument PdfDoc;

        /// <summary>
        /// Indicate whether the PDF file is password protected.
        /// </summary>
        public bool IsPasswordProtected { get; private set; }

        /// <summary>
        /// The password of the PDF file.
        /// </summary>
        public string Password { get; private set; }

        /// <summary>
        /// The number of pages in the PDF document.
        /// </summary>
        public int PageCount { get; private set; }

        /// <summary>
        /// Private constructor.
        /// </summary>
        private PdfModelMS()
        {
            IsPasswordProtected = false;
        }

        /// <summary>
        /// Outputs an asynchronous operation. 
        /// When the operation completes, a PdfModelMS object, representing the PDF, is returned.
        /// Use this method to initialize a MSPdfModel instance.
        /// </summary>
        /// <param name="pdfStorageFile">The PDF file</param>
        /// <returns></returns>
        public static async Task<PdfModelMS> LoadFromFile(StorageFile pdfStorageFile)
        {
            PdfModelMS msPdf = new PdfModelMS();
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
                        // Store the password of the file
                        msPdf.Password = passwordDialog.Password;
                        msPdf.IsPasswordProtected = true;
                    }
                    catch (Exception ex)
                    {
                        // Failed to load the file
                        App.NotifyUser(typeof(ViewerPage), "Failed to open the file.\n" + ex.Message, true);
                        failedToLoad = true;
                    }
                }
                else
                {
                    // User did not enter a password
                    failedToLoad = true;
                }
                // Return null if failed to load the file
                if (failedToLoad)
                {
                    return null;
                }
            }
            msPdf.PageCount = (int)msPdf.PdfDoc.PageCount;
            return msPdf;
        }

        /// <summary>
        /// Gets a page from the PDF document.
        /// </summary>
        /// <param name="pageNumber">1-based page number.</param>
        /// <returns></returns>
        public PdfPage GetPage(int pageNumber)
        {
            return PdfDoc.GetPage((uint)(pageNumber - 1));
        }

        /// <summary>
        /// Gets the PDF page's size.
        /// </summary>
        /// <param name="pageNumeber">1-based page number.</param>
        /// <returns></returns>
        public Size PageSize(int pageNumeber)
        {
            return PdfDoc.GetPage((uint)(pageNumeber - 1)).Size;
        }

        /// <summary>
        /// Renders the first page of a PDF document from a stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="renderWidth"></param>
        /// <returns></returns>
        public static async Task<BitmapImage> RenderFirstPageFromStream(IRandomAccessStream stream, uint renderWidth)
        {
            PdfModelMS msPdf = new PdfModelMS();
            msPdf.PdfDoc = await PdfDocument.LoadFromStreamAsync(stream);
            return await msPdf.RenderPageImage(1, renderWidth);
        }

        /// <summary>
        /// Renders a page of the loaded PDF document.
        /// </summary>
        /// <param name="pageNumber">1-based page number.</param>
        /// <param name="renderWidth">Page width for rendering</param>
        /// <returns></returns>
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
        public async Task ExportPageImage(int pageNumber, InkCanvas inkCanvas, StorageFile saveFile)
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
