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
using IniParser;
using IniParser.Model;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using UMapx.Window;

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

        private int _currentExposure = -3; // Подходит для большинства стандартных условий
        private int _currentBrightness = 8; // Лёгкая компенсация для большинства камер
        private const int TargetBrightnessMin = 100; // Минимальная целевая яркость
        private const int TargetBrightnessMax = 150; // Максимальная целевая яркость
        private int _currentContrast = 50; // Нейтральное значение для большинства случаев
        private int _currentHue = 0;              // Нейтральный оттенок
        private int _currentWhiteBalance = 4500; // Нейтральная цветовая температура


        #endregion

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing;

            //InitializeCameraSettings();
            string iniFilePath = "settings.ini"; // Путь к вашему INI-файлу C:\GitHub_repos\CameraExample\bin\Debug\net8.0-windows\settings.ini
            LoadCameraSettingsFromIni(iniFilePath);

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

        private void LoadCameraSettingsFromIni(string filePath)
        {
            var parser = new FileIniDataParser();
            IniData iniData = parser.ReadFile(filePath);

            //Путь к конфигурационному INI-файлу C:\GitHub_repos\CameraExample\bin\Debug\net8.0 - windows\settings.ini
            _cameraSettings = new CameraSettings
            {
                CameraId = int.Parse(iniData["CameraSettings"]["CameraId"]),
                ResolutionId = int.Parse(iniData["CameraSettings"]["ResolutionId"]),
                CameraControlPropertySettings = new List<CameraControlPropertySettings>
        {
            new CameraControlPropertySettings
            {
                CameraControlProperty = "Exposure",
                Value = int.Parse(iniData["CameraControlPropertySettings"]["Exposure"]),
                CameraControlFlag = iniData["CameraControlPropertySettings"]["ExposureFlag"]
            }
        },
                CameraProcAmpPropertySettings = new List<CameraProcAmpPropertySettings>
        {
            new CameraProcAmpPropertySettings
            {
                VideoProcAmpProperty = "Brightness",
                Value = int.Parse(iniData["CameraProcAmpPropertySettings"]["Brightness"]),
                VideoProcAmpFlag = iniData["CameraProcAmpPropertySettings"]["BrightnessFlag"]
            },
            new CameraProcAmpPropertySettings
            {
                VideoProcAmpProperty = "Hue",
                Value = int.Parse(iniData["CameraProcAmpPropertySettings"]["Hue"]),
                VideoProcAmpFlag = iniData["CameraProcAmpPropertySettings"]["HueFlag"]
            },
            new CameraProcAmpPropertySettings
            {
                VideoProcAmpProperty = "Contrast",
                Value = int.Parse(iniData["CameraProcAmpPropertySettings"]["Contrast"]),
                VideoProcAmpFlag = iniData["CameraProcAmpPropertySettings"]["ContrastFlag"]
            },
            new CameraProcAmpPropertySettings
            {
                VideoProcAmpProperty = "WhiteBalance",
                Value = int.Parse(iniData["CameraProcAmpPropertySettings"]["WhiteBalance"]),
                VideoProcAmpFlag = iniData["CameraProcAmpPropertySettings"]["WhiteBalanceFlag"]
            }
        }
            };
        }



        //private void InitializeCameraSettings()
        //{
        //    _cameraSettings = new CameraSettings
        //    {
        //        CameraId = 1,
        //        ResolutionId = 0,// 3
        //        CameraControlPropertySettings = new List<CameraControlPropertySettings>
        //        {
        //            new CameraControlPropertySettings
        //            {
        //                CameraControlProperty = "Exposure",
        //                Value = 0,
        //                CameraControlFlag = "None"
        //            }
        //        },
        //        CameraProcAmpPropertySettings = new List<CameraProcAmpPropertySettings>
        //        {
        //            new CameraProcAmpPropertySettings
        //            {
        //                VideoProcAmpProperty = "Brightness",
        //                Value = 0,
        //                VideoProcAmpFlag = "None"
        //            },
        //            new CameraProcAmpPropertySettings
        //            {
        //                VideoProcAmpProperty = "Hue",
        //                Value = 0,
        //                VideoProcAmpFlag = "None"
        //            },
        //            new CameraProcAmpPropertySettings
        //            {
        //                VideoProcAmpProperty = "Contrast",
        //                Value = 0, 
        //                VideoProcAmpFlag = "None"
        //            },
        //            new CameraProcAmpPropertySettings
        //            {
        //                VideoProcAmpProperty = "WhiteBalance",
        //                Value = 4000, // Начальное значение белого баланса (например, 4000 К)
        //                VideoProcAmpFlag = "None"
        //            }
        //        }
        //    };
        //}

        private async Task UpdateCameraSettingsAsync()
        {
            while (true)
            {
                using var frame = Frame;
                if (frame == null) continue;

                //var (redMarker, greenMarker, blueMarker) = AnalyzeMarkers(frame);
                //Console.WriteLine($"Average R={redMarker}, G={greenMarker}, B={blueMarker}");

                // Анализируем цвет маркера в заданной позиции
                var redMarkerData = GetMarkerColor(frame, new System.Drawing.Point(50, 50), 20);
                Console.WriteLine($"red Marker Color: {redMarkerData.Color}, Brightness: {redMarkerData.Brightness}");

                var greenMarkerData = GetMarkerColor(frame, new System.Drawing.Point(frame.Width - 50, 50), 20);
                Console.WriteLine($"green Marker Color: {greenMarkerData.Color}, Brightness: {greenMarkerData.Brightness}");

                var blueMarkerData = GetMarkerColor(frame, new System.Drawing.Point(frame.Width / 2, frame.Height - 50), 2);
                Console.WriteLine($"blue Marker Color: {blueMarkerData.Color}, Brightness: {blueMarkerData.Brightness}");

                lock (_locker)
                {
                    UpdateExposure(redMarkerData.Brightness, greenMarkerData.Brightness, blueMarkerData.Brightness);
                    UpdateBrightness(redMarkerData.Brightness, greenMarkerData.Brightness, blueMarkerData.Brightness);
                    UpdateHue(redMarkerData.Brightness, greenMarkerData.Brightness, blueMarkerData.Brightness);
                    UpdateContrast(redMarkerData.Brightness, greenMarkerData.Brightness, blueMarkerData.Brightness);
                    UpdateWhiteBalance(redMarkerData.Brightness, greenMarkerData.Brightness, blueMarkerData.Brightness); // Новый вызов

                    //UpdateExposure(redMarker, greenMarker, blueMarker);
                    //UpdateBrightness(redMarker, greenMarker, blueMarker);
                    //UpdateHue(redMarker, greenMarker, blueMarker);
                    //UpdateContrast(redMarker, greenMarker, blueMarker);
                    //UpdateWhiteBalance(redMarker, greenMarker, blueMarker); // Новый вызов
                }

                await Task.Delay(2000);
            }
        }

        //private void UpdateExposure(int red, int green, int blue)
        //{
        //    int averageBrightness = (red + green + blue) / 3;

        //    // Рассчитываем целевое значение экспозиции
        //    int targetExposure = _currentExposure;
        //    if (averageBrightness < TargetBrightnessMin)
        //    {
        //        targetExposure = Math.Clamp(_currentExposure + 1, -8, 0);
        //    }
        //    else if (averageBrightness > TargetBrightnessMax)
        //    {
        //        targetExposure = Math.Clamp(_currentExposure - 1, -8, 0);
        //    }

        //    // Сглаживание изменения экспозиции
        //    _currentExposure = (int)(_currentExposure * 0.8 + targetExposure * 0.2);

        //    SetCameraProperty("Exposure", _currentExposure);
        //}

        //private void UpdateBrightness(int red, int green, int blue)
        //{
        //    double redWeight = 0.299;
        //    double greenWeight = 0.587;
        //    double blueWeight = 0.114;

        //    double weightedBrightness = (red * redWeight + green * greenWeight + blue * blueWeight);

        //    // Рассчитываем целевое значение яркости
        //    int targetBrightness = _currentBrightness;
        //    if (weightedBrightness < TargetBrightnessMin)
        //    {
        //        targetBrightness = Math.Clamp(_currentBrightness + 8, -64, 64);
        //    }
        //    else if (weightedBrightness > TargetBrightnessMax)
        //    {
        //        targetBrightness = Math.Clamp(_currentBrightness - 8, -64, 64);
        //    }

        //    // Сглаживание изменения яркости
        //    _currentBrightness = (int)(_currentBrightness * 0.8 + targetBrightness * 0.2);

        //    SetProcAmpProperty("Brightness", _currentBrightness);
        //}

        private void UpdateExposure(int red, int green, int blue)
        {
            // Рассчитываем среднюю яркость
            int averageBrightness = (red + green + blue) / 3;

            // Оценка отклонения от идеальной средней яркости
            if (averageBrightness < TargetBrightnessMin)
            {
                _currentExposure = Math.Clamp(_currentExposure + 1, -8, 0);
            }
            else if (averageBrightness > TargetBrightnessMax)
            {
                _currentExposure = Math.Clamp(_currentExposure - 1, -8, 0);
            }

            // Устанавливаем новое значение экспозиции
            SetCameraProperty("Exposure", _currentExposure);
        }

        private void UpdateBrightness(int red, int green, int blue)
        {
            // Расчет коэффициентов для цветовых каналов
            double redWeight = 0.299;
            double greenWeight = 0.587;
            double blueWeight = 0.114;

            // Рассчитываем взвешенную яркость (учитывая вклад каждого канала)
            double weightedBrightness = (red * redWeight + green * greenWeight + blue * blueWeight);

            // Оценка отклонения от идеальной яркости
            if (weightedBrightness < TargetBrightnessMin)
            {
                _currentBrightness = Math.Clamp(_currentBrightness + 8, -64, 64);
            }
            else if (weightedBrightness > TargetBrightnessMax)
            {
                _currentBrightness = Math.Clamp(_currentBrightness - 8, -64, 64);
            }

            // Устанавливаем новое значение яркости
            SetProcAmpProperty("Brightness", _currentBrightness);
        }


        //private void UpdateExposure(int red, int green, int blue)
        //{
        //    int averageBrightness = (red + green + blue) / 3;
        //    if (averageBrightness < TargetBrightnessMin)
        //        _currentExposure = Math.Min(_currentExposure + 1, 0);
        //    else if (averageBrightness > TargetBrightnessMax)
        //        _currentExposure = Math.Max(_currentExposure - 1, -8);

        //    SetCameraProperty("Exposure", _currentExposure);
        //}


        //private void UpdateBrightness(int red, int green, int blue)
        //{
        //    int averageBrightness = (red + green + blue) / 3;
        //    if (averageBrightness < TargetBrightnessMin)
        //        _currentBrightness = Math.Clamp(_currentBrightness + 8, -64, 64);
        //    else if (averageBrightness > TargetBrightnessMax)
        //        _currentBrightness = Math.Clamp(_currentBrightness - 8, -64, 64);

        //    SetProcAmpProperty("Brightness", _currentBrightness);
        //}

        //private void UpdateHue(int red, int green, int blue)
        //{
        //    int hueAdjustment = CalculateHueAdjustment(red, green, blue);
        //    SetProcAmpProperty("Hue", hueAdjustment);
        //}

        //private void UpdateContrast(int red, int green, int blue)
        //{
        //    int contrastAdjustment = CalculateContrastAdjustment(red, green, blue, _currentContrast);
        //    if (contrastAdjustment != _currentContrast)
        //    {
        //        _currentContrast = contrastAdjustment;
        //        SetProcAmpProperty("Contrast", contrastAdjustment);
        //    }
        //}

        //private void UpdateWhiteBalance(int red, int green, int blue)
        //{
        //    // Ищем отклонение каналов от среднего значения
        //    int average = (red + green + blue) / 3;
        //    int deltaRed = red - average;
        //    int deltaBlue = blue - average;

        //    // Вычисляем коррекцию WhiteBalance на основе отклонений (примерно)
        //    int whiteBalanceCorrection = 4000 + deltaBlue * 10 - deltaRed * 10;

        //    // Ограничиваем диапазон допустимых значений
        //    whiteBalanceCorrection = Math.Clamp(whiteBalanceCorrection, 2800, 6500);

        //    // Применяем новый баланс белого
        //    SetProcAmpProperty("WhiteBalance", whiteBalanceCorrection);
        //}

        private void UpdateHue(int red, int green, int blue)
        {
            // Рассчитываем корректировку оттенка (Hue)
            int hueAdjustment = CalculateHueAdjustment(red, green, blue);

            // Плавное сглаживание изменений оттенка
            int smoothedHue = (int)(_currentHue * 0.8 + hueAdjustment * 0.2);

            // Применяем значение
            _currentHue = smoothedHue;
            SetProcAmpProperty("Hue", _currentHue);
        }

        private void UpdateContrast(int red, int green, int blue)
        {
            // Рассчитываем корректировку контраста
            int contrastAdjustment = CalculateContrastAdjustment(red, green, blue, _currentContrast);

            // Если есть изменения, плавно обновляем
            if (contrastAdjustment != _currentContrast)
            {
                _currentContrast = (int)(_currentContrast * 0.8 + contrastAdjustment * 0.2);
                SetProcAmpProperty("Contrast", _currentContrast);
            }
        }

        private void UpdateWhiteBalance(int red, int green, int blue)
        {
            // Рассчитываем среднее значение каналов
            int average = (red + green + blue) / 3;

            // Рассчитываем отклонения
            int deltaRed = red - average;
            int deltaBlue = blue - average;

            // Рассчитываем коррекцию для баланса белого
            int whiteBalanceCorrection = 4000 + deltaBlue * 10 - deltaRed * 10;

            // Ограничиваем диапазон значений
            whiteBalanceCorrection = Math.Clamp(whiteBalanceCorrection, 2800, 6500);

            // Плавное сглаживание изменений
            _currentWhiteBalance = (int)(_currentWhiteBalance * 0.8 + whiteBalanceCorrection * 0.2);

            // Применяем значение
            SetProcAmpProperty("WhiteBalance", _currentWhiteBalance);
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

        #region Frame (markers) analysis

        //private static (string Red, string Green, string Blue) AnalyzeMarkers(Bitmap frame)
        //{
        //    return (
        //        GetMarkerColorAndBrightness(frame, new System.Drawing.Point(50, 50), 20),
        //        GetMarkerColorAndBrightness(frame, new System.Drawing.Point(frame.Width - 50, 50), 20),
        //        GetMarkerColorAndBrightness(frame, new System.Drawing.Point(frame.Width / 2, frame.Height - 50), 20)
        //    );
        //}



        //private static (string Color, int Brightness) AnalyzeMarkers(Bitmap frame)
        //{
        //    // Анализируем цвет маркера в заданной позиции
        //    var markerData = GetMarkerColor(frame, new System.Drawing.Point(50, 50), 20);
        //    Console.WriteLine($"Marker Color: {markerData.Color}, Brightness: {markerData.Brightness}");
        //    return markerData;
        //}


    private static (string Color, int Brightness) GetMarkerColor(Bitmap frame, System.Drawing.Point position, int size)
        {
            int totalR = 0, totalG = 0, totalB = 0, pixelCount = 0;
            int halfSize = size / 2;

            for (int y = Math.Max(0, position.Y - halfSize); y < Math.Min(frame.Height, position.Y + halfSize); y++)
            {
                for (int x = Math.Max(0, position.X - halfSize); x < Math.Min(frame.Width, position.X + halfSize); x++)
                {
                    Color pixel = frame.GetPixel(x, y);

                    totalR += pixel.R;
                    totalG += pixel.G;
                    totalB += pixel.B;
                    pixelCount++;
                }
            }

            //if (pixelCount == 0) return "Unknown";

            // Рассчитываем средние значения
            int avgR = totalR / pixelCount;
            int avgG = totalG / pixelCount;
            int avgB = totalB / pixelCount;

            //Console.WriteLine($"Average R={avgR}, G={avgG}, B={avgB}");

            // Определение цвета на основе RGB
            if (avgR > avgG && avgR > avgB) return ("Red", avgR);
            if (avgG > avgR && avgG > avgB) return ("Green", avgG);
            if (avgB > avgR && avgB > avgG) return ("Blue", avgB);

            // Если не удалось явно определить цвет
            //return "Unknown";
            return ("Unknown", 0);
        }

        #endregion


        #region Static methods (calculations)

        private static int CalculateHueAdjustment(int redMarker, int greenMarker, int blueMarker)
        {
            const int IdealRed = 255;
            const int IdealGreen = 255;
            const int IdealBlue = 255;

            // Отклонения от идеальных значений
            int redDelta = IdealRed - redMarker;
            int greenDelta = IdealGreen - greenMarker;
            int blueDelta = IdealBlue - blueMarker;

            // Расчет коррекции
            int hueAdjustment = (redDelta - greenDelta + blueDelta) / 3;

            // Ограничение значений
            return Math.Clamp(hueAdjustment, -30, 30);
        }

        private static int CalculateContrastAdjustment(int redMarker, int greenMarker, int blueMarker, int currentContrast)
        {
            int minColor = Math.Min(redMarker, Math.Min(greenMarker, blueMarker));
            int maxColor = Math.Max(redMarker, Math.Max(greenMarker, blueMarker));
            int brightnessRange = maxColor - minColor;

            // Определяем идеальный контраст
            int idealContrast;
            if (brightnessRange < TargetBrightnessMin)
                idealContrast = 80;
            else if (brightnessRange > TargetBrightnessMax)
                idealContrast = 50;
            else
                idealContrast = 65;

            // Плавная корректировка
            int newContrast = currentContrast + (idealContrast - currentContrast) / 4;

            // Ограничиваем диапазон
            return Math.Clamp(newContrast, 0, 100);
        }

        //private static int CalculateHueAdjustment(int redMarker, int greenMarker, int blueMarker)
        //{
        //    // Идеальные значения цветов маркеров
        //    const int IdealRed = 255;
        //    const int IdealGreen = 255;
        //    const int IdealBlue = 255;

        //    // Расчет среднего отклонения цветов
        //    int redDelta = Math.Abs(IdealRed - redMarker);
        //    int greenDelta = Math.Abs(IdealGreen - greenMarker);
        //    int blueDelta = Math.Abs(IdealBlue - blueMarker);

        //    // Определение общей корректировки оттенка (эмпирическое значение)
        //    int hueAdjustment = (redDelta - greenDelta + blueDelta) / 3;

        //    // Возврат ограниченной корректировки
        //    return Math.Clamp(hueAdjustment, -30, 30); // Диапазон корректировки Hue
        //}

        //private static int CalculateContrastAdjustment(int redMarker, int greenMarker, int blueMarker, int currentContrast)
        //{
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

        private static Bitmap AddColorMarkers(Bitmap frame)
        {
            //using Graphics g = Graphics.FromImage(frame);
            //int markerSize = 20;
            //g.FillEllipse(Brushes.Red, 40, 40, markerSize, markerSize);
            //g.FillEllipse(Brushes.Green, frame.Width - 60, 40, markerSize, markerSize);
            //g.FillEllipse(Brushes.Blue, frame.Width / 2 - 10, frame.Height - 60, markerSize, markerSize);
            return frame;
        }

        //private static Bitmap AddColorMarkers(Bitmap frame)
        //{
        //    using Graphics g = Graphics.FromImage(frame);
        //    int markerSize = 20;

        //    // Добавление кругов в качестве визуальных подсказок
        //    g.FillEllipse(Brushes.Red, 40, 40, markerSize, markerSize); // Красная подсказка
        //    g.FillEllipse(Brushes.Green, frame.Width - 60, 40, markerSize, markerSize); // Зелёная подсказка
        //    g.FillEllipse(Brushes.Blue, frame.Width / 2 - 10, frame.Height - 60, markerSize, markerSize); // Синяя подсказка

        //    // Подпись для визуальных маркеров
        //    g.DrawString("Guide Marker", new Font("Arial", 10), Brushes.Black, 40, 40 + markerSize);
        //    g.DrawString("Guide Marker", new Font("Arial", 10), Brushes.Black, frame.Width - 60, 40 + markerSize);
        //    g.DrawString("Guide Marker", new Font("Arial", 10), Brushes.Black, frame.Width / 2 - 10, frame.Height - 60 + markerSize);

        //    return frame;
        //}

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

