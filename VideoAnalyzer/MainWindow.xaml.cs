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

using OpenCvSharp;
using OpenCvSharp.Extensions;
using OpenCvSharp.WpfExtensions;
using System.Net;
using Newtonsoft.Json.Linq;
using AzureCognitiveServices.Client;
using static AzureCognitiveServices.Client.CognitiveService;
using AzureCognitiveServices;

namespace VideoAnalyzer
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window, IDisposable
    {
        //values from Azure Service
        private const string groupId = "5f432198";
        private const string groupName = "Clients";
        private const string serviceKey = "5909943744314ecebf9170367f5b1a2a";
        private const string serviceEndpoint = @"https://offbeatface.cognitiveservices.azure.com/";

        private OpenFileDialog Files { get; set; }

        public CognitiveService Service { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            Service = CognitiveServiceBuilder.Create()
                            .HavingHttpConnection(config =>
                            {
                                config.WithServiceKey(serviceKey)
                                .EnsureSecureConnection()
                                .WithServiceEndpointUrl(serviceEndpoint);
                            }).Build(Dispatcher);
            //create containers in wpf for the left image "unprocessed images", right image "processed images", and a message area to view response messages
            //register them to the services following properties. 
            Service.MessageArea = MessageArea;
            Service.LeftImage = LeftImage;
            Service.RightImage = RightImage;

        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            
            await Service.InitializeGroup(groupId,groupName);
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
            MessageArea.Text = "";
            // How often to analyze. 
            await Service?.StartProcessing(TimeSpan.FromMilliseconds(slider_AnalysisInterval.Value), CameraList.SelectedIndex);
        }



        private async void stopButton_Click(object sender, RoutedEventArgs e)
        {
            await Service?.StopProcessing();
        }

        private void ModeList_Loaded(object sender, RoutedEventArgs e)
        {
            var modes = (AppMode[])Enum.GetValues(typeof(AppMode));
            var comboBox = sender as ComboBox;
            comboBox.ItemsSource = modes.Select(m => m.ToString());
            comboBox.SelectedIndex = 0;
        }

        private void ModeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var modes = (AppMode[])Enum.GetValues(typeof(AppMode));
            Service.SetAppMode(modes[comboBox.SelectedIndex]);
            checkBox.IsChecked = Service.FuseClientRemoteResults;
        }

        private void CameraList_Loaded(object sender, RoutedEventArgs e)
        {
            int numCameras = Service.GetNumberOfAvailableCameras();
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
            await Service?.AddPersonToGroup(groupId, person.Name, person.Images);
            AddPersonPanel.Visibility = Visibility.Collapsed;
        }

        private void checkBox_SelectionChange(object sender, RoutedEventArgs e)
        {
            Service?.FuseRemoteResultsSetting(checkBox.IsChecked.Value);
        }

        private void slider_AnalysisInterval_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Service?.TriggerAnalysisOnInterval(TimeSpan.FromMilliseconds(slider_AnalysisInterval.Value));
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
                    Service?.Dispose();
                }

                disposedValue = true;
            }
        }

    }
}
