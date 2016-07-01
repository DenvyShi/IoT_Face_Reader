using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Threading;
using System.Drawing;
using System.Net;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Face.Contract;
using System.Net.Http;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace IoT_Face_Reader
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // TAY's Face A free key from MSFT Cog Svcs.
        private readonly IFaceServiceClient faceServiceClient = new FaceServiceClient("9725d03742394560be3ff295e1e435a2");

        private MediaCapture mediaCapture;
        private StorageFile photoFile;
        private readonly string PHOTO_FILE_NAME = "photo.jpg";
        private bool isPreviewing;

        #region HELPER_FUNCTIONS

        enum Action
        {
            ENABLE,
            DISABLE
        }

        /// <summary>
        /// Helper function to enable or disable Initialization buttons
        /// </summary>
        /// <param name="action">enum Action</param>
        private void SetInitButtonVisibility(Action action)
        {
            if (action == Action.ENABLE)
            {
                video_init.IsEnabled = true;
                // audio_init.IsEnabled = true;
            }
            else
            {
                video_init.IsEnabled = false;
                // audio_init.IsEnabled = false;
            }
        }

        /// <summary>
        /// Helper function to enable or disable video related buttons (TakePhoto, Start Video Record)
        /// </summary>
        /// <param name="action">enum Action</param>
        private void SetVideoButtonVisibility(Action action)
        {
            if (action == Action.ENABLE)
            {
                takePhoto.IsEnabled = true;
                takePhoto.Visibility = Visibility.Visible;

                // recordVideo.IsEnabled = true;
                // recordVideo.Visibility = Visibility.Visible;
            }
            else
            {
                takePhoto.IsEnabled = false;
                takePhoto.Visibility = Visibility.Collapsed;

                // recordVideo.IsEnabled = false;
                // recordVideo.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        public MainPage()
        {
            this.InitializeComponent();
            SynchronizationContext syncContext = SynchronizationContext.Current;

            SetInitButtonVisibility(Action.ENABLE);
            SetVideoButtonVisibility(Action.DISABLE);
            // SetAudioButtonVisibility(Action.DISABLE);

            // isRecording = false;
            isPreviewing = false;
        }


        private async void Cleanup()
        {
            if (mediaCapture != null)
            {
                // Cleanup MediaCapture object
                if (isPreviewing)
                {
                    await mediaCapture.StopPreviewAsync();
                    captureImage.Source = null;
                    // playbackElement.Source = null;
                    isPreviewing = false;
                }
                if (1 == 2) // kludge I know
                {
                    await mediaCapture.StopRecordAsync();
                    //isRecording = false;
                    //recordVideo.Content = "Start Video Record";
                    //recordAudio.Content = "Start Audio Record";
                }
                mediaCapture.Dispose();
                mediaCapture = null;
            }
            SetInitButtonVisibility(Action.ENABLE);
        }


        public async void initVideo_Click(object sender, RoutedEventArgs e)
        {
            // Disable all buttons until initialization completes

            SetInitButtonVisibility(Action.DISABLE);
            SetVideoButtonVisibility(Action.DISABLE);
            // SetAudioButtonVisibility(Action.DISABLE);

            try
            {
                // still working on NMEA
                // updateNMEA();

                if (mediaCapture != null)
                {
                    // Cleanup MediaCapture object
                    if (isPreviewing)
                    {
                        await mediaCapture.StopPreviewAsync();
                        captureImage.Source = null;
                        // playbackElement.Source = null;
                        isPreviewing = false;
                    }
                    //if (isRecording)
                    // {
                    //     await mediaCapture.StopRecordAsync();
                    //    isRecording = false;
                    //    recordVideo.Content = "Start Video Record";
                    //    recordAudio.Content = "Start Audio Record";
                    //}
                    mediaCapture.Dispose();
                    mediaCapture = null;
                }

                statusBox.Text = "Initializing camera...";
                // Use default initialization
                mediaCapture = new MediaCapture();
                await mediaCapture.InitializeAsync();

                // Set callbacks for failure and recording limit exceeded
                statusBox.Text = "Device successfully initialized for video recording!";
                mediaCapture.Failed += new MediaCaptureFailedEventHandler(mediaCapture_Failed);
                // mediaCapture.RecordLimitationExceeded += new Windows.Media.Capture.RecordLimitationExceededEventHandler(mediaCapture_RecordLimitExceeded);

                // Start Preview                
                previewElement.Source = mediaCapture;
                await mediaCapture.StartPreviewAsync();
                isPreviewing = true;
                statusBox.Text = "Camera preview succeeded";

                // Enable buttons for video and photo capture
                SetVideoButtonVisibility(Action.ENABLE);

                // Enable Audio Only Init button, leave the video init button disabled
                // audio_init.IsEnabled = true;
            }
            catch (Exception ex)
            {
                statusBox.Text = "Unable to initialize camera for audio/video mode: " + ex.Message;
            }
        }

        private void cleanup_Click(object sender, RoutedEventArgs e)
        {
            SetInitButtonVisibility(Action.DISABLE);
            SetVideoButtonVisibility(Action.DISABLE);
            // SetAudioButtonVisibility(Action.DISABLE);
            Cleanup();
        }

        /// <summary>
        /// 'Take Photo' button click action function
        /// Capture image to a file in the default account photos folderr
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public async void takePhoto_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                takePhoto.IsEnabled = false;
                // recordVideo.IsEnabled = false;
                captureImage.Source = null;

                photoFile = await KnownFolders.PicturesLibrary.CreateFileAsync(
                    PHOTO_FILE_NAME, CreationCollisionOption.GenerateUniqueName);
                ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();
                await mediaCapture.CapturePhotoToStorageFileAsync(imageProperties, photoFile);
                takePhoto.IsEnabled = true;
                statusBox.Text = "Take Photo succeeded: " + photoFile.Path;

                IRandomAccessStream photoStream = await photoFile.OpenReadAsync();
                BitmapImage bitmap = new BitmapImage();
                bitmap.SetSource(photoStream);
                captureImage.Source = bitmap;

                // and now for the face API call
                // FaceAttributes attribs = await UploadAndDetectFaces(photoFile.Path);
                statusBox.Text = "00 ok just about to upload face";
                // Emotion A key: c9306a1f134749759f1f4f9ae8838e1a

                //var photoStreamNonrandom = new StreamReader(photoStream.AsStream());
                Stream stream = photoStream.AsStream();
                Stream fs1 = await photoFile.OpenStreamForReadAsync();
                Stream fs2 = await photoFile.OpenStreamForReadAsync();

                String faceURL = "http://i234.photobucket.com/albums/ee136/suwarnaadi/hair/Jason-Bateman-formal-hairstyle.jpg";

                var faceClient = new FaceServiceClient("9725d03742394560be3ff295e1e435a2");
                var emotionClient = new EmotionServiceClient("c9306a1f134749759f1f4f9ae8838e1a");
                var result = await faceClient.DetectAsync(fs1);
                var emotionResult = await emotionClient.RecognizeAsync(fs2);

                // the above result returns a Face[], so...
                ageBox.Text = result[0].FaceAttributes.Age.ToString();
                genderBox.Text = result[0].FaceAttributes.Gender.ToString();
                smileBox.Text = result[0].FaceAttributes.Smile.ToString();
                // facialHairBox.Text = result[0].FaceAttributes.FacialHair.ToString();
                glassesBox.Text = result[0].FaceAttributes.Glasses.ToString();

                angerBox.Text = emotionResult[0].Scores.Anger.ToString();
                contemptBox.Text = emotionResult[0].Scores.Contempt.ToString();
                disgustBox.Text = emotionResult[0].Scores.Disgust.ToString();
                fearBox.Text = emotionResult[0].Scores.Fear.ToString();
                happinessBox.Text = emotionResult[0].Scores.Happiness.ToString();
                neutralBox.Text = emotionResult[0].Scores.Neutral.ToString();
                sadnessBox.Text = emotionResult[0].Scores.Sadness.ToString();
                surpriseBox.Text = emotionResult[0].Scores.Surprise.ToString();

                


            }
            catch (Exception ex)
            {
                statusBox.Text = "foo: " + ex.Message;
                Cleanup();
            }
            finally
            {
                takePhoto.IsEnabled = true;
                // recordVideo.IsEnabled = true;
            }

        }


        public async void updateNMEA() // note that this doesn't work properly yet
        {
            //Create an HTTP client object
            Windows.Web.Http.HttpClient httpClient = new Windows.Web.Http.HttpClient();

            //Add a user-agent header to the GET request. 
            var headers = httpClient.DefaultRequestHeaders;

            //The safe way to add a header value is to use the TryParseAdd method and verify the return value is true,
            //especially if the header value is coming from user input.
            string header = "ie";
            if (!headers.UserAgent.TryParseAdd(header))
            {
                throw new Exception("Invalid header value: " + header);
            }

            header = "Mozilla/5.0 (compatible; MSIE 10.0; Windows NT 6.2; WOW64; Trident/6.0)";
            if (!headers.UserAgent.TryParseAdd(header))
            {
                throw new Exception("Invalid header value: " + header);
            }

            Uri requestUri = new Uri("http://192.168.0.100:50000/");

            //Send the GET request asynchronously and retrieve the response as a string.
            Windows.Web.Http.HttpResponseMessage httpResponse = new Windows.Web.Http.HttpResponseMessage();
            string httpResponseBody = "";

            try
            {
                //Send the GET request
            System.Threading.CancellationTokenSource source = new System.Threading.CancellationTokenSource(2000);
            // client.GetAsync(new Uri("http://example.com")).AsTask(source.Token);
            httpResponse = await httpClient.GetAsync(requestUri, Windows.Web.Http.HttpCompletionOption.ResponseHeadersRead).AsTask(source.Token);
            httpResponse.EnsureSuccessStatusCode();
            httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
            statusBox2.Text = httpResponseBody;
            }
            catch (TaskCanceledException exx)
            {
                statusBox2.Text = "foool: " + exx.ToString();
            }
            catch (Exception ex)
            {
                httpResponseBody = "Error: " + ex.HResult.ToString("X") + " Message: " + ex.Message;
            }
        }




        /// <summary>
        /// Callback function for any failures in MediaCapture operations
        /// </summary>
        /// <param name="currentCaptureObject"></param>
        /// <param name="currentFailure"></param>
        private async void mediaCapture_Failed(MediaCapture currentCaptureObject, MediaCaptureFailedEventArgs currentFailure)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    statusBox.Text = "MediaCaptureFailed: " + currentFailure.Message;

                   // if (isRecording)
                   // {
                    //    await mediaCapture.StopRecordAsync();
                   //     status.Text += "\n Recording Stopped";
                   // }
                }
                catch (Exception)
                {
                }
                finally
                {
                    SetInitButtonVisibility(Action.DISABLE);
                    SetVideoButtonVisibility(Action.DISABLE);
                   // SetAudioButtonVisibility(Action.DISABLE);
                    statusBox.Text += "\nCheck if camera is diconnected. Try re-launching the app";
                }
            });
        }



        
    }
}
