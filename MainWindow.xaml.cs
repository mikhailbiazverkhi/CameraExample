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
using static System.Runtime.InteropServices.JavaScript.JSType;

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

        // Добавляем поле для хранения CameraSettings
        private CameraSettings _cameraSettings;

        #endregion

        #region Launcher

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

            // Запускаем обновление Exposure после инициализации
            _ = UpdateExposureBasedOnColorAsync();
            _ = UpdateVideoProcAmpParametersBasedOnFrameAsync();
        }

        // Анализ цветов в кадре
        private (int RedBrightness, int GreenBrightness, int BlueBrightness) AnalyzeFrameColors(Bitmap frame)
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

        private async Task UpdateExposureBasedOnColorAsync()
        {
            if (_cameraSettings?.CameraControlPropertySettings == null) return;

            while (true)
            {
                // Извлекаем кадр и анализируем цвета
                Bitmap frame = Frame;
                if (frame == null) continue;

                // Преобразуем кадр в изображение для анализа
                var colorData = AnalyzeFrameColors(frame);

                // Определяем среднюю яркость для каждого цвета
                int averageRedBrightness = colorData.RedBrightness;
                int averageGreenBrightness = colorData.GreenBrightness;
                int averageBlueBrightness = colorData.BlueBrightness;

                // Логика регулировки экспозиции на основе анализа
                int targetExposure = CalculateTargetExposure(averageRedBrightness, averageGreenBrightness, averageBlueBrightness);

                lock (_locker)
                {
                    // Обновляем экспозицию
                    foreach (var setting in _cameraSettings.CameraControlPropertySettings)
                    {
                        if (setting.CameraControlProperty == "Exposure")
                        {
                            setting.Value = targetExposure;

                            // Применяем изменения экспозиции
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

                // Задержка между обновлениями (например, 1 секунда)
                await Task.Delay(1000);
            }
        }

        private int currentExposure = -4; // Начальное значение экспозиции
        private readonly int targetBrightnessMin = 30; // Минимальная целевая яркость int targetBrightnessMin = 100
        //private readonly int targetBrightnessMax = 90; // Максимальная целевая яркость int targetBrightnessMax = 150
        private readonly int targetBrightnessMax = 80;

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

        private async Task UpdateVideoProcAmpParametersBasedOnFrameAsync()
        {
            if (_cameraSettings?.CameraProcAmpPropertySettings == null) return;

            while (true)
            {
                // Извлекаем текущий кадр
                Bitmap frame = Frame;
                if (frame == null) continue;

                // Анализируем цвета кадра
                var colorData = AnalyzeFrameColors(frame);

                // Определяем среднюю яркость кадра
                int averageBrightness = (colorData.RedBrightness + colorData.GreenBrightness + colorData.BlueBrightness) / 3;

                //Console.WriteLine($"Average Brightness222: {averageBrightness}");

                lock (_locker)
                {
                    foreach (var setting in _cameraSettings.CameraProcAmpPropertySettings)
                    {
                        // Применяем логическую регулировку параметров
                        switch (setting.VideoProcAmpProperty)
                        {
                            // Регулируем яркость
                            case "Brightness":
                                ////setting.Value = ClampValue(averageBrightness / 2, 0, 255); // Регулируем яркость
                                ////setting.Value = ClampValue(averageBrightness / 2, -64, 64);
                                ////setting.Value = 64;
                                // Проверяем, находится ли яркость в целевом диапазоне
                                if (averageBrightness < targetBrightnessMin)
                                {
                                    // Если яркость ниже целевого диапазона, увеличиваем яркость
                                    setting.Value = Math.Min(setting.Value + 10, 64); // Ограничение сверху (максимум 0)
                                }
                                else if (averageBrightness > targetBrightnessMax)
                                {
                                    // Если яркость выше целевого диапазона, уменьшаем яркость
                                    setting.Value = Math.Max(setting.Value - 10, -64); // Ограничение снизу (минимум -8)
                                }
                                Console.WriteLine($"SettingBrightness: {setting.Value}");
                                break;
                            case "Contrast":
                                setting.Value = ClampValue(averageBrightness / 3, 0, 255); // Регулируем контраст
                                break;
                            case "Hue":
                                setting.Value = ClampValue((averageBrightness % 360), -180, 180); // Регулируем оттенок
                                break;
                            case "Saturation":
                                setting.Value = ClampValue(averageBrightness / 4, 0, 255); // Регулируем насыщенность
                                break;
                            case "Sharpness":
                                setting.Value = ClampValue(averageBrightness / 5, 0, 255); // Регулируем резкость
                                break;
                            case "Gamma":
                                setting.Value = ClampValue(averageBrightness / 2, 1, 500); // Регулируем гамму
                                break;
                            case "WhiteBalance":
                                setting.Value = ClampValue(averageBrightness * 50, 2000, 10000); // Регулируем баланс белого
                                break;
                            case "Gain":
                                setting.Value = ClampValue(averageBrightness * 2, 0, 255); // Регулируем усиление
                                break;
                        }

                        // Применяем изменения к видеоустройству
                        if (_videoSource is VideoCaptureDevice videoDevice)
                        {
                            //videoDevice.SetVideoProperty(
                            videoDevice.SetVideoProcAmpProperty(
                                (VideoProcAmpProperty)Enum.Parse(typeof(VideoProcAmpProperty), setting.VideoProcAmpProperty),
                                setting.Value,
                                (VideoProcAmpFlags)Enum.Parse(typeof(VideoProcAmpFlags), setting.VideoProcAmpFlag));
                        }
                    }
                }

                // Задержка между обновлениями (например, 1 секунда)
                await Task.Delay(1000);
            }
        }

        // Вспомогательный метод для ограничения значения в диапазоне
        private int ClampValue(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }


        // Закрытие окна и освобождение ресурсов

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

