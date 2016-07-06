using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Face.Contract;

// Win10 IoT project: face/emotion recognition using a webcam & msft APIs
// some code copied from msft sample code projects (e.g., webcam & facial recogn. doorbell)
// coming soon: GPS support & cloud connectivity
// github.com/yostx038


namespace IoT_Face_Reader
{

    public sealed partial class MainPage : Page
    {

        private MediaCapture mediaCapture;
        private StorageFile photoFile;
        private readonly string PHOTO_FILE_NAME = "photo.jpg";
        private bool isPreviewing;

        public Face[] faceResult;
        public Microsoft.ProjectOxford.Emotion.Contract.Emotion[] emotionResult;



        // int to keep track of which face to look at
        public int numFaces = 0;
        public int currentFace = 0;

        public int imageWidth = 640;
        public int imageHeight = 480;

        public WriteableBitmap writeableBitmap;


        #region HELPER_FUNCTIONS

        enum Action
        {
            ENABLE,
            DISABLE
        }


        private void SetInitButtonVisibility(Action action)
        {
            if (action == Action.ENABLE)
            {
                video_init.IsEnabled = true;
            }
            else
            {
                video_init.IsEnabled = false;
            }
        }


        private void SetVideoButtonVisibility(Action action)
        {
            if (action == Action.ENABLE)
            {
                takePhoto.IsEnabled = true;
                takePhoto.Visibility = Visibility.Visible;
            }
            else
            {
                takePhoto.IsEnabled = false;
                takePhoto.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        public MainPage()
        {
            this.InitializeComponent();
            SynchronizationContext syncContext = SynchronizationContext.Current;

            SetInitButtonVisibility(Action.ENABLE);
            isPreviewing = false;

            writeableBitmap = new WriteableBitmap(imageWidth, imageHeight);
            captureImage.Source = writeableBitmap;

        }

        // initialize webcam, set up live preview
        public async void initVideo_Click(object sender, RoutedEventArgs e)
        {

            SetInitButtonVisibility(Action.DISABLE);

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
                        isPreviewing = false;
                    }
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

                // Start Preview                
                previewElement.Source = mediaCapture;
                await mediaCapture.StartPreviewAsync();
                isPreviewing = true;
                statusBox.Text = "Camera preview succeeded";

            }
            catch (Exception ex)
            {
                statusBox.Text = "Unable to initialize camera for audio/video mode: " + ex.Message;
            }
        }

        // method to take a still image, send to APIs, and display result
        public async void takePhoto_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                takePhoto.IsEnabled = false;

                photoFile = await KnownFolders.PicturesLibrary.CreateFileAsync(
                    PHOTO_FILE_NAME, CreationCollisionOption.GenerateUniqueName);
                ImageEncodingProperties imageProperties = ImageEncodingProperties.CreateJpeg();
                await mediaCapture.CapturePhotoToStorageFileAsync(imageProperties, photoFile);
                takePhoto.IsEnabled = true;
                statusBox.Text = "Take Photo succeeded: " + photoFile.Path;

                IRandomAccessStream photoStream = await photoFile.OpenReadAsync();
                IRandomAccessStream photoStream2 = await photoFile.OpenReadAsync();
                BitmapImage bitmap = new BitmapImage();
                bitmap.SetSource(photoStream);
                await writeableBitmap.SetSourceAsync(photoStream2);

                // and now for the face API call
                statusBox.Text = "Uploading image for Face API";

                Stream fs1 = await photoFile.OpenStreamForReadAsync();
                Stream fs2 = await photoFile.OpenStreamForReadAsync();

                var faceClient = new FaceServiceClient("9725d03742394560be3ff295e1e435a2");
                var emotionClient = new EmotionServiceClient("c9306a1f134749759f1f4f9ae8838e1a");
                faceResult = await faceClient.DetectAsync(fs1);
                emotionResult = await emotionClient.RecognizeAsync(fs2);

                numFaces = faceResult.Length;

                statusBox.Text = "Number of faces detected: " + numFaces.ToString();
                currentFace = 0;

                if (numFaces > 0) // if faces were returned in the result, display the first one
                {
                    displayFaceInfo();
                    displayImage();
                }

            }
            catch (Exception ex)
            {
                statusBox.Text = ex.Message;
                Cleanup();
            }
            finally
            {
                takePhoto.IsEnabled = true;
            }

        }

        // method to draw marker on the face associated with currently displayed attributes
        public async void displayImage()
        {
            // displays the image

            var width = faceResult[currentFace].FaceRectangle.Width;
            var height = faceResult[currentFace].FaceRectangle.Height;
            var left = faceResult[currentFace].FaceRectangle.Left;
            var top = faceResult[currentFace].FaceRectangle.Top;


            using (IRandomAccessStream fileStream = await photoFile.OpenAsync(Windows.Storage.FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(fileStream);

                // Scale image to appropriate size
                BitmapTransform transform = new BitmapTransform()
                {
                    ScaledWidth = Convert.ToUInt32(writeableBitmap.PixelWidth),
                    ScaledHeight = Convert.ToUInt32(writeableBitmap.PixelHeight)
                };

                PixelDataProvider pixelData = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,    // WriteableBitmap uses BGRA format
                    BitmapAlphaMode.Straight,
                    transform,
                    ExifOrientationMode.IgnoreExifOrientation, // This sample ignores Exif orientation
                    ColorManagementMode.DoNotColorManage);

                byte[] sourcePixels = pixelData.DetachPixelData();

                long coord = 0;

                for (int x = (left * 4); x < ((left * 4) + 40); x+=4)
                {
                    for (int y = (top * 4); y < ((top * 4) + 40); y+=4)
                    {
                        coord = x  + (y * Convert.ToUInt32(writeableBitmap.PixelWidth));
                        sourcePixels[coord + 0] = 0;
                        sourcePixels[coord + 1] = 0;
                        sourcePixels[coord + 2] = 255;
                        sourcePixels[coord + 3] = 255;
                    }
                }

                // Open a stream to copy the image contents to the WriteableBitmap's pixel buffer
                using (Stream stream = writeableBitmap.PixelBuffer.AsStream())
                {
                    await stream.WriteAsync(sourcePixels, 0, sourcePixels.Length);
                }
            }

            // Redraw the WriteableBitmap
            writeableBitmap.Invalidate();

        }

        // method to populate face/emotion attribute fields
        public async void displayFaceInfo()
        {
            // first draw a box on the face we're looking at
            ageBox.Text = faceResult[currentFace].FaceAttributes.Age.ToString("F3");
            genderBox.Text = faceResult[currentFace].FaceAttributes.Gender.ToString();
            smileBox.Text = faceResult[currentFace].FaceAttributes.Smile.ToString("F3");
            glassesBox.Text = faceResult[currentFace].FaceAttributes.Glasses.ToString();

            // then, the affective stuff
            angerBox.Text = emotionResult[currentFace].Scores.Anger.ToString("F3");
            contemptBox.Text = emotionResult[currentFace].Scores.Contempt.ToString("F3");
            disgustBox.Text = emotionResult[currentFace].Scores.Disgust.ToString("F3");
            fearBox.Text = emotionResult[currentFace].Scores.Fear.ToString("F3");
            happinessBox.Text = emotionResult[currentFace].Scores.Happiness.ToString("F3");
            neutralBox.Text = emotionResult[currentFace].Scores.Neutral.ToString("F3");
            sadnessBox.Text = emotionResult[currentFace].Scores.Sadness.ToString("F3");
            surpriseBox.Text = emotionResult[currentFace].Scores.Surprise.ToString("F3");

            //last, update counter
            faceNumBox.Text = currentFace.ToString();
        }

        // method to step through multiple faces in image
        public async void incrementFace_Click(object sender, RoutedEventArgs e)
        {
            if (numFaces <= 1) // only one face
            {
                statusBox.Text = "only one face detected";
                return;
            }
            if (currentFace < (numFaces - 1)) // more than one face, currentFace is less than max
            {
                currentFace++;
                displayFaceInfo();
                displayImage();
            }
            if (currentFace == (numFaces - 1))
            {
                currentFace = 0;
                displayFaceInfo();
                displayImage();
            }
        }

        // webcam error handling
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

        // cleanup methods
        private async void Cleanup()
        {
            if (mediaCapture != null)
            {
                // Cleanup MediaCapture object
                if (isPreviewing)
                {
                    await mediaCapture.StopPreviewAsync();
                    captureImage.Source = null;
                    isPreviewing = false;
                }
                mediaCapture.Dispose();
                mediaCapture = null;
            }
            SetInitButtonVisibility(Action.ENABLE);
        }

        //cleanup methods
        private void cleanup_Click(object sender, RoutedEventArgs e)
        {
            SetInitButtonVisibility(Action.DISABLE);
            Cleanup();
        }


    }
}
