using CameraExample.Core;
using CameraExample.Settings;
using IniParser;
using IniParser.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
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

        private int _currentExposure = -3; // Подходит для большинства стандартных условий
        private int _currentBrightness = 8; // Лёгкая компенсация для большинства камер
        private const int TargetBrightnessMin = 100; // Минимальная целевая яркость
        private const int TargetBrightnessMax = 150; // Максимальная целевая яркость
        private int _currentContrast = 50; // Нейтральное значение для большинства случаев
        private int _currentHue = 0;              // Нейтральный оттенок
        private int _currentWhiteBalance = 4500; // Нейтральная цветовая температура

        private System.Drawing.Point _redMarkerPosition, _greenMarkerPosition, _blueMarkerPosition;
        int indicatorSize; // Размер цветовой точки места установки маркера

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

            // Load marker positions
            _redMarkerPosition = new System.Drawing.Point(
                int.Parse(iniData["MarkerPositions"]["RedMarkerX"]),
                int.Parse(iniData["MarkerPositions"]["RedMarkerY"]));

            _greenMarkerPosition = new System.Drawing.Point(
                int.Parse(iniData["MarkerPositions"]["GreenMarkerX"]),
                int.Parse(iniData["MarkerPositions"]["GreenMarkerY"]));

            _blueMarkerPosition = new System.Drawing.Point(
                int.Parse(iniData["MarkerPositions"]["BlueMarkerX"]),
                int.Parse(iniData["MarkerPositions"]["BlueMarkerY"]));


            indicatorSize = int.Parse(iniData["PointerSize"]["IndicatorSize"]);
        }



        private async Task UpdateCameraSettingsAsync()
        {
            while (true)
            {
                using var frame = Frame;
                if (frame == null) continue;

                // Определение зон исключения
                var exclusionZones = new List<Rectangle>
                {
                    new Rectangle(_redMarkerPosition.X - indicatorSize / 2, _redMarkerPosition.Y - indicatorSize / 2, indicatorSize, indicatorSize),
                    new Rectangle(_greenMarkerPosition.X - indicatorSize / 2, _greenMarkerPosition.Y - indicatorSize / 2, indicatorSize, indicatorSize),
                    new Rectangle(_blueMarkerPosition.X - indicatorSize / 2, _blueMarkerPosition.Y - indicatorSize / 2, indicatorSize, indicatorSize)
                };

                // Анализируем цвет бублика в заданной позиции
                var redMarkerData = GetMarkerColor(frame, _redMarkerPosition, indicatorSize + 30, exclusionZones);
                Console.WriteLine($"red Marker Color: {redMarkerData.Color}, Brightness: {redMarkerData.Brightness}");
                var greenMarkerData = GetMarkerColor(frame, _greenMarkerPosition, indicatorSize + 30, exclusionZones);
                Console.WriteLine($"green Marker Color: {greenMarkerData.Color}, Brightness: {greenMarkerData.Brightness}");
                var blueMarkerData = GetMarkerColor(frame, _blueMarkerPosition, indicatorSize + 30, exclusionZones);
                Console.WriteLine($"blue Marker Color: {blueMarkerData.Color}, Brightness: {blueMarkerData.Brightness}");            

                lock (_locker)
                {
                    UpdateExposure(redMarkerData.Brightness, greenMarkerData.Brightness, blueMarkerData.Brightness);
                    UpdateBrightness(redMarkerData.Brightness, greenMarkerData.Brightness, blueMarkerData.Brightness);
                    UpdateHue(redMarkerData.Brightness, greenMarkerData.Brightness, blueMarkerData.Brightness);
                    UpdateWhiteBalance(redMarkerData.Brightness, greenMarkerData.Brightness, blueMarkerData.Brightness);
                    UpdateContrast(redMarkerData.Brightness, greenMarkerData.Brightness, blueMarkerData.Brightness);
                }

                await Task.Delay(2500);
            }
        }

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

        private static (string Color, int Brightness) GetMarkerColor(Bitmap frame, System.Drawing.Point position, int size, List<Rectangle> exclusionZones)
        {
            int totalR = 0, totalG = 0, totalB = 0, pixelCount = 0;
            int halfSize = size / 2;

            for (int y = Math.Max(0, position.Y - halfSize); y < Math.Min(frame.Height, position.Y + halfSize); y++)
            {
                for (int x = Math.Max(0, position.X - halfSize); x < Math.Min(frame.Width, position.X + halfSize); x++)
                {
                    var pixelPos = new System.Drawing.Point(x, y);
                    if (exclusionZones.Exists(zone => zone.Contains(pixelPos)))
                        continue; // Игнорируем пиксель, если он находится в зоне исключения

                    Color pixel = frame.GetPixel(x, y);
                    totalR += pixel.R;
                    totalG += pixel.G;
                    totalB += pixel.B;
                    pixelCount++;
                }
            }

            if (pixelCount == 0)
                return ("Unknown", 0); // Если нет пикселей для анализа

            // Рассчитываем средние значения
            int avgR = totalR / pixelCount;
            int avgG = totalG / pixelCount;
            int avgB = totalB / pixelCount;

            // Определение цвета на основе RGB
            if (avgR > avgG && avgR > avgB) return ("Red", avgR);
            if (avgG > avgR && avgG > avgB) return ("Green", avgG);
            if (avgB > avgR && avgB > avgG) return ("Blue", avgB);

            return ("Unknown", 0);
        }

        //    private static (string Color, int Brightness) GetMarkerColor(Bitmap frame, System.Drawing.Point position, int size)
        //    {
        //        int totalR = 0, totalG = 0, totalB = 0, pixelCount = 0;
        //        int halfSize = size / 2;

        //        // Зоны исключения для анализа
        //        var exclusionZones = new List<Rectangle>
        //{
        //    new Rectangle(40, 40, 20, 20),
        //    new Rectangle(frame.Width - 60, 40, 20, 20),
        //    new Rectangle(frame.Width / 2 - 10, frame.Height - 60, 20, 20)
        //};

        //        for (int y = Math.Max(0, position.Y - halfSize); y < Math.Min(frame.Height, position.Y + halfSize); y++)
        //        {
        //            for (int x = Math.Max(0, position.X - halfSize); x < Math.Min(frame.Width, position.X + halfSize); x++)
        //            {
        //                var pixelPos = new System.Drawing.Point(x, y);
        //                if (exclusionZones.Exists(zone => zone.Contains(pixelPos)))
        //                    continue; // Игнорируем пиксель, если он находится в зоне исключения

        //                Color pixel = frame.GetPixel(x, y);
        //                totalR += pixel.R;
        //                totalG += pixel.G;
        //                totalB += pixel.B;
        //                pixelCount++;
        //            }
        //        }

        //        if (pixelCount == 0)
        //            return ("Unknown", 0); // Если нет пикселей для анализа

        //        // Рассчитываем средние значения
        //        int avgR = totalR / pixelCount;
        //        int avgG = totalG / pixelCount;
        //        int avgB = totalB / pixelCount;

        //        // Определение цвета на основе RGB
        //        if (avgR > avgG && avgR > avgB) return ("Red", avgR);
        //        if (avgG > avgR && avgG > avgB) return ("Green", avgG);
        //        if (avgB > avgR && avgB > avgG) return ("Blue", avgB);

        //        return ("Unknown", 0);
        //    }
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
                Frame = AddColorMarkers(frame, _redMarkerPosition, _greenMarkerPosition, _blueMarkerPosition, indicatorSize);
                UpdateImage();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing frame: {ex.Message}");
            }
        }

        private static Bitmap AddColorMarkers(Bitmap frame, System.Drawing.Point redMarker, System.Drawing.Point greenMarker, System.Drawing.Point blueMarker, int indicatorSize)
        {
            using Graphics g = Graphics.FromImage(frame);
            //int markerSize = 20;

            // Draw markers based on positions
            var redMarkerRect = new Rectangle(redMarker.X - indicatorSize / 2, redMarker.Y - indicatorSize / 2, indicatorSize, indicatorSize);
            var greenMarkerRect = new Rectangle(greenMarker.X - indicatorSize / 2, greenMarker.Y - indicatorSize / 2, indicatorSize, indicatorSize);
            var blueMarkerRect = new Rectangle(blueMarker.X - indicatorSize / 2, blueMarker.Y - indicatorSize / 2, indicatorSize, indicatorSize);

            g.FillEllipse(Brushes.Red, redMarkerRect);
            g.FillEllipse(Brushes.Green, greenMarkerRect);
            g.FillEllipse(Brushes.Blue, blueMarkerRect);

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

