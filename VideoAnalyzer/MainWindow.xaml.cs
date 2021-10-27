using Microsoft.Azure.CognitiveServices.Vision.Face;
using FaceAPI = Microsoft.Azure.CognitiveServices.Vision.Face;
using VisionAPI = Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using VideoFrameAnalyzer;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.WpfExtensions;
using System.Net;
using Newtonsoft.Json.Linq;

namespace VideoAnalyzer
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window, IDisposable
    {
        private const string cog_Key = "5909943744314ecebf9170367f5b1a2a";
        private const string cog_endpoint = "https://offbeatface.cognitiveservices.azure.com/";
        private const string clientGroupId = "5f432198";
        private const string clientGroupName = "Clients";
        private readonly TimeSpan analyzeInterval = TimeSpan.FromSeconds(1);
        private readonly FrameGrabber<LiveCameraResult> _grabber;
        private readonly CascadeClassifier _localFaceDetector = new();
        private AppMode _mode;
        private bool _fuseClientRemoteResults;
        private LiveCameraResult _latestResultsToDisplay = null;
        private static readonly ImageEncodingParam[] s_jpegParams = {
            new ImageEncodingParam(ImwriteFlags.JpegQuality, 60)
        };
        private OpenFileDialog Files { get; set; }
        public MainWindow()
        {
            InitializeComponent();

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Create grabber. 
            _grabber = new FrameGrabber<LiveCameraResult>();

            // Set up a listener for when the client receives a new frame.
            _grabber.NewFrameProvided += (s, e) =>
            {
                if (_mode != AppMode.Emotions)
                {
                    // Local face detection. 
                    var rects = _localFaceDetector.DetectMultiScale(e.Frame.Image);
                    // Attach faces to frame. 
                    e.Frame.UserData = rects;
                }

                // The callback may occur on a different thread, so we must use the
                // MainWindow.Dispatcher when manipulating the UI. 
                _ = Dispatcher.BeginInvoke((Action)(() =>
                  {
                    // Display the image in the left pane.
                    LeftImage.Source = e.Frame.Image.ToBitmapSource();

                    // If we're fusing client-side face detection with remote analysis, show the
                    // new frame now with the most recent analysis available. 
                    if (_fuseClientRemoteResults)
                      {
                          RightImage.Source = VisualizeResult(e.Frame);
                      }
                  }));

            };

            // Set up a listener for when the client receives a new result from an API call. 
            _grabber.NewResultAvailable += (s, e) =>
            {
                _ = Dispatcher.BeginInvoke((Action)(() =>
                  {
                      if (e.TimedOut)
                      {
                          MessageArea.Text = "API call timed out.";
                      }
                      else if (e.Exception != null)
                      {
                          string apiName = "";
                          string message = e.Exception.Message;
                          if (e.Exception is FaceAPI.Models.APIErrorException faceEx)
                          {
                              apiName = "Face";
                              message = faceEx.Message;
                          }
                          else if (e.Exception is VisionAPI.Models.ComputerVisionErrorResponseException visionEx)
                          {
                              apiName = "Computer Vision";
                              message = visionEx.Message;
                          }
                          MessageArea.Text = string.Format("{0} API call failed on frame {1}. Exception: {2}", apiName, e.Frame.Metadata.Index, message);
                      }
                      else
                      {
                          _latestResultsToDisplay = e.Analysis;

                        // Display the image and visualization in the right pane. 
                        if (!_fuseClientRemoteResults)
                          {
                              RightImage.Source = VisualizeResult(e.Frame);
                          }
                      }
                  }));
            };

            // Create local face detector. 
            _ = _localFaceDetector.Load(@"Data\haarcascade_frontalface_alt2.xml");
        }

        public enum AppMode
        {
            Faces,
            Emotions,
            EmotionsWithClientFaceDetect,
            Tags,
            Recognition
        }

        private BitmapSource VisualizeResult(VideoFrame frame)
        {
            // Draw any results on top of the image. 
            BitmapSource visImage = frame.Image.ToBitmapSource();

            var result = _latestResultsToDisplay;

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

        private void Label_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Application.Current.Shutdown();
        }


        private void personButton_Click(object sender, RoutedEventArgs e)
        {
            if (AddPersonPanel.Visibility == Visibility.Collapsed)
            {
                AddPersonPanel.Visibility = Visibility.Visible;
            }
            else
            {
                AddPersonPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void startButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CameraList.HasItems)
            {
                MessageArea.Text = "No cameras found; cannot start processing";
                return;
            }
            // How often to analyze. 
            _grabber.TriggerAnalysisOnInterval(analyzeInterval);

            // Reset message. 
            MessageArea.Text = "";
            await _grabber.StartProcessingCameraAsync(CameraList.SelectedIndex);
        }



        private async void stopButton_Click(object sender, RoutedEventArgs e)
        {
            await _grabber.StopProcessingAsync();
        }

        private void ModeList_Loaded(object sender, RoutedEventArgs e)
        {
            var modes = (AppMode[])Enum.GetValues(typeof(AppMode));

            var comboBox = sender as ComboBox;
            comboBox.ItemsSource = modes.Select(m => m.ToString());
            comboBox.SelectedIndex = 0;
        }

        private async Task<LiveCameraResult> RecognitionAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            MemoryStream jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            var localFaces = (OpenCvSharp.Rect[])frame.UserData;
            if (localFaces == null || localFaces.Length > 0)
            {
                FaceAPI.ApiKeyServiceClientCredentials credentials = new(cog_Key);
                FaceClient _faceClient = new(credentials)
                {
                    Endpoint = cog_endpoint
                };
                IList<DetectedFace> detectedFaces = await _faceClient.Face.DetectWithStreamAsync(jpg);
                List<Guid> detectedFacesId = detectedFaces.Select(r => r.FaceId.GetValueOrDefault()).ToList();
                IList<IdentifyResult> recognizedFaces = await _faceClient.Face.IdentifyAsync(detectedFacesId, clientGroupId);
                List<string> recognizedNames = new();
                foreach (IdentifyResult face in recognizedFaces)
                {
                    if (face.Candidates.Count > 0)
                    {
                        Person person = await _faceClient.PersonGroupPerson.GetAsync(clientGroupId, face.Candidates[0].PersonId);
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
        private async Task<LiveCameraResult> FacesAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            var attrs = new List<FaceAPI.Models.FaceAttributeType> {
                FaceAPI.Models.FaceAttributeType.Age,
                FaceAPI.Models.FaceAttributeType.Gender,
                FaceAPI.Models.FaceAttributeType.HeadPose
            };
            FaceAPI.ApiKeyServiceClientCredentials credentials = new(cog_Key);
            FaceClient _faceClient = new(credentials)
            {
                Endpoint = cog_endpoint
            };
            var faces = await _faceClient.Face.DetectWithStreamAsync(jpg, returnFaceAttributes: attrs);
            // Output. 
            return new LiveCameraResult { Faces = faces.ToArray() };
        }

        private async Task<LiveCameraResult> EmotionAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            FaceAPI.Models.DetectedFace[] faces = null;

            // See if we have local face detections for this image.
            var localFaces = (OpenCvSharp.Rect[])frame.UserData;
            if (localFaces == null || localFaces.Length > 0)
            {
                FaceAPI.ApiKeyServiceClientCredentials credentials = new(cog_Key);
                FaceClient _faceClient = new(credentials)
                {
                    Endpoint = cog_endpoint
                };
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

        private async Task<LiveCameraResult> TaggingAnalysisFunction(VideoFrame frame)
        {
            // Encode image. 
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            VisionAPI.ApiKeyServiceClientCredentials credentials = new(cog_Key);
            ComputerVisionClient _visionClient = new(credentials)
            {
                Endpoint = cog_endpoint
            };
            var tagResult = await _visionClient.TagImageInStreamAsync(jpg);
            // Output. 
            return new LiveCameraResult { Tags = tagResult.Tags.ToArray() };
        }

        private void ModeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Disable "most-recent" results display. 
            _fuseClientRemoteResults = false;

            var comboBox = sender as ComboBox;
            var modes = (AppMode[])Enum.GetValues(typeof(AppMode));
            _mode = modes[comboBox.SelectedIndex];
            switch (_mode)
            {
                case AppMode.Faces:
                    _grabber.AnalysisFunction = FacesAnalysisFunction;
                    _fuseClientRemoteResults = true;
                    break;
                case AppMode.Emotions:
                    _grabber.AnalysisFunction = EmotionAnalysisFunction;
                    break;
                case AppMode.EmotionsWithClientFaceDetect:
                    // Same as Emotions, except we will display the most recent faces combined with
                    // the most recent API results. 
                    _grabber.AnalysisFunction = EmotionAnalysisFunction;
                    _fuseClientRemoteResults = true;
                    break;
                case AppMode.Tags:
                    _grabber.AnalysisFunction = TaggingAnalysisFunction;
                    break;
                case AppMode.Recognition:
                    _grabber.AnalysisFunction = RecognitionAnalysisFunction;
                    _fuseClientRemoteResults = true;
                    break;
                default:
                    _grabber.AnalysisFunction = null;
                    break;
            }
        }

        private void CameraList_Loaded(object sender, RoutedEventArgs e)
        {
            int numCameras = _grabber.GetNumCameras();

            if (numCameras == 0)
            {
                
                MessageArea.Text = "No cameras found!";
            }

            var comboBox = sender as ComboBox;
            comboBox.ItemsSource = Enumerable.Range(0, numCameras).Select(i => string.Format("Camera {0}", i + 1));
            comboBox.SelectedIndex = 0;
        }



        private void btnLoad_Click(object sender, RoutedEventArgs e)
        {
            Files = new OpenFileDialog
            {
                Title = "Select a picture",
                Multiselect = true,
                Filter = "All supported graphics|*.jpg;*.jpeg;*.png|" +
              "JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
              "Portable Network Graphic (*.png)|*.png"
            };
            _ = Files.ShowDialog();
        }

        private async void saveBtn_Click(object sender, RoutedEventArgs e)
        {
            PersonData person = new()
            {
                Name = personName.Text,
                Images = Files.FileNames.ToList()
            };
            await AddPersonToGroup(clientGroupId, person.Name, person.Images);
            AddPersonPanel.Visibility = Visibility.Collapsed;
        }


        public static async Task InitializeGroup(string groupId, string groupName)
        {
            FaceAPI.ApiKeyServiceClientCredentials credentials = new(cog_Key);
            FaceClient client = new(credentials)
            {
                Endpoint = cog_endpoint
            };
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
        public static async Task AddPersonToGroup(string groupId, string personName, IEnumerable<string> images)
        {
            FaceAPI.ApiKeyServiceClientCredentials credentials = new(cog_Key);
            FaceClient client = new(credentials)
            {
                Endpoint = cog_endpoint
            };
            Person person = await client.PersonGroupPerson.CreateAsync(clientGroupId, personName);
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

        private bool disposedValue = false;

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
                    _grabber?.Dispose();
                    _localFaceDetector?.Dispose();
                }

                disposedValue = true;
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeGroup(clientGroupId, clientGroupName);
        }
    }
}
