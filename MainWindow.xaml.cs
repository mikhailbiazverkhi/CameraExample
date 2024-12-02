using CameraExample.Core;
using CameraExample.Settings;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
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
        private const int TargetBrightnessMax = 90; // Максимальная целевая яркость int targetBrightnessMax = 150
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
                    },
                    new CameraProcAmpPropertySettings
                    {
                        VideoProcAmpProperty = "Contrast",
                        Value = 0, //65
                        VideoProcAmpFlag = "Auto"
                    },
                    new CameraProcAmpPropertySettings
                    {
                        VideoProcAmpProperty = "WhiteBalance",
                        Value = 4000, // Начальное значение белого баланса (например, 4000 К)
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

                var (redMarker, greenMarker, blueMarker) = AnalyzeMarkers(frame);

                lock (_locker)
                {
                    UpdateExposure(redMarker, greenMarker, blueMarker);
                    UpdateBrightness(redMarker, greenMarker, blueMarker);
                    UpdateHue(redMarker, greenMarker, blueMarker);
                    UpdateContrast(redMarker, greenMarker, blueMarker);
                    UpdateWhiteBalance(redMarker, greenMarker, blueMarker); // Новый вызов
                }

                await Task.Delay(2000);
            }
        }

        private void UpdateExposure(int red, int green, int blue)
        {
            int averageBrightness = (red + green + blue) / 3;
            if (averageBrightness < TargetBrightnessMin)
                _currentExposure = Math.Min(_currentExposure + 1, 0);
            else if (averageBrightness > TargetBrightnessMax)
                _currentExposure = Math.Max(_currentExposure - 1, -8);

            SetCameraProperty("Exposure", _currentExposure);
        }


        //private void UpdateBrightness(int red, int green, int blue)
        //{
        //    int averageBrightness = (red + green + blue) / 3;
        //    if (averageBrightness < TargetBrightnessMin)
        //        _currentBrightness = Math.Clamp(_currentBrightness + 8, -64, 64);
        //    else if (averageBrightness > TargetBrightnessMax)
        //        _currentBrightness = Math.Clamp(_currentBrightness - 8, -64, 64);

        //    SetProcAmpProperty("Brightness", _currentBrightness);
        //}

        private void UpdateBrightness(int red, int green, int blue)
        {
            int averageBrightness = (red + green + blue) / 3;
            int targetBrightness = _currentBrightness;

            if (averageBrightness < TargetBrightnessMin)
                targetBrightness = Math.Clamp(_currentBrightness + 8, -64, 64);
            else if (averageBrightness > TargetBrightnessMax)
                targetBrightness = Math.Clamp(_currentBrightness - 8, -64, 64);

            _currentBrightness = SmoothAdjustment(_currentBrightness, targetBrightness);
            SetProcAmpProperty("Brightness", _currentBrightness);
        }

        private void UpdateHue(int red, int green, int blue)
        {
            int hueAdjustment = CalculateHueAdjustment(red, green, blue);
            SetProcAmpProperty("Hue", hueAdjustment);
        }

        private int CalculateHueAdjustment(int redMarker, int greenMarker, int blueMarker)
        {
            //int redDelta = Math.Abs(255 - red);
            //int greenDelta = Math.Abs(255 - green);
            //int blueDelta = Math.Abs(255 - blue);
            //return Math.Clamp((redDelta - greenDelta + blueDelta) / 3, -30, 30);

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

            /////////////////////
            //Console.WriteLine($"Hue adjustment applied: {hueAdjustment}");

            // Возврат ограниченной корректировки
            return Math.Clamp(hueAdjustment, -30, 30); // Диапазон корректировки Hue
        }

        //private void UpdateContrast(int red, int green, int blue)
        //{
        //    int contrastAdjustment = CalculateContrastAdjustment(red, green, blue, _currentContrast);
        //    if (contrastAdjustment != _currentContrast)
        //    {
        //        _currentContrast = contrastAdjustment;
        //        SetProcAmpProperty("Contrast", contrastAdjustment);
        //    }
        //}

        //private int CalculateContrastAdjustment(int redMarker, int greenMarker, int blueMarker, int currentContrast)
        //{
        //    //int minColor = Math.Min(red, Math.Min(green, blue));
        //    //int maxColor = Math.Max(red, Math.Max(green, blue));
        //    //int brightnessRange = maxColor - minColor;
        //    //int idealContrast = brightnessRange < 50 ? 80 : (brightnessRange > 150 ? 50 : 65);
        //    //return Math.Clamp(currentContrast + (idealContrast - currentContrast) / 4, 0, 100);

        //    // Рассчитать разброс яркости
        //    int minColor = Math.Min(redMarker, Math.Min(greenMarker, blueMarker));
        //    int maxColor = Math.Max(redMarker, Math.Max(greenMarker, blueMarker));
        //    int brightnessRange = maxColor - minColor;

        //    // Определить идеальный контраст для текущего разброса
        //    int idealContrast = brightnessRange < TargetBrightnessMin ? 80 : (brightnessRange > TargetBrightnessMax ? 50 : 65);

        //    // Плавно скорректировать контраст к целевому значению
        //    int newContrast = currentContrast + (idealContrast - currentContrast) / 4;

        //    // Ограничить диапазон контраста
        //    return Math.Clamp(newContrast, 0, 100);

        //}

        private void UpdateContrast(int red, int green, int blue)
        {
            int minColor = Math.Min(red, Math.Min(green, blue));
            int maxColor = Math.Max(red, Math.Max(green, blue));
            int brightnessRange = maxColor - minColor;

            int idealContrast = brightnessRange < TargetBrightnessMin ? 80 :
                                (brightnessRange > TargetBrightnessMax ? 50 : 65);

            if (Math.Abs(_currentContrast - idealContrast) > 5) // Допустимый диапазон изменения
            {
                _currentContrast = SmoothAdjustment(_currentContrast, idealContrast);
                SetProcAmpProperty("Contrast", _currentContrast);
            }
        }

        private void UpdateWhiteBalance(int red, int green, int blue)
        {
            // Ищем отклонение каналов от среднего значения
            int average = (red + green + blue) / 3;
            int deltaRed = red - average;
            int deltaBlue = blue - average;

            // Вычисляем коррекцию WhiteBalance на основе отклонений (примерно)
            int whiteBalanceCorrection = 4000 + deltaBlue * 10 - deltaRed * 10;

            // Ограничиваем диапазон допустимых значений
            whiteBalanceCorrection = Math.Clamp(whiteBalanceCorrection, 2800, 6500);

            // Применяем новый баланс белого
            SetProcAmpProperty("WhiteBalance", whiteBalanceCorrection);
        }

        private int SmoothAdjustment(int currentValue, int targetValue, float smoothingFactor = 0.1f)
        {
            return (int)(currentValue + (targetValue - currentValue) * smoothingFactor);
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

        #region Frame(Markers) Analysis

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

        private (int Red, int Green, int Blue) AnalyzeMarkers(Bitmap frame)
        {
            return (
                GetAverageBrightness(frame, new System.Drawing.Point(50, 50), 50),
                GetAverageBrightness(frame, new System.Drawing.Point(frame.Width - 50, 50), 50),
                GetAverageBrightness(frame, new System.Drawing.Point(frame.Width / 2, frame.Height - 50), 50)
            );

            //var redMarker = GetAverageBrightness(frame, new System.Drawing.Point(50, 50), 50);
            //var greenMarker = GetAverageBrightness(frame, new System.Drawing.Point(frame.Width - 50, 50), 50);
            //var blueMarker = GetAverageBrightness(frame, new System.Drawing.Point(frame.Width / 2, frame.Height - 50), 50);

            //Console.WriteLine($"Markers - Red: {redMarker}, Green: {greenMarker}, Blue: {blueMarker}");
            //return (redMarker, greenMarker, blueMarker);
        }

        private int GetAverageBrightness(Bitmap frame, System.Drawing.Point position, int size)
        {
            // Конвертируем изображение в RGB-матрицу
            var rgbMatrix = BitmapMatrix.ToRGB(frame);
            int height = rgbMatrix[0].GetLength(0);
            int width = rgbMatrix[0].GetLength(1);

            int totalBrightness = 0, pixelCount = 0;

            // Обрабатываем область вокруг заданной позиции
            for (int y = position.Y - size / 2; y < position.Y + size / 2; y++)
            {
                for (int x = position.X - size / 2; x < position.X + size / 2; x++)
                {
                    // Проверяем, что координаты в пределах изображения
                    if (x >= 0 && x < width && y >= 0 && y < height)
                    {
                        // Извлекаем значения R, G, B
                        int red = (int)(rgbMatrix[0][y, x] * 255);
                        int green = (int)(rgbMatrix[1][y, x] * 255);
                        int blue = (int)(rgbMatrix[2][y, x] * 255);

                        // Рассчитываем яркость пикселя
                        int brightness = (int)(0.2126 * red + 0.7152 * green + 0.0722 * blue);

                        totalBrightness += brightness;
                        pixelCount++;
                    }
                }
            }

            // Возвращаем среднее значение яркости
            return pixelCount > 0 ? totalBrightness / pixelCount : 0;
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
            try
            {
                Bitmap frame = (Bitmap)eventArgs.Frame.Clone();
                Frame = AddColorMarkers(frame);
                UpdateImage();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing frame: {ex.Message}");
            }
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
                try
                {
                    var bitmapSource = Frame.ToBitmapSource();
                    bitmapSource.Freeze();
                    Dispatcher.BeginInvoke(() => imgColor.Source = bitmapSource);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating image: {ex.Message}");
                }
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

