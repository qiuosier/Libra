using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;

namespace Libra.Class
{
    public class PageCollection : ObservableCollection<PageDetail>, IItemsRangeInfo
    {
        private PdfDocument pdfDocument;

        public PageCollection(PdfDocument pdfDoc)
        {
            this.pdfDocument = pdfDoc;
        }

        /// <summary>
        /// Indicate whether the collection has been initialized.
        /// </summary>
        private bool isInitialized = false;

        /// <summary>
        /// Prevent the initialization method to be involked again before it finishs.
        /// </summary>
        private bool isInitializing = false;

        /// <summary>
        /// Initialize the collection with blank pages
        /// </summary>
        /// <returns></returns>
        private async Task InitializeBlankPages()
        {
            if (this.isInitializing) return;
            this.isInitializing = true;
            this.Clear();
            await Task.Run(() =>
            {
                for (int i = 1; i <= this.pdfDocument.PageCount; i++)
                {
                    this.Add(new PageDetail(i));
                }
            });
            this.isInitialized = true;
        }

        /// <summary>
        /// Prevent the rendering method to be involked again before it finishs.
        /// </summary>
        private bool isRendering = false;
        public async void RangesChanged(ItemIndexRange visibleRange, IReadOnlyList<ItemIndexRange> trackedItems)
        {
            if (isRendering) return;
            isRendering = true;
            for (int i = visibleRange.FirstIndex; i <= visibleRange.LastIndex; i++)
            {
                if (this[i].PageImage != null) continue;
                int pageNumber = this[i].PageNumber;
                this.RemoveAt(i);
                this.Insert(i,new PageDetail(pageNumber, await RenderPageImage(pageNumber, 200 - 10)));
            }
            isRendering = false;
        }

        /// <summary>
        /// Render a pdf page to bitmap image
        /// </summary>
        /// <param name="pageNumber">Page number</param>
        /// <param name="renderWidth">Desired pixel width of the image</param>
        /// <returns></returns>
        private async Task<BitmapImage> RenderPageImage(int pageNumber, uint renderWidth)
        {
            // Render pdf image
            InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();
            PdfPage page = pdfDocument.GetPage(Convert.ToUInt32(pageNumber - 1));
            PdfPageRenderOptions options = new PdfPageRenderOptions();
            options.DestinationWidth = renderWidth;
            await page.RenderToStreamAsync(stream, options);
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.SetSource(stream);
            return bitmapImage;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls
        

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~PageCollection() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
}
