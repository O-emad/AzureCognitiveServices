using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Threading;
using static AzureCognitiveServices.Client.CognitiveService;
using FaceAPI = Microsoft.Azure.CognitiveServices.Vision.Face;
using VisionAPI = Microsoft.Azure.CognitiveServices.Vision.ComputerVision;

namespace AzureCognitiveServices.Client
{
    public class CognitiveServiceBuilder : ICognitiveServiceBuilder
    {
        protected CognitiveService Service;
        private CognitiveServiceBuilder()
        {
            Service = new CognitiveService();

        }
        public static CognitiveServiceBuilder Create() => new CognitiveServiceBuilder();

        public CognitiveServiceBuilder HavingHttpConnection(Action<IHttpConnectedServiceBuilder> connectionConfigure)
        {
            var builder = new HttpConnectedServiceBuilder();
            connectionConfigure(builder);
            Service.HttpConnectionConfiguration = builder.Build();
            return this;
        }

        public CognitiveService Build(Dispatcher dispatcher)
        {

            Service.Dispatcher = dispatcher;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            Service.Grabber = new FrameGrabber<LiveCameraResult>();
            // Set up a listener for when the client receives a new frame.
            Service.Grabber.NewFrameProvided += (s, e) =>
            {
                if (Service.Mode != AppMode.Emotions)
                {
                    // Local face detection. 
                    var rects = Service.LocalFaceDetector.DetectMultiScale(e.Frame.Image);
                    // Attach faces to frame. 
                    e.Frame.UserData = rects;
                }
                
                // The callback may occur on a different thread, so we must use the
                // MainWindow.Dispatcher when manipulating the UI. 
                _ = dispatcher.BeginInvoke((Action)(() =>
                {
                    // Display the image in the left pane.
                    Service.LeftImage.Source = e.Frame.Image.ToBitmapSource();

                    // If we're fusing client-side face detection with remote analysis, show the
                    // new frame now with the most recent analysis available. 
                    if (Service.FuseClientRemoteResults)
                    {
                        Service.RightImage.Source = Service.VisualizeResult(e.Frame);
                    }
                }));

            };

            // Set up a listener for when the client receives a new result from an API call. 
            Service.Grabber.NewResultAvailable += (s, e) =>
            {
                _ = dispatcher.BeginInvoke((Action)(() =>
                {
                    if (e.TimedOut)
                    {
                        Service.MessageArea.Text = "API call timed out.";
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
                        Service.MessageArea.Text = string.Format("{0} API call failed on frame {1}. Exception: {2}", apiName, e.Frame.Metadata.Index, message);
                    }
                    else
                    {
                        Service.LatestResultsToDisplay = e.Analysis;

                        // Display the image and visualization in the right pane. 
                        if (!Service.FuseClientRemoteResults)
                        {
                            Service.RightImage.Source = Service.VisualizeResult(e.Frame);
                        }
                    }
                }));
            };

            // Create local face detector. 
            _ = Service.LocalFaceDetector.Load(@"Data/haarcascade_frontalface_alt2.xml");



            return Service;
        }
    }
}
