using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.Devices;
using Windows.Media.MediaProperties;
using Windows.Phone.UI.Input;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using BarcodeLib;
using BarcodeScanner;
using System.Runtime.InteropServices;
using Windows.UI.Popups;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Devices.Geolocation;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System.Profile;
using Windows.Security.Cryptography.Core;
using Windows.Security.Cryptography;
using System.Text;
using Windows.Storage.Streams;
using System.Net;
using System.IO;
using MWBOverlay;
using System.Collections.ObjectModel;
using ManateeShoppingCart;
using Newtonsoft.Json;


namespace MWBCamera
{
    [ComImport]
    [System.Runtime.InteropServices.Guid("5b0d3235-4dba-4d44-865e-8f1d0e4fd04d")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }


    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ScannerPageNative : Page
    {
        private bool USE_MWPARSER = true;
        private int MWPARSER_MASK = Scanner.MWP_PARSER_MASK_NONE;// Parser.MWP_PARSER_MASK_GS1;
        private bool USE_ANALYTICS = false;
        private string apiKey;
        private string apiUser;

        public enum OverlayMode
        {
            OM_IMAGE, OM_MWOVERLAY, OM_NONE
        }
        Image imgOverlay;

        // Receive notifications about rotation of the device and UI and apply any necessary rotation to the preview stream and UI controls       
        private DisplayInformation _displayInformation = DisplayInformation.GetForCurrentView();
        private readonly SimpleOrientationSensor _orientationSensor = SimpleOrientationSensor.GetDefault();
        private SimpleOrientation _deviceOrientation = SimpleOrientation.NotRotated;
        private DisplayOrientations _displayOrientation = DisplayOrientations.Portrait;

        // Rotation metadata to apply to the preview stream and recorded videos (MF_MT_VIDEO_ROTATION)
        // Reference: http://msdn.microsoft.com/en-us/library/windows/apps/xaml/hh868174.aspx
        private static readonly Guid RotationKey = new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1");

        // Prevent the screen from sleeping while the camera is running
        private readonly DisplayRequest _displayRequest = new DisplayRequest();

        // For listening to media property changes
        private readonly SystemMediaTransportControls _systemMediaControls = SystemMediaTransportControls.GetForCurrentView();

        // MediaCapture and its state variables
        private MediaCapture _mediaCapture;
        private bool _isInitialized;
        private bool _isPreviewing;
        private bool _isRecording;

        // Information about the camera device
        private bool _mirroringPreview;
        private bool _externalCamera;

        // UI stat variable: whether the user has chosen a single control to manipulate, or showing buttons for all controls
        private bool _singleControlMode;

        // A flag that signals when the UI controls (especially sliders) are being set up, to prevent them from triggering callbacks and making API calls
        private bool _settingUpUi;


        private int maxThreads;
        private int thredsCounter = 0;
        private Object LOCK = new Object();
        private Object LOCKCounter = new Object();
        private static Windows.Foundation.Metadata.Platform platform;
        private DispatcherTimer focusTimer;
        public static Boolean resultDisplayed = false;

        //Desktop dataStructures
        private DeviceInformationCollection m_devInfoCollection;
        private List<Dictionary<Object, Object>> m_resolutionsCollection;
        private float cameraAspectRatio = 1;
        private float globalHeight = 0;
        private float globalWidth = 0;

        private bool flashOnOff = false;
        private float zoomValue = 0;

        private Geolocator geo = new Geolocator();
        Geoposition pos;
        static string baseURL = "http://analytics.manateeworks.com/afk.gif";

        public static int overlayMode;

        ObservableCollection<ItemModel> listItems;
        int editListIndex;
        int editItemIndex = -1;

        #region Constructor, lifecycle and navigation

        public ScannerPageNative(ObservableCollection<ItemModel> _listItems, int _editListIndex, int _editItemIndex)
        {
            this.InitializeComponent();

            // Do not cache the state of the UI when navigating
            NavigationCacheMode = NavigationCacheMode.Disabled;
            overlayMode = (int)OverlayMode.OM_MWOVERLAY;
            maxThreads = Environment.ProcessorCount;
            // Do not cache the state of the UI when suspending/navigating
            NavigationCacheMode = NavigationCacheMode.Disabled;

            platform = DetectPlatform();

            if (USE_ANALYTICS)
            {
                initializeAnalyticsWithUsername("analytics.lazyvlad@gmail.com", "7ad0f9b39ed450218ca8e9d0f742f744d3b10137");
            }

            //focus timer
            focusTimer = new DispatcherTimer();
            focusTimer.Tick += onFocusTimer;
            focusTimer.Interval = TimeSpan.FromSeconds(4.0);

            var dispatcher = Windows.UI.Core.CoreWindow.GetForCurrentThread().Dispatcher;

            BarcodeHelper.initDecoder();

            // Useful to know when to initialize/clean up the camera
            this.Loaded += ScannerPageNative_Loaded;
            this.Unloaded += ScannerPageNative_Unloaded;

            listItems = _listItems;
            editListIndex = _editListIndex;
            editItemIndex = _editItemIndex;
        }

        private async void ScannerPageNative_Unloaded(object sender, RoutedEventArgs e)
        {
            Application.Current.Suspending -= Application_Suspending;
            Application.Current.Resuming -= Application_Resuming;

            await CleanupCameraAsync();

            await CleanupUiAsync();
        }

        private async void ScannerPageNative_Loaded(object sender, RoutedEventArgs e)
        {
            Application.Current.Suspending += Application_Suspending;
            Application.Current.Resuming += Application_Resuming;

            _displayInformation = DisplayInformation.GetForCurrentView();

            await SetupUiAsync();

            if (platform == Platform.Windows)
            {
                await EnumerateResolutions();
            }

            await InitializeCameraAsync();
        }

        private async void Application_Suspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();

            await CleanupCameraAsync();

            await CleanupUiAsync();

            deferral.Complete();
        }

        private async void Application_Resuming(object sender, object o)
        {
            await SetupUiAsync();

            await InitializeCameraAsync();
        }

        public async void getGpsPosition()
        {
            var accessStatus = await Geolocator.RequestAccessAsync();
            switch (accessStatus)
            {
                case GeolocationAccessStatus.Allowed:
                    try
                    {
                        // await this because we don't know hpw long it will take to complete and we don't want to block the UI
                        pos = await geo.GetGeopositionAsync();
                        geo.ReportInterval = 2000;

                        geo.PositionChanged += OnPositionChanged;

                    }
                    catch (Exception ee)
                    {
                        Debug.WriteLine(ee.Message);
                    }

                    //     UpdateLocationData(pos);

                    break;

                case GeolocationAccessStatus.Denied:
                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                     {
                         displayResultAsync("Location is turned off.\nYou can turn it on in Settings->Privacy", "Info");

                     });
                    break;

                case GeolocationAccessStatus.Unspecified:

                    break;
            }
        }

        async private void OnPositionChanged(Geolocator sender, PositionChangedEventArgs e)
        {

            pos = await geo.GetGeopositionAsync();

            /* await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
             {

             }); */
        }

        private void initializeAnalyticsWithUsername(string apiuser, string apikey)
        {
            getGpsPosition();

            apiUser = apiuser;
            apiKey = apikey;
        }

        #endregion Constructor, lifecycle and navigation


        #region Event handlers

        /// <summary>
        /// In the event of the app being minimized this method handles media property change events. If the app receives a mute
        /// notification, it is no longer in the foregroud.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void SystemMediaControls_PropertyChanged(SystemMediaTransportControls sender, SystemMediaTransportControlsPropertyChangedEventArgs args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                // Only handle this event if this page is currently being displayed
                if (args.Property == SystemMediaTransportControlsProperty.SoundLevel)// && Frame.CurrentSourcePageType == typeof(ScannerPageNative))
                {
                    // Check to see if the app is being muted. If so, it is being minimized.
                    // Otherwise if it is not initialized, it is being brought into focus.
                    if (sender.SoundLevel == SoundLevel.Muted)
                    {
                        await CleanupCameraAsync();
                    }
                    else if (!_isInitialized)
                    {
                        await InitializeCameraAsync();
                    }
                }
            });
        }

        /// <summary>
        /// Occurs each time the simple orientation sensor reports a new sensor reading.
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="args">The event data.</param>
        private async void OrientationSensor_OrientationChanged(SimpleOrientationSensor sender, SimpleOrientationSensorOrientationChangedEventArgs args)
        {
            if (args.Orientation != SimpleOrientation.Faceup && args.Orientation != SimpleOrientation.Facedown)
            {
                // Only update the current orientation if the device is not parallel to the ground. This allows users to take pictures of documents (FaceUp)
                // or the ceiling (FaceDown) in portrait or landscape, by first holding the device in the desired orientation, and then pointing the camera
                // either up or down, at the desired subject.
                //Note: This assumes that the camera is either facing the same way as the screen, or the opposite way. For devices with cameras mounted
                //      on other panels, this logic should be adjusted.
                _deviceOrientation = args.Orientation;
            }
        }

        /// <summary>
        /// This event will fire when the page is rotated
        /// </summary>
        /// <param name="sender">The event source.</param>
        /// <param name="args">The event data.</param>
        private async void DisplayInformation_OrientationChanged(DisplayInformation sender, object args)
        {
            _displayOrientation = sender.CurrentOrientation;

            if (_isPreviewing)
            {
                await SetPreviewRotationAsync();
            }

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateButtonOrientation());
        }

        private async void HardwareButtons_CameraPressed(object sender, CameraEventArgs e)
        {

        }

        private void PreviewControl_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            // This event handler implements pinch-to-zoom, which should only happen if preview is running
            if (!_isPreviewing) return;

            // Pinch gestures are a delta, so apply them to the current zoom value
            //  var zoomFactor = ZoomSlider.Value * e.Delta.Scale;

            // Set the value back on the slider, which will make the call to the ZoomControl
            //  ZoomSlider.Value = zoomFactor;
        }

        private async void MediaCapture_RecordLimitationExceeded(MediaCapture sender)
        {
            // This is a notification that recording has to stop, and the app is expected to finalize the recording

            await StopRecordingAsync();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateCaptureControls());
        }

        private async void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            Debug.WriteLine("MediaCapture_Failed: (0x{0:X}) {1}", errorEventArgs.Code, errorEventArgs.Message);

            await CleanupCameraAsync();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateCaptureControls());
        }

        #endregion Event handlers


        #region MediaCapture methods

        /// <summary>
        /// Initializes the MediaCapture, registers events, gets camera device information for mirroring and rotating, starts preview and unlocks the UI
        /// </summary>
        /// <returns></returns>
        private async Task InitializeCameraAsync()
        {
            Debug.WriteLine("InitializeCameraAsync");

            if (_mediaCapture == null)
            {
                // Attempt to get the back camera if one is available, but use any camera device if not
                var cameraDevice = await FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Back);

                if (cameraDevice == null)
                {
                    Debug.WriteLine("No camera device found!");
                    return;
                }

                // Create MediaCapture and its settings
                _mediaCapture = new MediaCapture();

                // Register for a notification when video recording has reached the maximum time and when something goes wrong
                _mediaCapture.RecordLimitationExceeded += MediaCapture_RecordLimitationExceeded;
                _mediaCapture.Failed += MediaCapture_Failed;

                var settings = new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevice.Id };

                // Initialize MediaCapture
                try
                {
                    await _mediaCapture.InitializeAsync(settings);
                    _isInitialized = true;
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.WriteLine("The app was denied access to the camera");
                }

                // If initialization succeeded, start the preview
                if (_isInitialized)
                {
                    // Figure out where the camera is located
                    if (cameraDevice.EnclosureLocation == null || cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Unknown)
                    {
                        // No information on the location of the camera, assume it's an external camera, not integrated on the device
                        _externalCamera = true;
                    }
                    else
                    {
                        // Camera is fixed on the device
                        _externalCamera = false;

                        // Only mirror the preview if the camera is on the front panel
                        _mirroringPreview = (cameraDevice.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);
                    }

                    if (platform == Platform.Windows)
                    {
                        VideoRotation rotation = ConvertDeviceOrientationToDegrees1(_displayOrientation);
                        _mediaCapture.SetPreviewRotation(rotation);
                    }

                    await StartPreviewAsync();

                    UpdateCaptureControls();
                    UpdateManualControlCapabilities();
                }
            }
        }

        /// <summary>
        /// Starts the preview and adjusts it for for rotation and mirroring after making a request to keep the screen on
        /// </summary>
        /// <returns></returns>
        private async Task StartPreviewAsync()
        {
            // Prevent the device from sleeping while the preview is running
            _displayRequest.RequestActive();

            if (_displayOrientation == DisplayOrientations.Portrait)
            {
                globalHeight = (float)PreviewControl.ActualHeight;
                globalWidth = (float)PreviewControl.ActualWidth;
            }
            else
            {
                globalWidth = (float)PreviewControl.ActualHeight;
                globalHeight = (float)PreviewControl.ActualWidth;
            }

            // Set the preview source in the UI and mirror it if necessary

            PreviewControl.Source = _mediaCapture;
            PreviewControl.FlowDirection = _mirroringPreview ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            // Start the preview

            _isPreviewing = true;
            focusTimer.Start();
            if (platform == Platform.WindowsPhone)
                await _mediaCapture.StartPreviewAsync();

            // Initialize the preview to the current orientation
            if (_isPreviewing)
            {
                await SetPreviewRotationAsync();
            }

            if (platform == Platform.Windows)
                await _mediaCapture.StartPreviewAsync();

            //addOverLay();
            StartRecordingAsync();
        }


        /// <summary>
        /// Gets the current orientation of the UI in relation to the device and applies a corrective rotation to the preview
        /// </summary>
        private async Task SetPreviewRotationAsync()
        {
            addOverLay();
            // Only need to update the orientation if the camera is mounted on the device
            if (_externalCamera)
                return;

            // Calculate which way and how far to rotate the preview
            int rotationDegrees = ConvertDisplayOrientationToDegrees(_displayOrientation);

            // The rotation direction needs to be inverted if the preview is being mirrored
            if (_mirroringPreview)
            {
                rotationDegrees = (360 - rotationDegrees) % 360;
            }

            try
            {
                // Add rotation metadata to the preview stream to make sure the aspect ratio / dimensions match when rendering and getting preview frames
                var props = _mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
                props.Properties.Add(RotationKey, rotationDegrees);
                await _mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, null);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }


        private async void addOverLay()
        {
            try
            {
                if (overlayMode == (int)OverlayMode.OM_MWOVERLAY)
                {
                    if (platform == Platform.WindowsPhone)
                    {
                        if (_displayOrientation == DisplayOrientations.Landscape || _displayOrientation == DisplayOrientations.LandscapeFlipped)
                        {
                            FocusCanvas.Height = globalWidth;
                            FocusCanvas.Width = globalHeight;

                            cameraAspectRatio = (float)globalWidth / (float)globalHeight;
                        }
                        else
                        {
                            FocusCanvas.Height = globalHeight;
                            FocusCanvas.Width = globalWidth;

                            cameraAspectRatio = (float)globalHeight / (float)globalWidth;
                        }
                    }
                    else
                    {
                        System.Collections.Generic.IReadOnlyList<IMediaEncodingProperties> res = _mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview);

                        Dictionary<Object, Object> item = m_resolutionsCollection[EnumResolutions.SelectedIndex];

                        int resIndex = (int)item["resIndex"];
                        await _mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, res[resIndex]);

                        FocusCanvas.Height = globalHeight;
                        FocusCanvas.Width = globalWidth;

                        if (_displayOrientation == DisplayOrientations.Landscape || _displayOrientation == DisplayOrientations.LandscapeFlipped)
                        {
                            cameraAspectRatio = ((float)((VideoEncodingProperties)res[resIndex]).Height) / ((VideoEncodingProperties)res[resIndex]).Width;
                        }
                        else
                        {
                            cameraAspectRatio = ((float)((VideoEncodingProperties)res[resIndex]).Width) / ((VideoEncodingProperties)res[resIndex]).Height;

                        }
                    }
                    MWOverlay.addOverlay(FocusCanvas, cameraAspectRatio);
                }
                else
                if (overlayMode == (int)OverlayMode.OM_IMAGE)
                {
                    if (imgOverlay != null)
                    {
                        FocusCanvas.Children.Remove(imgOverlay);
                    }

                    if (platform == Platform.WindowsPhone)
                    {
                        if (_displayOrientation == DisplayOrientations.Landscape || _displayOrientation == DisplayOrientations.LandscapeFlipped)
                        {
                            FocusCanvas.Height = globalWidth;
                            FocusCanvas.Width = globalHeight;

                            cameraAspectRatio = (float)globalWidth / (float)globalHeight;
                        }
                        else
                        {
                            FocusCanvas.Height = globalHeight;
                            FocusCanvas.Width = globalWidth;

                            cameraAspectRatio = (float)globalHeight / (float)globalWidth;
                        }
                    }

                    float width = (float)FocusCanvas.Width;
                    float height = (float)FocusCanvas.Height;

                    float heightTmp = (float)height;
                    float widthTmp = (float)width;

                    float heightClip = 0;
                    float widthClip = 0;

                    if (width * cameraAspectRatio >= height)
                    {
                        heightTmp = (int)(width * cameraAspectRatio);
                        heightClip = (float)(heightTmp - height) / 2;
                    }
                    else
                    {
                        widthTmp = (int)(height / cameraAspectRatio);
                        widthClip = (float)(widthTmp - width) / 2;
                    }

                    imgOverlay = new Image()
                    {
                        Stretch = Stretch.Fill,
                        Width = FocusCanvas.Width,
                        Height = FocusCanvas.Height,
                        Margin = new Thickness(widthClip, heightClip, 0, 0)
                    };

                    BitmapImage BitImg = new BitmapImage(new Uri(this.BaseUri, "ms-appx:///Assets/overlay_mw.png"));
                    imgOverlay.Source = BitImg;
                    //imgOverlay.Source = ImageFromRelativePath(this, "ms-appx:///Assets/overlay_mw.png"); //BitImg;
                    FocusCanvas.Children.Add(imgOverlay);
                }
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.Message);
            }
        }

        public static BitmapImage ImageFromRelativePath(FrameworkElement parent, string path)
        {
            var uri = new Uri(parent.BaseUri, path);
            //var uri = new Uri(path, UriKind.Absolute);
            BitmapImage bmp = new BitmapImage();
            bmp.UriSource = uri;
            return bmp;
        }
        /// <summary>
        /// Stops the preview and deactivates a display request, to allow the screen to go into power saving modes
        /// </summary>
        /// <returns></returns>
        private async Task StopPreviewAsync()
        {
            try
            {
                // Stop the preview
                _isPreviewing = false;
                await _mediaCapture.StopPreviewAsync();

                // Use the dispatcher because this method is sometimes called from non-UI threads
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    // Cleanup the UI
                    PreviewControl.Source = null;

                    // Allow the device screen to sleep now that the preview is stopped
                    _displayRequest.RequestRelease();
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// Records an MP4 video to a StorageFile and adds rotation metadata to it
        /// </summary>
        /// <returns></returns>
        private async Task StartRecordingAsync()
        {
            GetPreviewFrameAsSoftwareBitmapAsync();
        }

        /// <summary>
        /// Stops recording a video
        /// </summary>
        /// <returns></returns>
        private async Task StopRecordingAsync()
        {
            Debug.WriteLine("Stopping recording...");

            _isRecording = false;
            await _mediaCapture.StopRecordAsync();

            Debug.WriteLine("Stopped recording!");
        }

        /// <summary>
        /// Cleans up the camera resources (after stopping any video recording and/or preview if necessary) and unregisters from MediaCapture events
        /// </summary>
        /// <returns></returns>
        private async Task CleanupCameraAsync()
        {
            Debug.WriteLine("CleanupCameraAsync");

            if (_isInitialized)
            {
                // If a recording is in progress during cleanup, stop it to save the recording
                if (_isRecording)
                {
                    await StopRecordingAsync();
                }

                if (_isPreviewing)
                {
                    // The call to stop the preview is included here for completeness, but can be
                    // safely removed if a call to MediaCapture.Dispose() is being made later,
                    // as the preview will be automatically stopped at that point
                    await StopPreviewAsync();
                }

                _isInitialized = false;
            }

            if (_mediaCapture != null)
            {
                _mediaCapture.RecordLimitationExceeded -= MediaCapture_RecordLimitationExceeded;
                _mediaCapture.Failed -= MediaCapture_Failed;
                _mediaCapture.Dispose();
                _mediaCapture = null;
            }
        }

        #endregion MediaCapture methods


        #region Helper functions

        /// <summary>
        /// Attempts to lock the page orientation, hide the StatusBar (on Phone) and registers event handlers for hardware buttons and orientation sensors
        /// </summary>
        /// <returns></returns>
        private async Task SetupUiAsync()
        {
            // Hide the status bar
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                await Windows.UI.ViewManagement.StatusBar.GetForCurrentView().HideAsync();
            }

            // Populate orientation variables with the current state
            _displayOrientation = _displayInformation.CurrentOrientation;
            if (_orientationSensor != null)
            {
                _deviceOrientation = _orientationSensor.GetCurrentOrientation();
            }

            RegisterEventHandlers();
        }

        /// <summary>
        /// Unregisters event handlers for hardware buttons and orientation sensors, allows the StatusBar (on Phone) to show, and removes the page orientation lock
        /// </summary>
        /// <returns></returns>
        private async Task CleanupUiAsync()
        {
            UnregisterEventHandlers();

            // Show the status bar
            if (ApiInformation.IsTypePresent("Windows.UI.ViewManagement.StatusBar"))
            {
                await Windows.UI.ViewManagement.StatusBar.GetForCurrentView().ShowAsync();
            }
        }

        private async Task EnumerateWebcamsAsync()
        {
            try
            {
                //ShowStatusMessage("Enumerating Webcams...");
                m_devInfoCollection = null;

                EnumedDeviceList.Items.Clear();

                m_devInfoCollection = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                if (m_devInfoCollection.Count == 0)
                {
                    //  ShowStatusMessage("No WebCams found.");
                }
                else
                {
                    for (int i = 0; i < m_devInfoCollection.Count; i++)
                    {
                        var devInfo = m_devInfoCollection[i];
                        var location = devInfo.EnclosureLocation;

                        if (location != null)
                        {

                            if (location.Panel == Windows.Devices.Enumeration.Panel.Front)
                            {
                                EnumedDeviceList.Items.Add(devInfo.Name + "-Front");
                            }
                            else if (location.Panel == Windows.Devices.Enumeration.Panel.Back)
                            {
                                EnumedDeviceList.Items.Add(devInfo.Name + "-Back");
                            }
                            else
                            {
                                EnumedDeviceList.Items.Add(devInfo.Name);
                            }
                        }
                        else
                        {
                            EnumedDeviceList.Items.Add(devInfo.Name);
                        }
                    }
                    EnumedDeviceList.SelectedIndex = 0;
                    // ShowStatusMessage("Enumerating Webcams completed successfully.");
                    // EnumerateResolutions();

                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        private async Task EnumerateResolutions()
        {
            try
            {
                //ShowStatusMessage("Enumerating Resolutions...");
                await EnumerateWebcamsAsync();

                EnumResolutions.Items.Clear();
                m_resolutionsCollection = new List<Dictionary<object, object>>();

                // ShowStatusMessage("Starting device");

                _mediaCapture = new Windows.Media.Capture.MediaCapture();

                var settings = new Windows.Media.Capture.MediaCaptureInitializationSettings();
                var chosenDevInfo = m_devInfoCollection[EnumedDeviceList.SelectedIndex];
                settings.VideoDeviceId = chosenDevInfo.Id;

                if (chosenDevInfo.EnclosureLocation != null && chosenDevInfo.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Back)
                {
                    //   m_bRotateVideoOnOrientationChange = true;
                    //    m_bReversePreviewRotation = false;
                }
                else if (chosenDevInfo.EnclosureLocation != null && chosenDevInfo.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front)
                {
                    //  m_bRotateVideoOnOrientationChange = true;
                    //   m_bReversePreviewRotation = true;
                }
                else
                {
                    // m_bRotateVideoOnOrientationChange = false;
                }

                await _mediaCapture.InitializeAsync(settings);

                System.Collections.Generic.IReadOnlyList<IMediaEncodingProperties> res = _mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview);

                uint highestRes = 0;
                int highestResIndex = -1;

                for (int i = 0; i < res.Count; i++)
                {
                    VideoEncodingProperties vp = (VideoEncodingProperties)res[i];
                    string s = vp.Width.ToString();
                    s = s + " x " + vp.Height.ToString();
                    // s = s + " " + vp.Bitrate.ToString();
                    // s = s + " " + vp.Type.ToString();
                    //  s = s + " " + vp.Subtype.ToString();
                    s = s + " " + (vp.FrameRate.Numerator / vp.FrameRate.Denominator).ToString() + " FPS";
                    s = s + " " + vp.Subtype.ToString();

                    Debug.WriteLine(i.ToString() + ": " + s);

                    // Acceps only streams in YUV formats
                    if (vp.Subtype.Equals("YUY2") || vp.Subtype.Equals("UYVY") || vp.Subtype.Equals("NV12") || vp.Subtype.Equals("RGB24"))
                    {
                        bool alreadyPresent = false;
                        foreach (var item in m_resolutionsCollection)
                        {
                            String resName = (String)item["resName"];
                            if (resName.Equals(s))
                            {
                                alreadyPresent = true;
                                break;
                            }

                        }

                        if (!alreadyPresent)
                        {
                            Dictionary<Object, Object> item = new Dictionary<Object, Object>();
                            item.Add("resName", s);
                            item.Add("resIndex", i);
                            m_resolutionsCollection.Add(item);
                            EnumResolutions.Items.Add(s);
                        }

                        if (vp.Height * vp.Width > highestRes)
                        {
                            highestRes = vp.Height * vp.Width;
                            highestResIndex = m_resolutionsCollection.Count - 1;
                        }
                    }
                }

                _mediaCapture = null;

                if (highestResIndex >= 0)
                {
                    EnumResolutions.SelectedIndex = highestResIndex;
                    //ShowStatusMessage("Enumerating Resolutions completed successfully.");
                }
                else
                {
                    //ShowExceptionMessage(new Exception("No usable scanning resolution found on camera"));
                }
                // btnStartDevice.IsEnabled = true;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        private async void EnumedDeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isPreviewing)
            {
                await StopPreviewAsync();
                await Task.Delay(50);
                await StartPreviewAsync();
            }
        }

        /// <summary>
        /// This method will update the icons, enable/disable and show/hide the photo/video buttons depending on the current state of the app and the capabilities of the device
        /// </summary>
        private void UpdateCaptureControls()
        {
            // Depending on the preview, hide or show the controls grid which houses the individual control buttons and settings
            CameraControlsGrid.Visibility = _isPreviewing ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Registers event handlers for hardware buttons and orientation sensors, and performs an initial update of the UI rotation
        /// </summary>
        private void RegisterEventHandlers()
        {
            if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
            {
                HardwareButtons.CameraPressed += HardwareButtons_CameraPressed;
                HardwareButtons.BackPressed += HardwareButtons_BackPressed;
            }

            // If there is an orientation sensor present on the device, register for notifications
            if (_orientationSensor != null)
            {
                _orientationSensor.OrientationChanged += OrientationSensor_OrientationChanged;

                // Update orientation of buttons with the current orientation
                UpdateButtonOrientation();
            }

            _displayInformation.OrientationChanged += DisplayInformation_OrientationChanged;
            _systemMediaControls.PropertyChanged += SystemMediaControls_PropertyChanged;
        }

        private void HardwareButtons_BackPressed(object sender, BackPressedEventArgs e)
        {
            // Back button exits single control mode
            if (_singleControlMode)
            {
                // Exit single control mode
                _singleControlMode = false;

                // Hide the container control for manual input
                ManualControlsGrid.Visibility = Visibility.Collapsed;

                e.Handled = true;
            }
        }

        /// <summary>
        /// Unregisters event handlers for hardware buttons and orientation sensors
        /// </summary>
        private void UnregisterEventHandlers()
        {
            if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
            {
                HardwareButtons.CameraPressed -= HardwareButtons_CameraPressed;
                HardwareButtons.BackPressed -= HardwareButtons_BackPressed;
            }

            if (_orientationSensor != null)
            {
                _orientationSensor.OrientationChanged -= OrientationSensor_OrientationChanged;
            }

            _displayInformation.OrientationChanged -= DisplayInformation_OrientationChanged;
            _systemMediaControls.PropertyChanged -= SystemMediaControls_PropertyChanged;
        }

        /// <summary>
        /// Attempts to find and return a device mounted on the panel specified, and on failure to find one it will return the first device listed
        /// </summary>
        /// <param name="desiredPanel">The desired panel on which the returned device should be mounted, if available</param>
        /// <returns></returns>
        private static async Task<DeviceInformation> FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel desiredPanel)
        {
            // Get available devices for capturing pictures
            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Get the desired camera by panel
            DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

            // If there is no device mounted on the desired panel, return the first device found
            return desiredDevice ?? allVideoDevices.FirstOrDefault();
        }

        #endregion Helper functions


        #region Rotation helpers

        /// <summary>
        /// Calculates the current camera orientation from the device orientation by taking into account whether the camera is external or facing the user
        /// </summary>
        /// <returns>The camera orientation in space, with an inverted rotation in the case the camera is mounted on the device and is facing the user</returns>
        private SimpleOrientation GetCameraOrientation()
        {
            if (_externalCamera)
            {
                // Cameras that are not attached to the device do not rotate along with it, so apply no rotation
                return SimpleOrientation.NotRotated;
            }

            var result = _deviceOrientation;

            // Account for the fact that, on portrait-first devices, the camera sensor is mounted at a 90 degree offset to the native orientation
            if (_displayInformation.NativeOrientation == DisplayOrientations.Portrait)
            {
                switch (result)
                {
                    case SimpleOrientation.Rotated90DegreesCounterclockwise:
                        result = SimpleOrientation.NotRotated;
                        break;
                    case SimpleOrientation.Rotated180DegreesCounterclockwise:
                        result = SimpleOrientation.Rotated90DegreesCounterclockwise;
                        break;
                    case SimpleOrientation.Rotated270DegreesCounterclockwise:
                        result = SimpleOrientation.Rotated180DegreesCounterclockwise;
                        break;
                    case SimpleOrientation.NotRotated:
                        result = SimpleOrientation.Rotated270DegreesCounterclockwise;
                        break;
                }
            }

            // If the preview is being mirrored for a front-facing camera, then the rotation should be inverted
            if (_mirroringPreview)
            {
                // This only affects the 90 and 270 degree cases, because rotating 0 and 180 degrees is the same clockwise and counter-clockwise
                switch (result)
                {
                    case SimpleOrientation.Rotated90DegreesCounterclockwise:
                        return SimpleOrientation.Rotated270DegreesCounterclockwise;
                    case SimpleOrientation.Rotated270DegreesCounterclockwise:
                        return SimpleOrientation.Rotated90DegreesCounterclockwise;
                }
            }

            return result;
        }

        /// <summary>
        /// Converts the given orientation of the device in space to the corresponding rotation in degrees
        /// </summary>
        /// <param name="orientation">The orientation of the device in space</param>
        /// <returns>An orientation in degrees</returns>
        private static int ConvertDeviceOrientationToDegrees(SimpleOrientation orientation)
        {
            switch (orientation)
            {
                case SimpleOrientation.Rotated90DegreesCounterclockwise:
                    return 90;
                case SimpleOrientation.Rotated180DegreesCounterclockwise:
                    return 180;
                case SimpleOrientation.Rotated270DegreesCounterclockwise:
                    return 270;
                case SimpleOrientation.NotRotated:
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Converts the given orientation of the app on the screen to the corresponding rotation in degrees
        /// </summary>
        /// <param name="orientation">The orientation of the app on the screen</param>
        /// <returns>An orientation in degrees</returns>
        private static int ConvertDisplayOrientationToDegrees(DisplayOrientations orientation)
        {
            switch (orientation)
            {
                case DisplayOrientations.Portrait:
                    return 90;
                case DisplayOrientations.LandscapeFlipped:
                    return 180;
                case DisplayOrientations.PortraitFlipped:
                    return 270;
                case DisplayOrientations.Landscape:
                default:
                    return 0;
            }
        }

        private static VideoRotation ConvertDeviceOrientationToDegrees1(DisplayOrientations orientation)
        {
            switch (orientation)
            {
                case DisplayOrientations.Portrait:
                    return (VideoRotation)1;
                case DisplayOrientations.LandscapeFlipped:
                    return (VideoRotation)2;
                case DisplayOrientations.PortraitFlipped:
                    return (VideoRotation)3;
                case DisplayOrientations.Landscape:
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Uses the current device orientation in space and page orientation on the screen to calculate the rotation
        /// transformation to apply to the controls
        /// </summary>
        private void UpdateButtonOrientation()
        {
            int device = ConvertDeviceOrientationToDegrees(_deviceOrientation);
            int display = ConvertDisplayOrientationToDegrees(_displayOrientation);

            if (_displayInformation.NativeOrientation == DisplayOrientations.Portrait)
            {
                device -= 90;
            }

            // Combine both rotations and make sure that 0 <= result < 360
            var angle = (360 + display + device) % 360;

            // Rotate the buttons in the UI to match the rotation of the device
            var transform = new RotateTransform { Angle = angle };
        }

        #endregion Rotation helpers


        #region Manual controls setup

        private void ManualControlButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // Toggle single control mode
            _singleControlMode = !_singleControlMode;

            // Show the container control for manual configuration only when in single control mode
            ManualControlsGrid.Visibility = _singleControlMode ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Reflect the capabilities of the device in the UI, and set the initial state for each control
        /// </summary>
        private void UpdateManualControlCapabilities()
        {
            // Prevent the setup from triggering API calls
            _settingUpUi = true;

            // The implementation of these methods is in the partial classes named MainPage.Control.xaml.cs, where "Control" is the name of the control
            UpdateFlashControlCapabilities();
            UpdateZoomControlCapabilities();
            UpdateSettingsControlCapabilities();
            _settingUpUi = false;
        }


        private void UpdateSettingsControlCapabilities()
        {
            if (platform == Platform.Windows)
            {
                ControlButton.Tag = Visibility.Visible;
            }
            else
            {
                ControlButton.Tag = Visibility.Collapsed;
                ControlButton.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateFlashControlCapabilities()
        {
            var flashControl = _mediaCapture.VideoDeviceController.FlashControl;

            //  if (platform == Platform.WindowsPhone)
            {
                if (flashControl.Supported)
                {
                    FlashButton.Tag = Visibility.Visible;
                }
                else
                {
                    FlashButton.Visibility = Visibility.Collapsed;
                    FlashButton.Tag = Visibility.Collapsed;
                }
            }
        }

        private void UpdateZoomControlCapabilities()
        {
            var zoomControl = _mediaCapture.VideoDeviceController.ZoomControl;

            if (zoomControl.Supported)
            {
                ZoomButton.Tag = Visibility.Visible;
            }
            else
            {
                ZoomButton.Visibility = Visibility.Collapsed;
                ZoomButton.Tag = Visibility.Collapsed;
            }
        }

        private void ZoomSlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (_settingUpUi) return;

            //   SetZoomLevel((float)ZoomSlider.Value);
        }

        private void ZoomSlider_clicked(object sender, RoutedEventArgs e)
        {
            if (_settingUpUi) return;

            var zoomControl = _mediaCapture.VideoDeviceController.ZoomControl;

            zoomValue++;

            if (zoomValue == zoomControl.Max + 1)
                zoomValue = zoomControl.Min;

            SetZoomLevel(zoomValue);
        }

        private void SetZoomLevel(float level)
        {
            var zoomControl = _mediaCapture.VideoDeviceController.ZoomControl;

            // Make sure zoomFactor is within the valid range
            level = Math.Max(Math.Min(level, zoomControl.Max), zoomControl.Min);

            // Make sure zoomFactor is a multiple of Step, snap to the next lower step
            level -= (level % zoomControl.Step);

            var settings = new ZoomSettings { Value = level };

            if (zoomControl.SupportedModes.Contains(ZoomTransitionMode.Smooth))
            {
                // Favor smooth zoom for this sample
                settings.Mode = ZoomTransitionMode.Smooth;
            }
            else
            {
                settings.Mode = zoomControl.SupportedModes.First();
            }

            zoomControl.Configure(settings);
        }

        private void TorchCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            //   _mediaCapture.VideoDeviceController.TorchControl.Enabled = (TorchCheckBox.IsChecked == true);
            if (flashOnOff == false)
            {
                _mediaCapture.VideoDeviceController.TorchControl.Enabled = true;
                BitmapImage bmp = new BitmapImage();
                Uri u = new Uri("ms-appx:/Assets/flashbuttonon.png", UriKind.RelativeOrAbsolute);
                bmp.UriSource = u;
                // NOTE: change starts here
                Image i = new Image();
                i.Source = bmp;
                FlashButton.Content = i;
                flashOnOff = true;
            }
            else
            {
                _mediaCapture.VideoDeviceController.TorchControl.Enabled = false;
                BitmapImage bmp = new BitmapImage();
                Uri u = new Uri("ms-appx:/Assets/flashbuttonoff.png", UriKind.RelativeOrAbsolute);
                bmp.UriSource = u;
                // NOTE: change starts here
                Image i = new Image();
                i.Source = bmp;
                FlashButton.Content = i;
                flashOnOff = false;
            }
        }

        public static Windows.Foundation.Metadata.Platform DetectPlatform()
        {
            bool isHardwareButtonsAPIPresent =
                Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons");

            if (isHardwareButtonsAPIPresent)
            {
                return Windows.Foundation.Metadata.Platform.WindowsPhone;
            }
            else
            {
                return Windows.Foundation.Metadata.Platform.Windows;
            }
        }


        private void onFocusTimer(object sender, object e)
        {
            try
            {

                if (_mediaCapture.VideoDeviceController.FocusControl.Supported)
                {
                    _mediaCapture.VideoDeviceController.FocusControl.Configure(new FocusSettings { Mode = FocusMode.Auto, Value = 100, DisableDriverFallback = true });
                    _mediaCapture.VideoDeviceController.FocusControl.FocusAsync();
                }

                else
                if (_mediaCapture.VideoDeviceController.Focus.Capabilities.Supported)
                {
                    bool success = _mediaCapture.VideoDeviceController.Focus.TrySetAuto(false);
                    success = _mediaCapture.VideoDeviceController.Focus.TrySetValue(_mediaCapture.VideoDeviceController.Focus.Capabilities.Max);
                    success = _mediaCapture.VideoDeviceController.Focus.TrySetAuto(true);
                }

            }
            catch (Exception e2)
            {
                focusTimer.Stop();
                Debug.WriteLine(e2.Message);
            }
        }


        private async Task GetPreviewFrameAsSoftwareBitmapAsync()
        {

            if (resultDisplayed)
            {
                //GetPreviewFrameAsSoftwareBitmapAsync();
                return;
            }


            int height = 0;
            int width = 0;
            byte[] returnArray = null;
            //   lock(LOCKCounter)
            // {

            //}
            string resString = null;

            try
            {
                var previewProperties = _mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties; //.videoPreview

                Windows.Graphics.Imaging.BitmapPixelFormat pixelFormat = Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8;
                if (previewProperties.Subtype.Equals("YUY2"))
                {
                    pixelFormat = Windows.Graphics.Imaging.BitmapPixelFormat.Yuy2;
                }
                else
                if (previewProperties.Subtype.Equals("NV12"))
                {
                    pixelFormat = Windows.Graphics.Imaging.BitmapPixelFormat.Nv12;
                }


                if (thredsCounter < maxThreads)
                {

                    var videoFrame = new VideoFrame(pixelFormat, (int)previewProperties.Width, (int)previewProperties.Height);

                    await _mediaCapture.GetPreviewFrameAsync(videoFrame);
                    //   counter++;
                    //  txtRes.Text = counter.ToString();
                    Windows.Graphics.Imaging.SoftwareBitmap previewFrame = videoFrame.SoftwareBitmap;
                    returnArray = convertToGrayscale(previewFrame, out width, out height);
                    // previewFrame.Dispose();
                    // previewFrame = null;
                    //Thread thread = new Thread(decodeFrame(returnArray, width, height));

                    lock (LOCK)
                    {
                        ++thredsCounter;
                    }
                    Task decode = new Task(() => decodeFrame(returnArray, width, height));
                    decode.Start();
                }

                //  var sbSource = new SoftwareBitmapSource();
                //await sbSource.SetBitmapAsync(previewFrame);

                //PreviewFrameImage.Source = sbSource;

                GetPreviewFrameAsSoftwareBitmapAsync();
            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.Message);
            }
        }


        private void decodeFrame(byte[] returnArray, int width, int height)
        {

            byte[] result = new byte[10000];

            //resString = "";
            if (returnArray != null)
            {
                int resLen = BarcodeLib.Scanner.MWBscanGrayscaleImage(returnArray, width, height, result);
                MWResult mwResult = null;

                if (resultDisplayed)
                {
                    resLen = -1;
                }

                if (resLen > 0)
                {
                    MWResults results = new MWResults(result);
                    string s = System.Text.Encoding.UTF8.GetString(result, 0, result.Length);

                    if (results.count > 0)
                    {
                        mwResult = results.getResult(0);
                        result = mwResult.bytes;
                    }

                    if (mwResult != null)
                    {
                        resultDisplayed = true;

                        string typeName = BarcodeHelper.getBarcodeName(mwResult.type);

                        byte[] retVal = new byte[6000];
                        string displayString;

                        if (USE_MWPARSER == true)
                        {
                            double d = BarcodeLib.Scanner.MWPgetJSON(MWPARSER_MASK, System.Text.Encoding.UTF8.GetBytes(mwResult.encryptedResult), retVal);

                            if (d == -1)
                            {
                                displayString = mwResult.text;
                            }
                            else
                            {
                                displayString = System.Text.Encoding.UTF8.GetString(retVal);
                            }
                        }
                        else
                        {
                            displayString = mwResult.text;
                        }

                        if (USE_ANALYTICS)
                        {
                            sendReport(mwResult);
                        }

                        try
                        {
                            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                            {
                                displayResultAsync(displayString, typeName);

                            });
                        }
                        catch (Exception ee)
                        {
                            Debug.WriteLine(ee.Message);
                        }
                    }
                }
                returnArray = null;
                //GC.Collect();
            }
            lock (LOCK)
            {
                --thredsCounter;
            }
        }

        private async void sendReport(MWResult mwResult)
        {
            double lat = pos.Coordinate.Point.Position.Latitude; // current latitude
            double longt = pos.Coordinate.Point.Position.Longitude; // current longitude
            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            int timeInSec = (int)t.TotalSeconds;
            string securityData = timeInSec.ToString();

            DeviceInfo info = DeviceInfo.Instance;


            string post = "osName=";
            post += Info.OS;
            post += "&deviceID=";
            post += info.Id; // sto e device id
            post += "&appName=";
            post += Info.ApplicationName;
            post += "&appVersion=";
            post += Info.ApplicationVersion;
            post += "&osVersion=";
            post += Info.SystemVersion;
            post += "&symbology=";
            post += BarcodeHelper.getBarcodeName(mwResult.type);
            post += "&Code=";
            if (USE_MWPARSER)
                post += mwResult.encryptedResult;
            else
                post += mwResult.text;
            post += "&Manufacturer=";
            post += Info.DeviceManufacturer;
            post += "&Model=";
            post += Info.DeviceModel;
            post += "&session=";
            post += securityData;
            post += "|";
            post += info.Id;
            post += "&api_user=";
            post += apiUser;
            post += "&security_data=";
            post += securityData;
            post += "&security_hash=";
            post += hmac(securityData + apiKey, apiKey);

            if (lat != 0 && longt != 0)
            {
                post += "&lat=";
                post += lat;
                post += "&lng=";
                post += longt;
            }

            await httpPostRequest(post);

        }

        private async Task httpPostRequest(string post)
        {
            Uri resourceAddress;

            // The value of 'AddressField' is set by the user and is therefore untrusted input. If we can't create a
            // valid, absolute URI, we'll notify the user about the incorrect input.
            if (!TryGetUri(baseURL, out resourceAddress))
            {
                return;
            }

            try
            {
                WebRequest request = WebRequest.CreateHttp(resourceAddress);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                using (var stream = await Task.Factory.FromAsync<Stream>(request.BeginGetRequestStream, request.EndGetRequestStream, null))
                {
                    //  string postData = JsonConvert.SerializeObject(post);
                    byte[] postDataAsBytes = Encoding.UTF8.GetBytes(post);
                    await stream.WriteAsync(postDataAsBytes, 0, postDataAsBytes.Length);
                }

                using (var response = await Task.Factory.FromAsync<WebResponse>(request.BeginGetResponse, request.EndGetResponse, null))
                {

                    /*  Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                      {
                          displayResultAsync(response.ToString(), "response");

                      });
                      */
                }




            }
            catch (Exception ee)
            {
                Debug.WriteLine(ee.Message);
            }
        }

        /*  sha1nfo s;


    uint8_t hmacKey[]={
        '7','a','d','0','f','9','b','3','9','e','d','4','5','0','2','1','8','c','a','8','e','9','d','0','f','7','4','2','f','7','4','4','d','3','b','1','0','1','3','7'

    };
    printf("Test: FIPS 198a A.1\n");
    printf("Expect:251e6bfc902dfd087c25f5a67b79e2bc895865b4\n");
    printf("Result:");
    const char data[] = {
        '1','4','5','5','8','0','1','9','9','2','7','a','d','0','f','9','b','3','9','e','d','4','5','0','2','1','8','c','a','8','e','9','d','0','f','7','4','2','f','7','4','4','d','3','b','1','0','1','3','7'
    };
    sha1_initHmac(&s, hmacKey, 40);
    sha1_write(&s, data,40+10);
    printHash(sha1_resultHmac(&s));
    printf("\n\n");
*/

        private string hmac(string message, string key)
        {
            IBuffer KeyMaterial = CryptographicBuffer.ConvertStringToBinary(key, BinaryStringEncoding.Utf8);
            MacAlgorithmProvider HmacSha1Provider = MacAlgorithmProvider.OpenAlgorithm("HMAC_SHA1");
            CryptographicKey MacKey = HmacSha1Provider.CreateKey(KeyMaterial);
            IBuffer DataToBeSigned = CryptographicBuffer.ConvertStringToBinary(message, BinaryStringEncoding.Utf8);
            IBuffer SignatureBuffer = CryptographicEngine.Sign(MacKey, DataToBeSigned);
            string Signature = CryptographicBuffer.EncodeToHexString(SignatureBuffer);// EncodeToBase64String(SignatureBuffer);
            return Signature;
        }

        private string hmac1(string message, string key)
        {
            /*     IBuffer KeyMaterial = CryptographicBuffer.ConvertStringToBinary(key + "&", BinaryStringEncoding.Utf8);
                 MacAlgorithmProvider HmacSha1Provider = MacAlgorithmProvider.OpenAlgorithm("HMAC_SHA1");
                 CryptographicKey MacKey = HmacSha1Provider.CreateKey(KeyMaterial);
                 IBuffer DataToBeSigned = CryptographicBuffer.ConvertStringToBinary(message, BinaryStringEncoding.Utf8);
                 IBuffer SignatureBuffer = CryptographicEngine.Sign(MacKey, DataToBeSigned);
                 string Signature = CryptographicBuffer.EncodeToHexString(SignatureBuffer);// EncodeToBase64String(SignatureBuffer);
                 */
            return message; // Signature;
        }

        internal static bool TryGetUri(string uriString, out Uri uri)
        {
            // Note that this app has both "Internet (Client)" and "Home and Work Networking" capabilities set,
            // since the user may provide URIs for servers located on the internet or intranet. If apps only
            // communicate with servers on the internet, only the "Internet (Client)" capability should be set.
            // Similarly if an app is only intended to communicate on the intranet, only the "Home and Work
            // Networking" capability should be set.
            if (!Uri.TryCreate(uriString.Trim(), UriKind.Absolute, out uri))
            {
                return false;
            }

            if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                return false;
            }

            return true;
        }


        private async void displayResultAsync(string text, string typeName)
        {
            MessageDialog dialog = new MessageDialog(text, typeName);
            await dialog.ShowAsync();
            resultDisplayed = false;

            try
            {
                if (editItemIndex >= 0)
                {
                    listItems[editItemIndex].BarcodeResult = text;
                    listItems[editItemIndex].BarcodeType = typeName;
                }
                else
                {
                    ItemModel itemResult = new ItemModel();

                    itemResult.ID = 1;
                    if (listItems.Count > 0)
                        itemResult.ID = ((int)listItems.Max(x => x.ID)) + 1;

                    itemResult.Name = "";
                    itemResult.BarcodeResult = text;
                    itemResult.BarcodeType = typeName;

                    listItems.Insert(0, itemResult);
                }

                if (Xamarin.Forms.Application.Current.Properties.ContainsKey("AllLists"))
                {
                    string jsonList = Xamarin.Forms.Application.Current.Properties["AllLists"].ToString();
                    ObservableCollection<ListsModel> tempList = JsonConvert.DeserializeObject<ObservableCollection<ListsModel>>(jsonList);

                    tempList[editListIndex].Items = listItems;

                    Xamarin.Forms.Application.Current.Properties["AllLists"] = JsonConvert.SerializeObject(tempList);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            await ((Xamarin.Forms.Platform.UWP.PageRenderer)this.Parent).Element.Navigation.PopModalAsync();

            //GetPreviewFrameAsSoftwareBitmapAsync();
        }

        private unsafe byte[] convertToGrayscale(SoftwareBitmap bitmap, out int width, out int height)
        {
            byte* data = null;
            uint capacity = 0;
            width = 0;
            height = 0;
            byte[] returnArray = null;

            // Effect is hard-coded to operate on BGRA8 format only
            if (bitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8 || bitmap.BitmapPixelFormat == BitmapPixelFormat.Nv12 ||
                bitmap.BitmapPixelFormat == BitmapPixelFormat.Yuy2 || bitmap.BitmapPixelFormat == BitmapPixelFormat.Gray8)
            {
                // In BGRA8 format, each pixel is defined by 4 bytes
                int BYTES_PER_PIXEL = 4;

                using (var buffer = bitmap.LockBuffer(BitmapBufferAccessMode.ReadWrite))
                using (IMemoryBufferReference reference = buffer.CreateReference())
                {
                    if (reference is IMemoryBufferByteAccess)
                    {
                        // Get a pointer to the pixel buffer
                        ((IMemoryBufferByteAccess)reference).GetBuffer(out data, out capacity);
                        var desc = buffer.GetPlaneDescription(0);
                        width = desc.Width;
                        height = desc.Height;
                        returnArray = new byte[desc.Width * desc.Height];
                        if (bitmap.BitmapPixelFormat == BitmapPixelFormat.Yuy2)
                        {
                            int length = desc.Width * desc.Height;
                            for (int i = 0; i < length; i++)
                            {
                                returnArray[i] = data[i << 1];
                            }
                        }
                        else
                        if (bitmap.BitmapPixelFormat == BitmapPixelFormat.Nv12 || bitmap.BitmapPixelFormat == BitmapPixelFormat.Gray8)
                        {

                            Marshal.Copy((IntPtr)data, returnArray, 0, desc.Width * desc.Height);
                        }
                        else

                        if (bitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8)
                        {
                            BYTES_PER_PIXEL = 4;

                            // Get information about the BitmapBuffer

                            // Iterate over all pixels
                            width = desc.Width;
                            height = desc.Height;
                            for (uint row = 0; row < desc.Height; row++)
                            {
                                for (uint col = 0; col < desc.Width; col++)
                                {
                                    // Index of the current pixel in the buffer (defined by the next 4 bytes, BGRA8)
                                    var currPixel = desc.StartIndex + desc.Stride * row + BYTES_PER_PIXEL * col;

                                    // Read the current pixel information into b,g,r channels (leave out alpha channel)
                                    var b = data[currPixel + 0]; // Blue
                                    var g = data[currPixel + 1]; // Green
                                    var r = data[currPixel + 2]; // Red

                                    int y = (r * 77) + (g * 151) + (b * 28) >> 8;
                                    /*
                                                                    data[currPixel + 0] = (byte)y;
                                                                    data[currPixel + 1] = (byte)y;
                                                                    data[currPixel + 2] = (byte)y;
                                                                    */
                                    returnArray[row * desc.Width + col] = (byte)y;
                                }
                            }
                        }
                    }
                }
            }
            return returnArray;
        }

        private async void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // If there is an orientation sensor present on the device, register for notifications

            if (platform == Platform.Windows)
            {
                if (_isPreviewing)
                {
                    await StopPreviewAsync();
                    VideoRotation trt = ConvertDeviceOrientationToDegrees1(_displayOrientation);
                    _mediaCapture.SetPreviewRotation(trt);
                    await Task.Delay(30);
                    await StartPreviewAsync();
                }
            }
        }

        #endregion
    }

    public sealed class DeviceInfo
    {
        private static DeviceInfo _Instance;
        public static DeviceInfo Instance
        {
            get
            {
                if (_Instance == null)
                    _Instance = new DeviceInfo();
                return _Instance;
            }

        }

        public string Id { get; private set; }
        public string Model { get; private set; }
        public string Manufracturer { get; private set; }
        public string Name { get; private set; }
        public static string OSName { get; set; }

        private DeviceInfo()
        {
            Id = GetId();
            var deviceInformation = new EasClientDeviceInformation();
            Model = deviceInformation.SystemProductName;
            Manufracturer = deviceInformation.SystemManufacturer;
            Name = deviceInformation.FriendlyName;
            OSName = deviceInformation.OperatingSystem;
        }

        private static string GetId()
        {
            if (Windows.Foundation.Metadata.ApiInformation.IsTypePresent("Windows.System.Profile.HardwareIdentification"))
            {
                var token = HardwareIdentification.GetPackageSpecificToken(null);
                var hardwareId = token.Id;
                var dataReader = Windows.Storage.Streams.DataReader.FromBuffer(hardwareId);

                byte[] bytes = new byte[hardwareId.Length];
                dataReader.ReadBytes(bytes);

                return BitConverter.ToString(bytes).Replace("-", "");
            }

            throw new Exception("NO API FOR DEVICE ID PRESENT!");
        }
    }
}
