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
    public class CognitiveService : IDisposable
    {
        /// <summary>
        /// face recognition group id
        /// </summary>
        public string ClientGroupId { get; set; }
        /// <summary>
        /// face recognition group name
        /// </summary>
        public string ClientGroupName { get; set; }
        /// <summary>
        /// fuse the remote results with the live stream option
        /// </summary>
        public bool FuseClientRemoteResults { get; set; }

        
        internal LiveCameraResult LatestResultsToDisplay { get; set; } = null;
        /// <summary>
        /// api connection configuration
        /// </summary>
        public ServiceHttpConnection HttpConnectionConfiguration { get; set; }

        internal FrameGrabber<LiveCameraResult> Grabber { get; set; }
        /// <summary>
        /// classifier for face detection
        /// </summary>
        internal CascadeClassifier LocalFaceDetector { get; set; } = new CascadeClassifier();
        /// <summary>
        /// how often to trigger analysis
        /// </summary>
        public TimeSpan AnalyzeInterval { get; set; } = TimeSpan.FromSeconds(1);
        /// <summary>
        /// the unprocessed image to view, in case of not using it, register in to a hidden view
        /// </summary>
        public Image LeftImage { get; set; }
        /// <summary>
        /// the processed image
        /// </summary>
        public Image RightImage { get; set; }
        /// <summary>
        /// a message area to view the response messages
        /// </summary>
        public TextBlock MessageArea { get; set; }
        public MessageBox MessageBox { get; set; }
        /// <summary>
        /// the operating app mode
        /// </summary>
        public AppMode Mode { get; set; }

        /// <summary>
        /// the mainwindow dispatcher
        /// </summary>
        public Dispatcher Dispatcher { get; set; }

        private static readonly ImageEncodingParam[] s_jpegParams = {
            new ImageEncodingParam(ImwriteFlags.JpegQuality, 60)
        };

        /// <summary>
        /// the supported app modes
        /// </summary>
        public enum AppMode
        {
            Faces,
            Emotions,
            Tags,
            Recognition
        }

        /// <summary>
        /// draws the result on top of the image
        /// </summary>
        /// <param name="frame">the analyzed frame</param>
        /// <returns></returns>
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

        /// <summary>
        ///Use a simple heuristic for matching the client-side faces to the faces in the
        /// results. Just sort both lists left-to-right, and assume a 1:1 correspondence.
        /// </summary>
        /// <param name="faces">list of api detected faces</param>
        /// <param name="clientRects">list of opencv rectangles representing the detected faces</param>
        private static void MatchAndReplaceFaceRectangles(DetectedFace[] faces, OpenCvSharp.Rect[] clientRects)
        {
 

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
        /// <summary>
        /// recognition function recognizes the faces from the given group id
        /// </summary>
        /// <param name="frame">the video frame to be analyzed</param>
        /// <returns> A <see cref="Task{LiveCameraResult}"/> representing the asynchronous API call,
        ///     and containing the faces returned by the API. </returns>
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

        /// <summary>
        /// creates a DetectedFace object from the given facerectangle
        /// </summary>
        /// <param name="rect">face rectangle representing the detected face</param>
        /// <returns>a <see cref="DetectedFace"/> object with only the face rectangle</returns>
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

        /// <summary> face function detects the face of a given person and generate the required attributes
        /// </summary>
        /// <param name="frame">the video frame to be analyzed </param>
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

        /// <summary>
        /// emotion function recognizes the emotions of the given face
        /// </summary>
        /// <param name="frame">the video frame to be analyzed</param>
        /// <returns> A <see cref="Task{LiveCameraResult}"/> representing the asynchronous API call,
        ///     and containing the faces returned by the API. </returns>
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
        /// <summary>
        /// tagging function generates tags for the recognized objects in an image
        /// </summary>
        /// <param name="frame">the video frame to be analyzed</param>
        /// <returns> A <see cref="Task{LiveCameraResult}"/> representing the asynchronous API call,
        ///     and containing the faces returned by the API. </returns>
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

        /// <summary>
        ///intializes a group with the given id and name, this group contains the model to be trained to recognize faces from given training samples
        ///*IMPORTANT* don't intialize in the MainWindow constructor
        /// </summary>
        /// <param name="groupId"> group id</param>
        /// <param name="groupName"> group name</param>
        /// <returns></returns>
        public async Task InitializeGroup(string groupId, string groupName)
        {
            ClientGroupId = groupId;
            ClientGroupName = groupName;
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

        /// <summary>
        /// adds a person to the given group, can also be used to add more images to the same person to increase the confidence of recognition
        /// </summary>
        /// <param name="groupId">group id</param>
        /// <param name="personName">person name</param>
        /// <param name="images">collection of images</param>
        /// <returns></returns>
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

        /// <summary>
        /// selects the desired app mode to be used 
        /// </summary>
        /// <param name="mode">the app mode desired </param>
        /// <param name="fuseRemoteClientResults">fuse the results from the service with the live stream</param>
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
        /// <summary>
        /// forces the application to fuse the results from the service with the live stream with any app mode
        /// </summary>
        /// <param name="fuseRemoteClientResults"></param>
        public void FuseRemoteResultsSetting(bool fuseRemoteClientResults)
        {
            FuseClientRemoteResults = fuseRemoteClientResults;
        }

        /// <summary>
        /// sets the frequency of the analysis, the analysis also calls the azure service, so keep this within a reasonable
        /// value to make sure the service has enough time to respond *above 500ms is prefered*
        /// </summary>
        /// <param name="seconds"></param>
        public void TriggerAnalysisOnInterval(TimeSpan seconds)
        {
            
            AnalyzeInterval = seconds;
            Grabber.TriggerAnalysisOnInterval(AnalyzeInterval);
        }
        /// <summary>
        /// get the number of the available cameras on the device
        /// </summary>
        /// <returns>count of available cameras</returns>
        public int GetNumberOfAvailableCameras()
        {
           return Grabber.GetNumCameras();
        }

        /// <summary>
        /// starts the image processing on the given camera number, with the provided analysis interval, it has a default value of 1 second
        /// and can be overriden using the analysisInterval parameter or with TriggerAnalysisOnInterval
        /// </summary>
        /// <param name="analysisInterval">the frequency of analysis</param>
        /// <param name="cameraNum">the selected camera number</param>
        /// <returns></returns>
        public async Task StartProcessing(TimeSpan analysisInterval ,int cameraNum = 0)
        {
            TriggerAnalysisOnInterval(analysisInterval);

            await Grabber.StartProcessingCameraAsync(cameraNum);
        }
        /// <summary>
        /// stops the image processing and turns of the camera connection
        /// </summary>
        /// <returns></returns>
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
