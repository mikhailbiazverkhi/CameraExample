using CameraExample.Core;
using CameraExample.Settings;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using UMapx.Imaging;
using UMapx.Video;
using UMapx.Video.DirectShow;

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
        private CameraSettings _cameraSettings;
        private Bitmap _frame;

        private int _currentExposure = -4; // Начальное значение экспозиции
        private int _currentBrightness = 0; // Переменная для хранения текущей яркости
        private const int TargetBrightnessMin = 30; // Минимальная целевая яркость int targetBrightnessMin = 100
        private const int TargetBrightnessMax = 80; // Максимальная целевая яркость int targetBrightnessMax = 150
        private int _currentContrast = 50; // Начальное значение контраста

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing;

            InitializeCameraSettings();
            _videoSource = VideoSourceUtils.GetVideoDevice(_cameraSettings);

            if (_videoSource != null)
            {
                _videoSource.NewFrame += OnNewFrame;
                _videoSource.Start();
                Console.WriteLine("Video source has been successfully started");
            }

            _ = UpdateCameraSettingsAsync();
        }
           
            #endregion

        #region Camera Settings

            private void InitializeCameraSettings()
            {
                _cameraSettings = new CameraSettings
                {
                    CameraId = 1,
                    ResolutionId = 0,
                    CameraControlPropertySettings = new List<CameraControlPropertySettings>
                {
                    new CameraControlPropertySettings
                    {
                        CameraControlProperty = "Exposure",
                        Value = 0,
                        CameraControlFlag = "Auto"
                    }
                },
                    CameraProcAmpPropertySettings = new List<CameraProcAmpPropertySettings>
                {
                    new CameraProcAmpPropertySettings
                    {
                        VideoProcAmpProperty = "Brightness",
                        Value = 0,
                        VideoProcAmpFlag = "Auto"
                    },
                    new CameraProcAmpPropertySettings
                    {
                        VideoProcAmpProperty = "Hue",
                        Value = 0,
                        VideoProcAmpFlag = "Auto"
                    }
                    ,
                    new CameraProcAmpPropertySettings
                    {
                        VideoProcAmpProperty = "Contrast",
                        Value = 65,
                        VideoProcAmpFlag = "Auto"
                    }
                }
                };
            }
        private async Task UpdateCameraSettingsAsync()
        {
            while (true)
            {
                using var frame = Frame;
                if (frame == null) continue;

                var (averageRed, averageGreen, averageBlue, _, _, _) = AnalyzeFrameColors(frame);
                int averageBrightness = (averageRed + averageGreen + averageBlue) / 3;

                lock (_locker)
                {
                    UpdateExposure(averageBrightness);
                    UpdateBrightness(averageBrightness);

                    // Обновление Hue
                    int hueAdjustment = CalculateHueAdjustment(averageRed, averageGreen, averageBlue);
                    SetProcAmpProperty("Hue", hueAdjustment);

                    // Обновление Contrast
                    int contrastAdjustment = CalculateContrastAdjustment(averageRed, averageGreen, averageBlue, _currentContrast);
                    if (contrastAdjustment != _currentContrast)
                    {
                        _currentContrast = contrastAdjustment;
                        SetProcAmpProperty("Contrast", contrastAdjustment);
                    }
                }

                await Task.Delay(1000);
            }
        }

        private void UpdateExposure(int averageBrightness)
        {
            if (averageBrightness < TargetBrightnessMin)
                _currentExposure = Math.Min(_currentExposure + 1, 0);
            else if (averageBrightness > TargetBrightnessMax)
                _currentExposure = Math.Max(_currentExposure - 1, -8);

            SetCameraProperty("Exposure", _currentExposure);
        }

        private void UpdateBrightness(int averageBrightness)
        {
            if (averageBrightness < TargetBrightnessMin)
            {
                _currentBrightness = Math.Clamp(_currentBrightness + 10, -64, 64);
            }
            else if (averageBrightness > TargetBrightnessMax)
            {
                _currentBrightness = Math.Clamp(_currentBrightness - 10, -64, 64);
            }

            SetProcAmpProperty("Brightness", _currentBrightness);
        }

        private void SetCameraProperty(string property, int value)
        {
            if (_videoSource is VideoCaptureDevice videoDevice)
            {
                videoDevice.SetCameraProperty(
                    (CameraControlProperty)Enum.Parse(typeof(CameraControlProperty), property),
                    value,
                    CameraControlFlags.Manual);
            }
        }

        private void SetProcAmpProperty(string property, int value)
        {
            if (_videoSource is VideoCaptureDevice videoDevice)
            {
                videoDevice.SetVideoProcAmpProperty(
                    (VideoProcAmpProperty)Enum.Parse(typeof(VideoProcAmpProperty), property),
                    value,
                    VideoProcAmpFlags.Manual);
            }
        }

        #endregion

        #region Frame Analysis

        private (int Red, int Green, int Blue, int RedMarker, int GreenMarker, int BlueMarker) AnalyzeFrameColors(Bitmap frame)
        {
            var (averageRed, averageGreen, averageBlue) = AnalyzeAverageColors(frame);
            var (redMarker, greenMarker, blueMarker) = AnalyzeMarkers(frame);

            return (averageRed, averageGreen, averageBlue, redMarker, greenMarker, blueMarker);
        }

        #region frame.GetPixel(x, y)1
        //private (int Red, int Green, int Blue) AnalyzeAverageColors(Bitmap frame)
        //{
        //    int pixelCount = frame.Width * frame.Height;
        //    long redSum = 0, greenSum = 0, blueSum = 0;

        //    for (int y = 0; y < frame.Height; y++)
        //    {
        //        for (int x = 0; x < frame.Width; x++)
        //        {
        //            var pixel = frame.GetPixel(x, y);  //
        //            redSum += pixel.R;
        //            greenSum += pixel.G;
        //            blueSum += pixel.B;
        //        }
        //    }
        //    //Console.WriteLine($"redSum / pixelCount11 {redSum / pixelCount}  greenSum / pixelCount11 {greenSum / pixelCount}  blueSum / pixelCount11  {blueSum / pixelCount}");
        //    return ((int)(redSum / pixelCount), (int)(greenSum / pixelCount), (int)(blueSum / pixelCount));
        //}
        #endregion

        //BitmapMatrix.ToRGB(frame)
        private (int Red, int Green, int Blue) AnalyzeAverageColors(Bitmap frame)
        {
            // Используем BitmapMatrix.ToRGB для получения данных RGB
            var rgbMatrix = BitmapMatrix.ToRGB(frame);
            int width = rgbMatrix[0].GetLength(0);
            int height = rgbMatrix[0].GetLength(1);

            float redSum = 0, greenSum = 0, blueSum = 0;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    redSum += rgbMatrix[2][x, y] * 256;
                    greenSum += rgbMatrix[1][x, y] * 256;
                    blueSum += rgbMatrix[0][x, y] * 256;
                }
            }
            int pixelCount = width * height;
            return ((int)(redSum / pixelCount), (int)(greenSum / pixelCount), (int)(blueSum / pixelCount));
        }


        private (int RedMarker, int GreenMarker, int BlueMarker) AnalyzeMarkers(Bitmap frame)
        {
            return (
                GetAverageBrightness(frame, new System.Drawing.Point(50, 50), 20),
                GetAverageBrightness(frame, new System.Drawing.Point(frame.Width - 50, 50), 20),
                GetAverageBrightness(frame, new System.Drawing.Point(frame.Width / 2, frame.Height - 50), 20)
            );
        }

        #region frame.GetPixel(x, y)2
        //private int GetAverageBrightness(Bitmap frame, System.Drawing.Point position, int size)
        //{
        //    int totalBrightness = 0, pixelCount = 0;

        //    for (int y = position.Y - size / 2; y < position.Y + size / 2; y++)
        //    {
        //        for (int x = position.X - size / 2; x < position.X + size / 2; x++)
        //        {
        //            if (x >= 0 && x < frame.Width && y >= 0 && y < frame.Height)
        //            {
        //                var pixel = frame.GetPixel(x, y);  //
        //                totalBrightness += (int)(0.2126 * pixel.R + 0.7152 * pixel.G + 0.0722 * pixel.B);
        //                pixelCount++;
        //            }
        //        }
        //    }

        //    Console.WriteLine($"pixelCount3 {pixelCount}");
        //    Console.WriteLine($"totalBrightness / pixelCount3 {totalBrightness / pixelCount}");

        //    return pixelCount > 0 ? totalBrightness / pixelCount : 0;
        //}
        #endregion

        //BitmapMatrix.ToRGB(frame)
        private int GetAverageBrightness(Bitmap frame, System.Drawing.Point position, int size)
        {
            // Используем BitmapMatrix.ToRGB для получения данных RGB
            var rgbMatrix = BitmapMatrix.ToRGB(frame);
            int height = rgbMatrix[0].GetLength(0);
            int width = rgbMatrix[0].GetLength(1);


            int totalBrightness = 0, pixelCount = 0;

            for (int y = position.Y - size / 2; y < position.Y + size / 2; y++)
            {
                for (int x = position.X - size / 2; x < position.X + size / 2; x++)
                {
                    if (x >= 0 && x < width && y >= 0 && y < height)
                    {
                        int brightness = (int)(0.2126 * rgbMatrix[0][y, x] * 256 + 0.7152 * rgbMatrix[1][y, x] * 256 + 0.0722 * rgbMatrix[2][y, x] * 256);
                        totalBrightness += brightness;
                        pixelCount++;
                    }
                }
            }
            return pixelCount > 0 ? totalBrightness / pixelCount : 0;
        }


        private int CalculateHueAdjustment(int redMarker, int greenMarker, int blueMarker)
        {
            // Идеальные значения цветов маркеров
            const int IdealRed = 255;
            const int IdealGreen = 255;
            const int IdealBlue = 255;

            // Расчет среднего отклонения цветов
            int redDelta = Math.Abs(IdealRed - redMarker);
            int greenDelta = Math.Abs(IdealGreen - greenMarker);
            int blueDelta = Math.Abs(IdealBlue - blueMarker);

            // Определение общей корректировки оттенка (эмпирическое значение)
            int hueAdjustment = (redDelta - greenDelta + blueDelta) / 3;

            // Возврат ограниченной корректировки
            return Math.Clamp(hueAdjustment, -30, 30); // Диапазон корректировки Hue
        }

        private int CalculateContrastAdjustment(int averageRed, int averageGreen, int averageBlue, int currentContrast)
        {
            // Рассчитать разброс яркости
            int minColor = Math.Min(averageRed, Math.Min(averageGreen, averageBlue));
            int maxColor = Math.Max(averageRed, Math.Max(averageGreen, averageBlue));
            int brightnessRange = maxColor - minColor;

            // Определить идеальный контраст для текущего разброса
            int idealContrast = brightnessRange < 50 ? 80 : (brightnessRange > 150 ? 50 : 65);

            // Плавно скорректировать контраст к целевому значению
            int newContrast = currentContrast + (idealContrast - currentContrast) / 4;

            // Ограничить диапазон контраста
            return Math.Clamp(newContrast, 0, 100);
        }

        #endregion

        #region Properties
        private Bitmap Frame
        {
            get
            {
                lock (_locker) return _frame?.Clone() as Bitmap;
            }
            set
            {
                lock (_locker)
                {
                    _frame?.Dispose();
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
            Bitmap frame = (Bitmap)eventArgs.Frame.Clone();
            Frame = AddColorMarkers(frame);
            UpdateImage();
        }

        private Bitmap AddColorMarkers(Bitmap frame)
        {
            using Graphics g = Graphics.FromImage(frame);
            int markerSize = 20;

            g.FillEllipse(Brushes.Red, 40, 40, markerSize, markerSize);
            g.FillEllipse(Brushes.Green, frame.Width - 60, 40, markerSize, markerSize);
            g.FillEllipse(Brushes.Blue, frame.Width / 2 - 10, frame.Height - 60, markerSize, markerSize);

            return frame;
        }

        private void UpdateImage()
        {
            if (Frame != null)
            {
                var bitmapSource = Frame.ToBitmapSource();
                bitmapSource.Freeze();
                Dispatcher.BeginInvoke(() => imgColor.Source = bitmapSource);
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
            _videoSource?.WaitForStop();
            Dispose();
            Console.WriteLine("Video source has been successfully stopped");
        }
        #endregion

        #region IDisposable

        public void Dispose()
        {
            _videoSource?.Dispose();
            _frame?.Dispose();
        }

        #endregion
    }
}

