using CameraExample.Core;
using CameraExample.Settings;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
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
        private readonly CameraSettings _cameraSettings;
        private Bitmap _frame;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing;

            // Инициализируем _cameraSettings с настройками камеры
            _cameraSettings = new CameraSettings
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
                        VideoProcAmpProperty = "Brightness", 
                        // Brightness, Contrast, Hue, Saturation, Sharpness, Gamma, ColorEnable, WhiteBalance, BacklightCompensation, 
                        // Gain, DigitalMultiplier, DigitalMultiplierLimit, WhiteBalanceComponent, PowerlineFrequency
                        Value = 0, // Начальное значение и  only int value
                        VideoProcAmpFlag = "Manual" // Manual, Auto, None
                    }
                }
            };

            // Передаем _cameraSettings в VideoSource
            _videoSource = VideoSourceUtils.GetVideoDevice(_cameraSettings);

            if (_videoSource != null)
            {
                _videoSource.NewFrame += OnNewFrame;
                _videoSource.Start();
                Console.WriteLine("Video source has been successfully started");
            }

            // Запускаем обновление Exposure и Brightness после инициализации
            _ = UpdateCameraSettingsBasedOnFrameAsync();
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


        private async Task UpdateCameraSettingsBasedOnFrameAsync()
        {
            if (_cameraSettings == null) return;

            while (true)
            {
                using var frame = Frame;

                if (frame == null) continue;

                // Анализируем цвета кадра
                var colorData = AnalyzeFrameColors(frame);

                int averageRedBrightness = colorData.RedBrightness;
                int averageGreenBrightness = colorData.GreenBrightness;
                int averageBlueBrightness = colorData.BlueBrightness;
                int averageBrightness = (averageRedBrightness + averageGreenBrightness + averageBlueBrightness) / 3;

                lock (_locker)
                {
                    // Регулируем CameraControl экспозицию (Exposure)
                    if (_cameraSettings.CameraControlPropertySettings != null)
                    {
                        foreach (var setting in _cameraSettings.CameraControlPropertySettings)
                        {
                            if (setting.CameraControlProperty == "Exposure")
                            {
                                int targetExposure = CalculateTargetExposure(averageRedBrightness, averageGreenBrightness, averageBlueBrightness);
                                setting.Value = targetExposure;

                                if (_videoSource is VideoCaptureDevice videoDevice)
                                {
                                    videoDevice.SetCameraProperty(
                                        (CameraControlProperty)Enum.Parse(typeof(CameraControlProperty), setting.CameraControlProperty),
                                        setting.Value,
                                        (CameraControlFlags)Enum.Parse(typeof(CameraControlFlags), setting.CameraControlFlag));
                                }
                                break;
                            }
                        }
                    }

                    // Регулируем параметры VideoProcAmp яркость (Brightness)
                    if (_cameraSettings.CameraProcAmpPropertySettings != null)
                    {
                        foreach (var setting in _cameraSettings.CameraProcAmpPropertySettings)
                        {
                            switch (setting.VideoProcAmpProperty)
                            {
                                case "Brightness":
                                    if (averageBrightness < targetBrightnessMin)
                                        setting.Value = Math.Min(setting.Value + 10, 64);
                                    else if (averageBrightness > targetBrightnessMax)
                                        setting.Value = Math.Max(setting.Value - 10, -64);
                                    break;
                                case "Contrast":
                                    setting.Value = ClampValue(averageBrightness / 3, 0, 255);
                                    break;
                                case "Hue":
                                    setting.Value = ClampValue(averageBrightness % 360, -180, 180);
                                    break;
                                case "Saturation":
                                    setting.Value = ClampValue(averageBrightness / 4, 0, 255);
                                    break;
                                case "Sharpness":
                                    setting.Value = ClampValue(averageBrightness / 5, 0, 255);
                                    break;
                                case "Gamma":
                                    setting.Value = ClampValue(averageBrightness / 2, 1, 500);
                                    break;
                                case "WhiteBalance":
                                    setting.Value = ClampValue(averageBrightness * 50, 2000, 10000);
                                    break;
                                case "Gain":
                                    setting.Value = ClampValue(averageBrightness * 2, 0, 255);
                                    break;
                            }

                            if (_videoSource is VideoCaptureDevice videoDevice)
                            {
                                videoDevice.SetVideoProcAmpProperty(
                                    (VideoProcAmpProperty)Enum.Parse(typeof(VideoProcAmpProperty), setting.VideoProcAmpProperty),
                                    setting.Value,
                                    (VideoProcAmpFlags)Enum.Parse(typeof(VideoProcAmpFlags), setting.VideoProcAmpFlag));
                            }
                        }
                    }
                }

                // Задержка между обновлениями
                await Task.Delay(1000);
            }
        }

        #endregion

        #region Private

        private int currentExposure = -4; // Начальное значение экспозиции
        private const int targetBrightnessMin = 30; // Минимальная целевая яркость int targetBrightnessMin = 100
        //private const int targetBrightnessMax = 90; // Максимальная целевая яркость int targetBrightnessMax = 150
        private const int targetBrightnessMax = 80;

        // Анализ цветов в кадре
        private static (int RedBrightness, int GreenBrightness, int BlueBrightness) AnalyzeFrameColors(Bitmap frame)
        {
            // Используем метод ToRGB для получения массива плоскостей RGB
            float[][,] rgbArray = BitmapMatrix.ToRGB(frame); // [R, G, B], массив двумерных массивов

            int width = rgbArray[0].GetLength(0);  // Ширина
            int height = rgbArray[0].GetLength(1); // Высота

            float redSum = 0, greenSum = 0, blueSum = 0;
            int pixelCount = width * height;

            // Проходим по пикселям и суммируем RGB-компоненты
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    redSum += rgbArray[0][x, y];   // R-компонента
                    greenSum += rgbArray[1][x, y]; // G-компонента
                    blueSum += rgbArray[2][x, y];  // B-компонента
                }
            }

            // Рассчитываем среднюю яркость
            int redBrightness = (int)(redSum / pixelCount * 255);
            int greenBrightness = (int)(greenSum / pixelCount * 255);
            int blueBrightness = (int)(blueSum / pixelCount * 255);

            return (redBrightness, greenBrightness, blueBrightness);
        }

        private int CalculateTargetExposure(int redBrightness, int greenBrightness, int blueBrightness)
        {
            // Рассчитываем среднюю яркость
            int averageBrightness = (redBrightness + greenBrightness + blueBrightness) / 3;

            //Console.WriteLine($"AverageBrightness111: {averageBrightness}");

            // Проверяем, находится ли яркость в целевом диапазоне
            if (averageBrightness < targetBrightnessMin)
            {
                // Если яркость ниже целевого диапазона, увеличиваем экспозицию
                currentExposure = Math.Min(currentExposure + 1, 0); // Ограничение сверху (максимум 0)
            }
            else if (averageBrightness > targetBrightnessMax)
            {
                // Если яркость выше целевого диапазона, уменьшаем экспозицию
                currentExposure = Math.Max(currentExposure - 1, -8); // Ограничение снизу (минимум -8)
            }

            Console.WriteLine($"TargetExposure: {currentExposure}");

            return currentExposure;
        }

        // Вспомогательный метод для ограничения значения в диапазоне
        private static int ClampValue(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
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

