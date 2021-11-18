using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FaceAPI = Microsoft.Azure.CognitiveServices.Vision.Face;
using VisionAPI = Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
namespace AzureCognitiveServices
{
    public static class Visualization
    {
        private static readonly SolidColorBrush s_lineBrush = new(new System.Windows.Media.Color { R = 255, G = 185, B = 0, A = 255 });
        private static readonly Typeface s_typeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        private static BitmapSource DrawOverlay(BitmapSource baseImage, Action<DrawingContext, double> drawAction)
        {
            double annotationScale = (double)baseImage.PixelHeight / 320.0;

            DrawingVisual visual = new();
            DrawingContext drawingContext = visual.RenderOpen();

            drawingContext.DrawImage(baseImage, new Rect(0, 0, baseImage.Width, baseImage.Height));

            drawAction(drawingContext, annotationScale);

            drawingContext.Close();

            RenderTargetBitmap outputBitmap = new(
                baseImage.PixelWidth, baseImage.PixelHeight,
                baseImage.DpiX, baseImage.DpiY, PixelFormats.Pbgra32);

            outputBitmap.Render(visual);

            return outputBitmap;
        }

        public static BitmapSource DrawTags(BitmapSource baseImage, VisionAPI.Models.ImageTag[] tags)
        {
            if (tags == null)
            {
                return baseImage;
            }

            Action<DrawingContext, double> drawAction = (drawingContext, annotationScale) =>
            {
                double y = 0;
                foreach (var tag in tags)
                {
                    // Create formatted text--in a particular font at a particular size


                    FormattedText ft = new(tag.Name,
                        CultureInfo.CurrentCulture, FlowDirection.LeftToRight, s_typeface,
                        32 * annotationScale, Brushes.Black,0.5);

                    // Instead of calling DrawText (which can only draw the text in a solid colour), we
                    // convert to geometry and use DrawGeometry, which allows us to add an outline. 
                    var geom = ft.BuildGeometry(new System.Windows.Point(10 * annotationScale, y));
                    drawingContext.DrawGeometry(s_lineBrush, new Pen(Brushes.Black, 2 * annotationScale), geom);
                    // Move line down
                    y += 32 * annotationScale;
                }
            };

            return DrawOverlay(baseImage, drawAction);
        }

        public static BitmapSource DrawFaces(BitmapSource baseImage, FaceAPI.Models.DetectedFace[] faces, string[] celebName)
        {
            if (faces == null)
            {
                return baseImage;
            }

            Action<DrawingContext, double> drawAction = (drawingContext, annotationScale) =>
            {
                for (int i = 0; i < faces.Length; i++)
                {
                    var face = faces[i];
                    if (face.FaceRectangle == null) { continue; }

                    Rect faceRect = new(
                        face.FaceRectangle.Left, face.FaceRectangle.Top,
                        face.FaceRectangle.Width, face.FaceRectangle.Height);

                    var summary = new StringBuilder();

                    if (face.FaceAttributes != null)
                    {
                        summary.Append(Aggregation.SummarizeFaceAttributes(face.FaceAttributes));

                        if (face.FaceAttributes.Emotion != null)
                        {
                            summary.Append(Aggregation.SummarizeEmotion(face.FaceAttributes.Emotion));
                        }
                    }

                    if (celebName?[i] != null)
                    {
                        summary.Append(celebName[i]);
                    }

                    faceRect.Inflate(6 * annotationScale, 6 * annotationScale);


                    //rectangle outline
                    double lineThickness = 1 * annotationScale;

                    drawingContext.DrawRectangle(
                        Brushes.Transparent,
                        new Pen(s_lineBrush, lineThickness),
                        faceRect);

                    if (summary.Length > 0)
                    {
                        FormattedText ft = new(summary.ToString(),
                            CultureInfo.CurrentCulture, FlowDirection.LeftToRight, s_typeface,
                            8 * annotationScale, Brushes.Black,1);

                        var pad = 1 * annotationScale;

                        var ypad = pad;
                        var xpad = pad + 1 * annotationScale;
                        var origin = new System.Windows.Point(
                            faceRect.Left + xpad - lineThickness / 2,
                            faceRect.Top - ft.Height - ypad + lineThickness / 2);
                        var rect = ft.BuildHighlightGeometry(origin).GetRenderBounds(null);
                        rect.Inflate(xpad, ypad);

                        drawingContext.DrawRectangle(s_lineBrush, null, rect);
                        drawingContext.DrawText(ft, origin);
                    }
                }
            };

            return DrawOverlay(baseImage, drawAction);
        }
    }
}
