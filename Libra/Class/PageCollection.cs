using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;

namespace Libra.Class
{
    public class PageCollection : ObservableCollection<PageDetail>, IItemsRangeInfo
    {
        private const int RENDERWIDTH_THUMBNAIL = 200;
        private const int BUFFER_FACTOR = 5;

        private ItemIndexRange visibleRange;
        private PdfDocument pdfDocument;
        public int SelectedIndex { get; set; }

        public PageCollection(PdfDocument pdfDoc)
        {
            this.pdfDocument = pdfDoc;
            this.Add(new PageDetail(1));
            this.renderPagesQueue = new Queue<int>();
            this.recyclePagesQueue = new Queue<int>();
            this.IsInitialized = false;
        }

        /// <summary>
        /// Indicate whether the collection has been initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Prevent the initialization method to be involked again before it finishs.
        /// </summary>
        private bool isInitializing = false;

        /// <summary>
        /// Initialize the collection with blank pages
        /// </summary>
        /// <returns></returns>
        public async Task InitializeBlankPages()
        {
            if (this.isInitializing) return;
            this.isInitializing = true;
            this.Clear();
            await Window.Current.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal,() =>
            {
                for (int i = 1; i <= this.pdfDocument.PageCount; i++)
                {
                    this.Add(new PageDetail(i));
                }
            });
            this.IsInitialized = true;
        }

        public async void RangesChanged(ItemIndexRange visibleRange, IReadOnlyList<ItemIndexRange> trackedItems)
        {
            if (this.visibleRange == visibleRange) return;
            if (!IsInitialized) await InitializeBlankPages();
            this.visibleRange = visibleRange;
            this.renderPagesQueue.Clear();
            // Add visible pages to queue
            for (int i = visibleRange.FirstIndex; i <= visibleRange.LastIndex; i++)
            {
                this.renderPagesQueue.Enqueue(i);
            }
            // Add buffer pages to queue
            //for (int i = (int)(visibleRange.LastIndex + 1); i < visibleRange.LastIndex + visibleRange.Length / 2; i++)
            //{
            //    if(i >= 0 && i < this.pdfDocument.PageCount)
            //        this.renderPagesQueue.Enqueue(i);
            //}
            //for (int i = (int)(visibleRange.FirstIndex - visibleRange.Length / 2); i < visibleRange.FirstIndex; i++)
            //{
            //    if (i >= 0 && i < this.pdfDocument.PageCount)
            //        this.renderPagesQueue.Enqueue(i);
            //}
            if (!isRendering) await RenderPages();
        }

        /// <summary>
        /// Pages to be rendered.
        /// </summary>
        private Queue<int> renderPagesQueue;

        /// <summary>
        /// Prevent the rendering method to be involked again before it finishs.
        /// </summary>
        private bool isRendering = false;

        /// <summary>
        /// Render images for pages
        /// </summary>
        private async Task RenderPages()
        {
            // Set isRendering to true to prevent this method to be called multiple times before the rendering is finished.
            this.isRendering = true;
            while (renderPagesQueue.Count > 0)
            {
                int i = this.renderPagesQueue.Dequeue();
                if (this[i].PageImage != null) continue;
                int pageNumber = this[i].PageNumber;
                this.RemoveAt(i);
                this.Insert(i, new PageDetail(pageNumber, await RenderPageImage(pageNumber, RENDERWIDTH_THUMBNAIL)));
                this.recyclePagesQueue.Enqueue(i);
            }
            // Recycle pages when rendering is done
            RemovePages();
            this.isRendering = false;
        }

        /// <summary>
        /// Pages to be recycled.
        /// </summary>
        private Queue<int> recyclePagesQueue;
        
        /// <summary>
        /// Remove pages and release memory.
        /// </summary>
        private void RemovePages()
        {
            // use a counter to avoid infinite loop
            int counter = 0;
            uint bufferSize = BUFFER_FACTOR * visibleRange.Length;
            while (recyclePagesQueue.Count > bufferSize && counter < bufferSize)
            {
                int i = this.recyclePagesQueue.Dequeue();
                counter++;
                if ((visibleRange.FirstIndex - i) > visibleRange.Length || (i - visibleRange.LastIndex) > visibleRange.Length)
                {
                    // Remove page if it is far away
                    int pageNumber = this[i].PageNumber;
                    this.RemoveAt(i);
                    this.Insert(i, new PageDetail(pageNumber));
                }
                else this.recyclePagesQueue.Enqueue(i);
            }
        }

        /// <summary>
        /// Render a pdf page to bitmap image.
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
