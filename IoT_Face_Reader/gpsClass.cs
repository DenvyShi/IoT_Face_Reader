using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;

public class gpsClass
{

    // serial port stuff
    private SerialDevice serialPort = null;
    DataWriter dataWriteObject = null;
    DataReader dataReaderObject = null;

    private ObservableCollection<DeviceInformation> listOfDevices;
    private CancellationTokenSource ReadCancellationTokenSource;


    public async void gpsInit()
	{
        // for serial init
        // this.InitializeComponent();
        // comPortInput.IsEnabled = true;
        // sendTextButton.IsEnabled = false;
        listOfDevices = new ObservableCollection<DeviceInformation>();
        // ListAvailablePorts();
        // ConnectDevices.SelectedIndex = -1;
    }

    private async void Listen()
    {
        try
        {
            if (serialPort != null)
            {
                dataReaderObject = new DataReader(serialPort.InputStream);

                // keep reading the serial input
                //while (true)
                //{
                await ReadAsync(ReadCancellationTokenSource.Token);
                //}
            }
        }
        catch (Exception ex)
        {
            if (ex.GetType().Name == "TaskCanceledException")
            {
                statusBox.Text = "Reading task was cancelled, closing device and cleaning up";
                CloseDevice();
            }
            else
            {
                statusBox.Text = ex.Message;
            }
        }
        finally
        {
            // Cleanup once complete
            if (dataReaderObject != null)
            {
                dataReaderObject.DetachStream();
                dataReaderObject = null;
            }
        }
    }

    // close gps port
    private void CloseDevice()
    {
        if (ReadCancellationTokenSource != null)
        {
            if (!ReadCancellationTokenSource.IsCancellationRequested)
            {
                ReadCancellationTokenSource.Cancel();
            }
        }
        //
        // from orig
        if (serialPort != null)
        {
            serialPort.Dispose();
        }
        serialPort = null;

        comPortInput.IsEnabled = true;
        // sendTextButton.IsEnabled = false;
        // rcvdText.Text = "";
        listOfDevices.Clear();
    }

    // read gps data async
    private async Task ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            Task<UInt32> loadAsyncTask;

            uint ReadBufferLength = 2048;
            string gpsSentencePrefix = "blah";
            string gpsSentenceTemp = "STUFF, things, carp, blah, blah, blah, balh, balh, thibngs";
            string[] gpsSentence = gpsSentenceTemp.Split(',');

            // $GPGGA,123519,4807.038,N,01131.000,E,1,08,0.9,545.4,M,46.9,M,,*47

            // If task cancellation was requested, comply
            cancellationToken.ThrowIfCancellationRequested();

            // Set InputStreamOptions to complete the asynchronous read operation when one or more bytes is available
            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;

            // Create a task object to wait for data on the serialPort.InputStream
            loadAsyncTask = dataReaderObject.LoadAsync(ReadBufferLength).AsTask(cancellationToken);

            // Launch the task and wait
            UInt32 bytesRead = await loadAsyncTask;

            StringReader strReader = new StringReader(dataReaderObject.ReadString(bytesRead));

            //statusBox.Text = strReader.ReadLine() + "~~~~~" + strReader.ReadLine() + "zzzzzzzzz" + strReader.ReadLine();

            while (gpsSentencePrefix != "$GPGGA")
            {
                //statusBox.Text = "rxd: " +  dataReaderObject.ReadString(bytesRead);
                // 1 = time, 2 = lat, 4 = lon, 6=fixQuality, 7=n_sats
                gpsSentenceTemp = strReader.ReadLine();
                gpsSentence = gpsSentenceTemp.Split(',');
                gpsSentencePrefix = gpsSentence[0];
                if (gpsSentencePrefix == "$GPGGA")
                {
                    statusBox.Text = "lat = " + gpsSentence[2] + ", lon = " + gpsSentence[4] + ",  fix = " + gpsSentence[6] +
                        ", sats = " + gpsSentence[7] + "\n" + gpsSentenceTemp;
                }

            }
        }
        catch (Exception ex)
        {
            statusBox.Text = ex.Message;
        }


        CloseDevice();
    }


    private async void ListAvailablePorts()
    {
        try
        {
            string aqs = SerialDevice.GetDeviceSelector();
            var dis = await DeviceInformation.FindAllAsync(aqs);
            listOfDevices.Add(dis[0]);

            //statusBox.Text = "Select a device and connect";

            //for (int i = 0; i < dis.Count; i++)
            //{
            //    listOfDevices.Add(dis[i]);
            //}

            // DeviceListSource.Source = listOfDevices;
            comPortInput.IsEnabled = true;
            //ConnectDevices.SelectedIndex = -1;
        }
        catch (Exception ex)
        {
            statusBox.Text = ex.Message;
        }
    }


    private async void comPortInput_Click(object sender, RoutedEventArgs e)
    {
        ListAvailablePorts();
        // var selection = ConnectDevices.SelectedItems;
        var selection = listOfDevices;

        //if (selection.Count <= 0)
        // {
        //   statusBox.Text = "Select a device and connect";
        //    return;
        //}

        DeviceInformation entry = (DeviceInformation)selection[0];

        try
        {
            serialPort = await SerialDevice.FromIdAsync(entry.Id);

            // Disable the 'Connect' button 
            comPortInput.IsEnabled = false;

            // Configure serial settings
            serialPort.WriteTimeout = TimeSpan.FromMilliseconds(1000);
            serialPort.ReadTimeout = TimeSpan.FromMilliseconds(1000);
            serialPort.BaudRate = 9600;
            serialPort.Parity = SerialParity.None;
            serialPort.StopBits = SerialStopBitCount.One;
            serialPort.DataBits = 8;
            serialPort.Handshake = SerialHandshake.None;

            // Display configured settings
            statusBox.Text = "Serial port configured successfully: ";
            statusBox.Text += serialPort.BaudRate + "-";
            statusBox.Text += serialPort.DataBits + "-";
            statusBox.Text += serialPort.Parity.ToString() + "-";
            statusBox.Text += serialPort.StopBits;

            // Set the RcvdText field to invoke the TextChanged callback
            // The callback launches an async Read task to wait for data
            statusBox.Text = "Waiting for data...";

            // Create cancellation token object to close I/O operations when closing the device
            ReadCancellationTokenSource = new CancellationTokenSource();

            // Enable 'WRITE' button to allow sending data
            // sendTextButton.IsEnabled = true;

            Listen();
        }
        catch (Exception ex)
        {
            statusBox.Text = ex.Message;
            comPortInput.IsEnabled = true;
            // sendTextButton.IsEnabled = false;
        }
    }


}
