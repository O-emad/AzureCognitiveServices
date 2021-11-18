using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FaceAPI = Microsoft.Azure.CognitiveServices.Vision.Face;
using VisionAPI = Microsoft.Azure.CognitiveServices.Vision.ComputerVision;

namespace AzureCognitiveServices.Client
{
    public class CognitiveService :IDisposable
    {
        public string ClientGroupId { get; set; } = "5f432198";
        public string ClientGroupName { get; set; } = "Clients";

        public bool FuseClientRemoteResults;

        internal LiveCameraResult LatestResultsToDisplay { get; set; } = null;

        public ServiceHttpConnection HttpConnectionConfiguration { get; set; }

        public FrameGrabber<LiveCameraResult> Grabber { get; set; }

        public CascadeClassifier LocalFaceDetector { get; set; } = new CascadeClassifier();
        public TimeSpan AnalyzeInterval { get; set; } = TimeSpan.FromSeconds(1);

        public Image LeftImage { get; set; }
        public Image RightImage { get; set; }
        public TextBlock MessageArea { get; set; }
        public MessageBox MessageBox { get; set; }
        public AppMode Mode { get; set; }

        public Dispatcher Dispatcher { get; set; }

        private static readonly ImageEncodingParam[] s_jpegParams = {
            new ImageEncodingParam(ImwriteFlags.JpegQuality, 60)
        };

        public enum AppMode
        {
            Faces,
            Emotions,
            Tags,
            Recognition
        }
        public BitmapSource VisualizeResult(VideoFrame frame)
        {
            // Draw any results on top of the image. 

            BitmapSource visImage = frame.Image.ToBitmapSource();

            var result = LatestResultsToDisplay;

            if (result != null)
            {
                // See if we have local face detections for this image.
                var clientFaces = (OpenCvSharp.Rect[])frame.UserData;
                if (clientFaces != null && result.Faces != null)
                {
                    // If so, then the analysis results might be from an older frame. We need to match
                    // the client-side face detections (computed on this frame) with the analysis
                    // results (computed on the older frame) that we want to display. 
                    MatchAndReplaceFaceRectangles(result.Faces, clientFaces);
                }

                visImage = Visualization.DrawFaces(visImage, result.Faces, result.CelebrityNames);
                visImage = Visualization.DrawTags(visImage, result.Tags);
            }

            return visImage;
        }
        private static void MatchAndReplaceFaceRectangles(DetectedFace[] faces, OpenCvSharp.Rect[] clientRects)
        {
            // Use a simple heuristic for matching the client-side faces to the faces in the
            // results. Just sort both lists left-to-right, and assume a 1:1 correspondence. 

            // Sort the faces left-to-right. 
            var sortedResultFaces = faces
                .OrderBy(f => f.FaceRectangle.Left + 0.5 * f.FaceRectangle.Width)
                .ToArray();

            // Sort the clientRects left-to-right.
            var sortedClientRects = clientRects
                .OrderBy(r => r.Left + 0.5 * r.Width)
                .ToArray();

            // Assume that the sorted lists now corrrespond directly. We can simply update the
            // FaceRectangles in sortedResultFaces, because they refer to the same underlying
            // objects as the input "faces" array. 
            for (int i = 0; i < Math.Min(faces.Length, clientRects.Length); i++)
            {
                // convert from OpenCvSharp rectangles
                OpenCvSharp.Rect r = sortedClientRects[i];
                sortedResultFaces[i].FaceRectangle = new FaceAPI.Models.FaceRectangle { Left = r.Left, Top = r.Top, Width = r.Width, Height = r.Height };
            }
        }

        public async Task<LiveCameraResult> RecognitionAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            MemoryStream jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            var localFaces = (OpenCvSharp.Rect[])frame.UserData;
            if (localFaces == null || localFaces.Length > 0)
            {

                var _faceClient = HttpConnectionConfiguration.AsFaceApiService().Client;
                IList<DetectedFace> detectedFaces = await _faceClient.Face.DetectWithStreamAsync(jpg);
                List<Guid> detectedFacesId = detectedFaces.Select(r => r.FaceId.GetValueOrDefault()).ToList();
                IList<IdentifyResult> recognizedFaces = await _faceClient.Face.IdentifyAsync(detectedFacesId, ClientGroupId);
                List<string> recognizedNames = new();
                foreach (IdentifyResult face in recognizedFaces)
                {
                    if (face.Candidates.Count > 0)
                    {
                        Person person = await _faceClient.PersonGroupPerson.GetAsync(ClientGroupId, face.Candidates[0].PersonId);
                        recognizedNames.Add(person.Name);
                        Dispatcher.Invoke(() =>
                        {
                            MessageArea.Text = person.Name + " with confidence: " + face.Candidates[0].Confidence.ToString();
                        });

                    }
                    else
                    {
                        recognizedNames.Add("");
                    }
                }
                return new LiveCameraResult
                {
                    // Extract face rectangles from results. 
                    Faces = detectedFaces.Select(c => CreateFace(c.FaceRectangle)).ToArray(),
                    // Extract celebrity names from results. 
                    CelebrityNames = recognizedNames.ToArray()
                };
            }
            else
            {
                return new LiveCameraResult
                {
                    // Local face detection found no faces; don't call Cognitive Services.
                    Faces = Array.Empty<DetectedFace>(),
                    CelebrityNames = Array.Empty<string>()
                };
            }
        }

        private static DetectedFace CreateFace(FaceRectangle rect)
        {
            return new FaceAPI.Models.DetectedFace
            {
                FaceRectangle = new FaceAPI.Models.FaceRectangle
                {
                    Left = rect.Left,
                    Top = rect.Top,
                    Width = rect.Width,
                    Height = rect.Height
                }
            };
        }

        /// <summary> Function which submits a frame to the Face API. </summary>
        /// <param name="frame"> The video frame to submit. </param>
        /// <returns> A <see cref="Task{LiveCameraResult}"/> representing the asynchronous API call,
        ///     and containing the faces returned by the API. </returns>
        public async Task<LiveCameraResult> FacesAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            var attrs = new List<FaceAPI.Models.FaceAttributeType> {
                FaceAPI.Models.FaceAttributeType.Age,
                FaceAPI.Models.FaceAttributeType.Gender,
                FaceAPI.Models.FaceAttributeType.HeadPose
            };

            var _faceClient = HttpConnectionConfiguration.AsFaceApiService().Client;

            var faces = await _faceClient.Face.DetectWithStreamAsync(jpg, returnFaceAttributes: attrs);
            // Output. 
            return new LiveCameraResult { Faces = faces.ToArray() };
        }

        public async Task<LiveCameraResult> EmotionAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            FaceAPI.Models.DetectedFace[] faces = null;

            // See if we have local face detections for this image.
            var localFaces = (OpenCvSharp.Rect[])frame.UserData;
            if (localFaces == null || localFaces.Length > 0)
            {
                var _faceClient = HttpConnectionConfiguration.AsFaceApiService().Client;
                // If localFaces is null, we're not performing local face detection.
                // Use Cognigitve Services to do the face detection.
                faces = (await _faceClient.Face.DetectWithStreamAsync(
                    jpg,
                    returnFaceId: false,
                    returnFaceLandmarks: false,
                    returnFaceAttributes: new FaceAPI.Models.FaceAttributeType[1] { FaceAPI.Models.FaceAttributeType.Emotion })).ToArray();
            }
            else
            {
                // Local face detection found no faces; don't call Cognitive Services.
                faces = new FaceAPI.Models.DetectedFace[0];
            }

            // Output. 
            return new LiveCameraResult
            {
                Faces = faces
            };
        }

        public async Task<LiveCameraResult> TaggingAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            var _visionClient = HttpConnectionConfiguration.AsVisionApiService().Client;
            var tagResult = await _visionClient.TagImageInStreamAsync(jpg);
            // Output. 
            return new LiveCameraResult { Tags = tagResult.Tags.ToArray() };
        }
        public async Task InitializeGroup(string groupId, string groupName)
        {

            var client = HttpConnectionConfiguration.AsFaceApiService().Client;
            PersonGroup group = new();
            try
            {
                //get group if exists
                group = await client.PersonGroup.GetAsync(groupId);

            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(ex.Message + " Group was not found");
            }
            finally
            {
                if (group.PersonGroupId == null)
                    try
                    {
                        await client.PersonGroup.CreateAsync(groupId, groupName);

                    }
                    catch (Exception ex)
                    {

                        _ = MessageBox.Show(ex.Message + " Failed to create group");
                    }

            }
        }
        public async Task AddPersonToGroup(string groupId, string personName, IEnumerable<string> images)
        {
            var client = HttpConnectionConfiguration.AsFaceApiService().Client;
            Person person = await client.PersonGroupPerson.CreateAsync(ClientGroupId, personName);
            //get photos of the person
            foreach (string image in images)
            {

                using (FileStream stream = new(image, FileMode.Open))
                {
                    try
                    {
                        _ = await client.PersonGroupPerson.AddFaceFromStreamAsync(groupId, person.PersonId, stream);
                    }
                    catch (Exception ex)
                    {

                        _ = MessageBox.Show(ex.Message);
                    }
                }
            }
            await client.PersonGroup.TrainAsync(groupId);
        }
        public void SetAppMode(AppMode mode, bool fuseRemoteClientResults = true)
        {
            Mode = mode;
            switch (Mode)
            {
                case AppMode.Faces:
                    Grabber.AnalysisFunction = FacesAnalysisFunction;
                    FuseClientRemoteResults = fuseRemoteClientResults;
                    break;
                case AppMode.Emotions:
                    Grabber.AnalysisFunction = EmotionAnalysisFunction;
                    FuseClientRemoteResults = fuseRemoteClientResults;
                    break;
                case AppMode.Tags:
                    Grabber.AnalysisFunction = TaggingAnalysisFunction;
                    FuseClientRemoteResults = fuseRemoteClientResults;
                    break;
                case AppMode.Recognition:
                    Grabber.AnalysisFunction = RecognitionAnalysisFunction;
                    FuseClientRemoteResults = fuseRemoteClientResults;
                    break;
                default:
                    Grabber.AnalysisFunction = null;
                    break;
            }
        }
        public void FuseRemoteResultsSetting(bool fuseRemoteClientResults)
        {
            FuseClientRemoteResults = fuseRemoteClientResults;
        }
        public void TriggerAnalysisOnInterval(TimeSpan seconds)
        {
            
            AnalyzeInterval = seconds;
            Grabber.TriggerAnalysisOnInterval(AnalyzeInterval);
        }
        public int GetNumberOfAvailableCameras()
        {
           return Grabber.GetNumCameras();
        }
        public async Task StartProcessing(TimeSpan analysisInterval ,int cameraNum = 0)
        {
            TriggerAnalysisOnInterval(analysisInterval);

            await Grabber.StartProcessingCameraAsync(cameraNum);
        }
        public async Task StopProcessing()
        {
            await Grabber.StopProcessingAsync();
        }

        private bool disposedValue;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Grabber?.Dispose();
                    LocalFaceDetector?.Dispose();
                }

                disposedValue = true;
            }
        }

    }
}
