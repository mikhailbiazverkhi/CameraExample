using CameraExample.Core;
using CameraExample.Settings;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using UMapx.Video;

namespace CameraExample
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        #region Fields

        private readonly IVideoSource _videoSource;
        private static readonly object _locker = new();
        private Bitmap _frame;

        #endregion

        #region Launcher

        /// <summary>
        /// Constructor.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing;
            _videoSource = VideoSourceUtils.GetVideoDevice(new CameraSettings
            {
                CameraId = 1,
                ResolutionId = 0,
                CameraControlPropertySettings = new List<CameraControlPropertySettings>
                { 
                    new CameraControlPropertySettings
                    {
                        CameraControlProperty = "Exposure", // Pan, Tilt, Roll, Zoom, Exposure, Iris, Focus
                        Value = 0, // only int value
                        CameraControlFlag = "Manual" // Manual, Auto, None
                    }
                },
                CameraProcAmpPropertySettings = new List<CameraProcAmpPropertySettings> 
                { 
                    new CameraProcAmpPropertySettings
                    {
                        VideoProcAmpProperty = "Brightness", // Brightness, Contrast, Hue, Saturation, Sharpness, Gamma, ColorEnable, WhiteBalance, BacklightCompensation, 
                        // Gain, DigitalMultiplier, DigitalMultiplierLimit, WhiteBalanceComponent, PowerlineFrequency
                        Value = 64, // only int value
                        VideoProcAmpFlag = "Manual" // Manual, Auto, None
                    }
                }
            });

            if (_videoSource != null )
            {
                _videoSource.NewFrame += OnNewFrame;
                _videoSource.Start();
                Console.WriteLine("Video source has been successfully started");
            }
        }

        /// <summary>
        /// Windows closing.
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">Event args</param>
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _videoSource?.SignalToStop();
            _videoSource?.Dispose();
            _frame?.Dispose();
            Console.WriteLine("Video source has been successfully stopped");
        }

        #endregion

        #region Properties

        /// <summary>
        /// Get frame and dispose previous.
        /// </summary>
        private Bitmap Frame
        {
            get
            {
                if (_frame is null)
                    return null;

                Bitmap frame;

                lock (_locker)
                {
                    frame = (Bitmap)_frame.Clone();
                }

                return frame;
            }
            set
            {
                lock (_locker)
                {
                    if (_frame != null)
                    {
                        _frame.Dispose();
                        _frame = null;
                    }

                    _frame = value;
                }
            }
        }

        #endregion

        #region Handling events

        /// <summary>
        /// Frame handling on event call.
        /// </summary>
        /// <param name="sender">sender</param>
        /// <param name="eventArgs">event arguments</param>
        private void OnNewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Frame = (Bitmap)eventArgs.Frame.Clone();
            InvokeDrawing();
        }

        #endregion

        #region Private voids

        /// <summary>
        /// Invoke drawing method.
        /// </summary>
        private void InvokeDrawing()
        {
            // color drawing
            var printColor = Frame;

            if (printColor != null)
            {
                var bitmapColor = printColor.ToBitmapSource();
                bitmapColor.Freeze();
                _ = Dispatcher.BeginInvoke(new ThreadStart(delegate { imgColor.Source = bitmapColor; }));
            }
        }

        #endregion

        #region IDisposable

        private bool _disposed;

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _videoSource?.Dispose();
                    _frame?.Dispose();
                }
                _disposed = true;
            }
        }

        ~MainWindow()
        {
            Dispose(false);
        }

        #endregion
    }
}
