using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Input.Inking;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Interactive;
using System.IO;


namespace Libra.Class
{
    /// <summary>
    /// Contains parameters for mapping between a MS PDF page and a Syfusion PDF page.
    /// </summary>
    class PageMapping
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

        /// <summary>
        /// Converts an ink stroke (displaying on the screen) to an ink annotation (to be saved in the file).
        /// </summary>
        /// <param name="stroke"></param>
        /// <param name="mapping"></param>
        /// <returns></returns>
        public PdfInkAnnotation InkStroke2InkAnnotation(InkStroke stroke)
        {
            List<float> strokePoints = new List<float>();
            foreach (InkPoint p in stroke.GetInkPoints())
            {
                float X = (float)(p.Position.X * ScaleRatio + Offset.X);
                float Y = (float)(p.Position.Y * ScaleRatio + Offset.Y);
                switch (Rotation)
                {
                    case 0: // No rotation
                        {
                            strokePoints.Add(X);
                            strokePoints.Add(PageSize.Height - Y);
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
                            strokePoints.Add(PageSize.Width - X);
                            strokePoints.Add(Y);
                            break;
                        }
                    case 3: // 270-degree rotation
                        {
                            strokePoints.Add(PageSize.Height - Y);
                            strokePoints.Add(PageSize.Width - X);
                            break;
                        }
                }
            }
            PdfInkAnnotation inkAnnotation = new PdfInkAnnotation(Rectangle, strokePoints)
            {
                // Color
                Color = new PdfColor(ColorFromUI(stroke.DrawingAttributes.Color)),
                // Size
                // TODO: Possible 0-width
                BorderWidth = (int)Math.Round(stroke.DrawingAttributes.Size.Width * ScaleRatio)
            };
            if (stroke.DrawingAttributes.DrawAsHighlighter)
                inkAnnotation.Opacity = InkingManager.HighlighterOpacity;
            return inkAnnotation;
        }

        /// <summary>
        /// Converts an ink annotation (from the PDF file) to an ink stroke (to be displayed on the screen).
        /// </summary>
        /// <param name="inkAnnotation"></param>
        /// <param name="mapping"></param>
        /// <returns></returns>
        public InkStroke InkAnnotation2InkStroke(PdfLoadedInkAnnotation inkAnnotation)
        {
            List<float> strokePoints = inkAnnotation.InkList;
            InkStrokeBuilder builder = new InkStrokeBuilder();
            List<Point> InkPoints = new List<Point>();
            // Construct ink points
            for (int i = 0; i < strokePoints.Count; i = i + 2)
            {
                if ((i + 1) >= strokePoints.Count)
                {
                    // TODO: Something must be wrong.
                    break;
                }
                double X = 0, Y = 0;
                float W = strokePoints[i];
                float Z = strokePoints[i + 1];
                switch (Rotation)
                {
                    case 0: // No rotation
                        {
                            X = W;
                            Y = PageSize.Height - Z;
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
                            X = PageSize.Width - W;
                            Y = Z;
                            break;
                        }
                    case 3: // 270-degree rotation
                        {
                            X = PageSize.Width - Z;
                            Y = PageSize.Height - W;
                            break;
                        }
                }
                double pointX = (X - Offset.X) / ScaleRatio;
                double pointY = (Y - Offset.Y) / ScaleRatio;
                InkPoints.Add(new Point(pointX, pointY));
            }
            InkStroke stroke = builder.CreateStroke(InkPoints);
            Windows.UI.Color color = ColorToUI(inkAnnotation.Color);
            double width = inkAnnotation.BorderWidth;
            if (width < InkingPreference.MIN_PEN_SIZE) width = InkingPreference.MIN_PEN_SIZE;
            width = width / ScaleRatio;
            Size size = new Size(width, width);
            if (inkAnnotation.Opacity == InkingManager.HighlighterOpacity)
            {
                stroke.DrawingAttributes = InkingManager.HighlighterDrawingAttributes(color, size);
            }
            else
            {
                stroke.DrawingAttributes = InkingManager.PencilDrawingAttributes(color, size);
            }

            
            return stroke;
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

        /// <summary>
        /// Converts a Syfusion PDF color to Windows UI color.
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        private Windows.UI.Color ColorToUI(PdfColor color)
        {
            Windows.UI.Color c = Windows.UI.Color.FromArgb(255, color.R, color.G, color.B);
            return c;
        }
    }
}
