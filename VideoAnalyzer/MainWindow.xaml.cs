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
        private FrameGrabber<LiveCameraResult> _grabber;
        private readonly CascadeClassifier _localFaceDetector = new CascadeClassifier();
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
            //CustomComponentsInitialize();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Create grabber. 
            _grabber = new FrameGrabber<LiveCameraResult>();

            // Set up a listener for when the client receives a new frame.
            _grabber.NewFrameProvided += (s, e) =>
            {
                if (_mode == AppMode.EmotionsWithClientFaceDetect)
                {
                    // Local face detection. 
                    var rects = _localFaceDetector.DetectMultiScale(e.Frame.Image);
                    // Attach faces to frame. 
                    e.Frame.UserData = rects;
                }

                // The callback may occur on a different thread, so we must use the
                // MainWindow.Dispatcher when manipulating the UI. 
                Dispatcher.BeginInvoke((Action)(() =>
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

                // See if auto-stop should be triggered. 
                //if (Properties.Settings.Default.AutoStopEnabled && (DateTime.Now - _startTime) > Properties.Settings.Default.AutoStopTime)
                //{
                //    _grabber.StopProcessingAsync().GetAwaiter().GetResult();
                //}
            };

            // Set up a listener for when the client receives a new result from an API call. 
            _grabber.NewResultAvailable += (s, e) =>
            {
                this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (e.TimedOut)
                    {
                        MessageArea.Text = "API call timed out.";
                    }
                    else if (e.Exception != null)
                    {
                        string apiName = "";
                        string message = e.Exception.Message;
                        var faceEx = e.Exception as FaceAPI.Models.APIErrorException;
                        var visionEx = e.Exception as VisionAPI.Models.ComputerVisionErrorResponseException;
                        if (faceEx != null)
                        {
                            apiName = "Face";
                            message = faceEx.Message;
                        }
                        else if (visionEx != null)
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
            _localFaceDetector.Load(@"Data\haarcascade_frontalface_alt2.xml");
            var task = InitializeGroup(clientGroupId, clientGroupName);
        }

        public enum AppMode
        {
            Faces,
            Emotions,
            EmotionsWithClientFaceDetect,
            Tags,
            Recognition
        }

        private async void CustomComponentsInitialize()
        {
            await InitializeGroup(clientGroupId, clientGroupName);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Create grabber. 
            _grabber = new FrameGrabber<LiveCameraResult>();

            // Set up a listener for when the client receives a new frame.
            _grabber.NewFrameProvided += (s, e) =>
            {
                if (_mode == AppMode.EmotionsWithClientFaceDetect)
                {
                    // Local face detection. 
                    var rects = _localFaceDetector.DetectMultiScale(e.Frame.Image);
                    // Attach faces to frame. 
                    e.Frame.UserData = rects;
                }

                // The callback may occur on a different thread, so we must use the
                // MainWindow.Dispatcher when manipulating the UI. 
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    // Display the image in the left pane.
                    LeftImage.Source = e.Frame.Image.ToBitmapSource();

                    // If we're fusing client-side face detection with remote analysis, show the
                    // new frame now with the most recent analysis available. 
                    if(_fuseClientRemoteResults)
                    {
                        RightImage.Source = VisualizeResult(e.Frame);
                    }
                }));

                // See if auto-stop should be triggered. 
                //if (Properties.Settings.Default.AutoStopEnabled && (DateTime.Now - _startTime) > Properties.Settings.Default.AutoStopTime)
                //{
                //    _grabber.StopProcessingAsync().GetAwaiter().GetResult();
                //}
            };

            // Set up a listener for when the client receives a new result from an API call. 
            _grabber.NewResultAvailable += (s, e) =>
            {
                this.Dispatcher.BeginInvoke((Action)(() =>
                {
                    if (e.TimedOut)
                    {
                        MessageArea.Text = "API call timed out.";
                    }
                    else if (e.Exception != null)
                    {
                        string apiName = "";
                        string message = e.Exception.Message;
                        var faceEx = e.Exception as FaceAPI.Models.APIErrorException;
                        var visionEx = e.Exception as VisionAPI.Models.ComputerVisionErrorResponseException;
                        if (faceEx != null)
                        {
                            apiName = "Face";
                            message = faceEx.Message;
                        }
                        else if (visionEx != null)
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
            _localFaceDetector.Load("Data/haarcascade_frontalface_alt2.xml");
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

        private void MatchAndReplaceFaceRectangles(FaceAPI.Models.DetectedFace[] faces, OpenCvSharp.Rect[] clientRects)
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

            // Clean leading/trailing spaces in API keys. 
           // Properties.Settings.Default.FaceAPIKey = Properties.Settings.Default.FaceAPIKey.Trim();
           // Properties.Settings.Default.VisionAPIKey = Properties.Settings.Default.VisionAPIKey.Trim();

            // Create API clients.
            var _faceClient = new FaceAPI.FaceClient(new FaceAPI.ApiKeyServiceClientCredentials(cog_Key))
            {
                Endpoint = cog_endpoint
            };
            var _visionClient = new VisionAPI.ComputerVisionClient(new VisionAPI.ApiKeyServiceClientCredentials(cog_Key))
            {
                Endpoint = cog_endpoint
            };

            // How often to analyze. 
            _grabber.TriggerAnalysisOnInterval(analyzeInterval);

            // Reset message. 
            MessageArea.Text = "";

            // Record start time, for auto-stop
            //_startTime = DateTime.Now;

            await _grabber.StartProcessingCameraAsync(CameraList.SelectedIndex);
        }



        private void stopButton_Click(object sender, RoutedEventArgs e)
        {

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
            var jpg = frame.Image.ToMemoryStream(".jpg", s_jpegParams);
            // Submit image to API. 
            //var _visionClient = new VisionAPI.ComputerVisionClient(new VisionAPI.ApiKeyServiceClientCredentials(cog_Key))
            //{
            //    Endpoint = cog_endpoint
            //};
            //var domainModelResults = await _visionClient.AnalyzeImageByDomainInStreamAsync("celebrities", jpg);

            FaceAPI.ApiKeyServiceClientCredentials credentials = new(cog_Key);
            FaceClient _faceClient = new(credentials)
            {
                Endpoint = cog_endpoint
            };
            var detectedFaces = await _faceClient.Face.DetectWithStreamAsync(jpg);
            var detectedFacesId = detectedFaces.Select(r => r.FaceId.GetValueOrDefault()).ToList();
            var recognizedFaces = await _faceClient.Face.IdentifyAsync(detectedFacesId, clientGroupId);
            var recognizedNames = new List<string>();
            foreach (var face in recognizedFaces)
            {
                if (face.Candidates.Count > 0)
                {
                    var person = await _faceClient.PersonGroupPerson.GetAsync(clientGroupId, face.Candidates[0].PersonId);
                    recognizedNames.Add(person.Name);
                }
                else
                {
                    recognizedNames.Add("");
                }
            }
            // Count the API call. 
            // Properties.Settings.Default.VisionAPICallCount++;
            // Output. 
            //var jobject = domainModelResults as JObject;
            //var celebs = jobject.ToObject<FaceAPI.Models.DetectedFace>().;

            return new LiveCameraResult
            {
                // Extract face rectangles from results. 
                Faces = detectedFaces.Select(c => CreateFace(c.FaceRectangle)).ToArray(),
                // Extract celebrity names from results. 
                CelebrityNames = recognizedNames.ToArray()
            };
        }

        private FaceAPI.Models.DetectedFace CreateFace(FaceAPI.Models.FaceRectangle rect)
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
                    //_grabber.AnalysisFunction = FacesAnalysisFunction;
                    break;
                case AppMode.Emotions:
                    //_grabber.AnalysisFunction = EmotionAnalysisFunction;
                    break;
                case AppMode.EmotionsWithClientFaceDetect:
                    // Same as Emotions, except we will display the most recent faces combined with
                    // the most recent API results. 
                   // _grabber.AnalysisFunction = EmotionAnalysisFunction;
                    _fuseClientRemoteResults = true;
                    break;
                case AppMode.Tags:
                    //_grabber.AnalysisFunction = TaggingAnalysisFunction;
                    break;
                case AppMode.Recognition:
                    _grabber.AnalysisFunction = RecognitionAnalysisFunction;
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
            Files = new OpenFileDialog();
            Files.Title = "Select a picture";
            Files.Multiselect = true;
            Files.Filter = "All supported graphics|*.jpg;*.jpeg;*.png|" +
              "JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
              "Portable Network Graphic (*.png)|*.png";
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
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _grabber?.Dispose();
                    //_visionClient?.Dispose();
                    //_faceClient?.Dispose();
                    _localFaceDetector?.Dispose();
                }

                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }
    }
}
