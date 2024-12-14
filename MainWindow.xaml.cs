using CameraExample.Config;
using CameraExample.Core;
using CameraExample.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

//using System.Windows.Shapes;
using System.Windows.Threading;
using UMapx.Video;
using UMapx.Video.DirectShow;
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
        private CameraSettings _cameraSettingsFromConfig, _cameraSettingsFromDriver;
        private Bitmap _frame;

        private int _currentExposure = -3; // Подходит для большинства стандартных условий
        private int _currentBrightness = 8; // Лёгкая компенсация для большинства камер
        private const int TargetBrightnessMin = 100; // Минимальная целевая яркость
        private const int TargetBrightnessMax = 150; // Максимальная целевая яркость
        private int _currentContrast = 50; // Нейтральное значение для большинства случаев
        private int _currentHue = 0;              // Нейтральный оттенок
        private int _currentWhiteBalance = 4500; // Нейтральная цветовая температура

        //from config
        string iniFilePath = Path.Combine(AppContext.BaseDirectory, "CameraConfig.ini"); //путь конфиг файла
        private System.Drawing.Point _redMarkerPositionFromConfig, _greenMarkerPositionFromConfig, _blueMarkerPositionFromConfig;
        int indicatorSizeFromConfig; // Размер цветовой точки места установки маркера
        

        #endregion

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing;

            LoadCameraSettings(iniFilePath);
            LoadIndicatorSettings(iniFilePath);

            _videoSource = VideoSourceUtils.GetVideoDevice(_cameraSettingsFromConfig);
            _cameraSettingsFromDriver = VideoSourceUtils.GetCameraSettingsFromDevice(_cameraSettingsFromConfig.CameraId);

            //DriverConfigSettings(_cameraSettingsFromConfig, _cameraSettingsFromDriver,
            //    _redMarkerPositionFromConfig, _greenMarkerPositionFromConfig, _blueMarkerPositionFromConfig, indicatorSizeFromConfig);

            if (_videoSource != null)
            {
                _videoSource.NewFrame += OnNewFrame;
                _videoSource.Start();
                Console.WriteLine("Video source has been successfully started");
            }

            _ = UpdateCameraSettingsAsync();
        }

        #endregion

        #region Camera and Markers Settings (config file)

        private void LoadCameraSettings(string filePath)
        {
            var ini = new IniFile(filePath);

            _cameraSettingsFromConfig = new CameraSettings
            {
                CameraId = int.Parse(ini.Read("Camera", "CameraId", "1")),
                ResolutionId = int.Parse(ini.Read("Camera", "ResolutionId", "0")),

                CameraControlPropertySettings = new List<CameraControlPropertySettings>
                {
                    new CameraControlPropertySettings
                    {
                        CameraControlProperty = ini.Read("Exposure", "CameraControlProperty", "Exposure"),
                        Value = int.Parse(ini.Read("Exposure", "Value", "0")),
                        CameraControlFlag = ini.Read("Exposure", "CameraControlFlag", "None")
                    }
                },
                CameraProcAmpPropertySettings = new List<CameraProcAmpPropertySettings>
                {
                    new CameraProcAmpPropertySettings
                    {
                        VideoProcAmpProperty = ini.Read("Brightness", "VideoProcAmpProperty", "Brightness"),
                        Value = int.Parse(ini.Read("Brightness", "Value", "0")),
                        VideoProcAmpFlag = ini.Read("Brightness", "VideoProcAmpFlag", "None")
                    },
                    new CameraProcAmpPropertySettings
                    {
                        VideoProcAmpProperty = ini.Read("Hue", "VideoProcAmpProperty", "Hue"),
                        Value = int.Parse(ini.Read("Hue", "Value", "0")),
                        VideoProcAmpFlag = ini.Read("Hue", "VideoProcAmpFlag", "None")
                    },
                    new CameraProcAmpPropertySettings
                    {
                        VideoProcAmpProperty = ini.Read("Contrast", "VideoProcAmpProperty", "Contrast"),
                        Value = int.Parse(ini.Read("Contrast", "Value", "0")),
                        VideoProcAmpFlag = ini.Read("Contrast", "VideoProcAmpFlag", "None")
                    },
                    new CameraProcAmpPropertySettings
                    {
                        VideoProcAmpProperty = ini.Read("WhiteBalance", "VideoProcAmpProperty", "WhiteBalance"),
                        Value = int.Parse(ini.Read("WhiteBalance", "Value", "4000")),
                        VideoProcAmpFlag = ini.Read("WhiteBalance", "VideoProcAmpFlag", "None")
                    }
                }
            };
        }

        private void LoadIndicatorSettings(string filePath)
        {
            var ini = new IniFile(filePath);

            // Load marker positions
            _redMarkerPositionFromConfig = new System.Drawing.Point(
                int.Parse(ini.Read("MarkerPositions", "RedMarkerX", "40")),
                int.Parse(ini.Read("MarkerPositions", "RedMarkerY", "40")));

            _greenMarkerPositionFromConfig = new System.Drawing.Point(
                int.Parse(ini.Read("MarkerPositions", "GreenMarkerX", "600")),
                int.Parse(ini.Read("MarkerPositions", "GreenMarkerY", "40")));

            _blueMarkerPositionFromConfig = new System.Drawing.Point(
                int.Parse(ini.Read("MarkerPositions", "BlueMarkerX", "300")),
                int.Parse(ini.Read("MarkerPositions", "BlueMarkerY", "400")));


            indicatorSizeFromConfig = int.Parse(ini.Read("PointerSize", "IndicatorSize", "20"));
        }

        //private void SaveCameraSettingsToIni(string filePath)
        //{
        //    // Используем StringBuilder для создания содержимого INI-файла
        //    var iniContent = new StringBuilder();

        //    // Сохранение настроек камеры
        //    iniContent.AppendLine("[Camera]");
        //    iniContent.AppendLine($"CameraId={_cameraSettingsFromConfig.CameraId}");
        //    iniContent.AppendLine($"ResolutionId={_cameraSettingsFromConfig.ResolutionId}");

        //    // Сохранение свойств управления камерой
        //    iniContent.AppendLine("[Exposure]");
        //    iniContent.AppendLine($"CameraControlProperty={_cameraSettingsFromDriver.CameraControlPropertySettings[1].CameraControlProperty}");
        //    iniContent.AppendLine($"Value={_cameraSettingsFromDriver.CameraControlPropertySettings[1].Value}");
        //    iniContent.AppendLine($"CameraControlFlag={_cameraSettingsFromDriver.CameraControlPropertySettings[1].CameraControlFlag}");



        //    // Сохранение свойств ProcAmp камеры (настройки изображения)
        //    iniContent.AppendLine("[Brightness]");
        //    iniContent.AppendLine($"VideoProcAmpProperty={_cameraSettingsFromDriver.CameraProcAmpPropertySettings[0].VideoProcAmpProperty}");
        //    iniContent.AppendLine($"Value={_cameraSettingsFromDriver.CameraProcAmpPropertySettings[0].Value}");
        //    iniContent.AppendLine($"VideoProcAmpFlag={_cameraSettingsFromDriver.CameraProcAmpPropertySettings[0].VideoProcAmpFlag}");

        //    iniContent.AppendLine("[Hue]");
        //    iniContent.AppendLine($"VideoProcAmpProperty={_cameraSettingsFromDriver.CameraProcAmpPropertySettings[2].VideoProcAmpProperty}");
        //    iniContent.AppendLine($"Value={_cameraSettingsFromDriver.CameraProcAmpPropertySettings[2].Value}");
        //    iniContent.AppendLine($"VideoProcAmpFlag={_cameraSettingsFromDriver.CameraProcAmpPropertySettings[2].VideoProcAmpFlag}");

        //    iniContent.AppendLine("[Contrast]");
        //    iniContent.AppendLine($"VideoProcAmpProperty={_cameraSettingsFromDriver.CameraProcAmpPropertySettings[1].VideoProcAmpProperty}");
        //    iniContent.AppendLine($"Value={_cameraSettingsFromDriver.CameraProcAmpPropertySettings[1].Value}");
        //    iniContent.AppendLine($"VideoProcAmpFlag={_cameraSettingsFromDriver.CameraProcAmpPropertySettings[1].VideoProcAmpFlag}");

        //    iniContent.AppendLine("[WhiteBalance]");
        //    iniContent.AppendLine($"VideoProcAmpProperty={_cameraSettingsFromDriver.CameraProcAmpPropertySettings[6].VideoProcAmpProperty}");
        //    iniContent.AppendLine($"Value={_cameraSettingsFromDriver.CameraProcAmpPropertySettings[6].Value}");
        //    iniContent.AppendLine($"VideoProcAmpFlag={_cameraSettingsFromDriver.CameraProcAmpPropertySettings[6].VideoProcAmpFlag}");

        //    // Сохранение позиций маркеров
        //    iniContent.AppendLine("[MarkerPositions]");
        //    iniContent.AppendLine($"RedMarkerX={_redMarkerPositionFromConfig.X}");
        //    iniContent.AppendLine($"RedMarkerY={_redMarkerPositionFromConfig.Y}");
        //    iniContent.AppendLine($"GreenMarkerX={_greenMarkerPositionFromConfig.X}");
        //    iniContent.AppendLine($"GreenMarkerY={_greenMarkerPositionFromConfig.Y}");
        //    iniContent.AppendLine($"BlueMarkerX={_blueMarkerPositionFromConfig.X}");
        //    iniContent.AppendLine($"BlueMarkerY={_blueMarkerPositionFromConfig.Y}");

        //    // Сохранение размера указателя
        //    iniContent.AppendLine("[PointerSize]");
        //    iniContent.AppendLine($"IndicatorSize={indicatorSizeFromConfig}");

        //    // Запись в файл
        //    File.WriteAllText(filePath, iniContent.ToString());
        //}

        private void DriverConfigSettings(CameraSettings cameraSettingsFromConfig, CameraSettings cameraSettingsFromDriver,
           System.Drawing.Point redMarkerPositionFromConfig, System.Drawing.Point greenMarkerPositionFromConfig, System.Drawing.Point blueMarkerPositionFromConfig, int indicatorSizeFromConfig)
        {
            // Сохранение настроек камеры
            Console.WriteLine($"Camera: CameraId={cameraSettingsFromConfig.CameraId}, " +
                $"ResolutionId={cameraSettingsFromConfig.ResolutionId}");

            // Сохранение свойств управления камерой
            Console.WriteLine($"Exposure: CameraControlProperty={cameraSettingsFromDriver.CameraControlPropertySettings[1].CameraControlProperty}, " +
                $"Value={cameraSettingsFromDriver.CameraControlPropertySettings[1].Value}, " +
                $"CameraControlFlag={cameraSettingsFromDriver.CameraControlPropertySettings[1].CameraControlFlag}");

            // Сохранение свойств ProcAmp камеры (настройки изображения)
            Console.WriteLine($"Brightness: CameraControlProperty={cameraSettingsFromDriver.CameraProcAmpPropertySettings[0].VideoProcAmpProperty}, " +
                $"Value={cameraSettingsFromDriver.CameraProcAmpPropertySettings[0].Value}, " +
                $"CameraControlFlag={cameraSettingsFromDriver.CameraProcAmpPropertySettings[0].VideoProcAmpFlag}");

            Console.WriteLine($"Hue: CameraControlProperty={cameraSettingsFromDriver.CameraProcAmpPropertySettings[2].VideoProcAmpProperty}, " +
                $"Value={cameraSettingsFromDriver.CameraProcAmpPropertySettings[2].Value}, " +
                $"CameraControlFlag={cameraSettingsFromDriver.CameraProcAmpPropertySettings[2].VideoProcAmpFlag}");

            Console.WriteLine($"Contrast: CameraControlProperty={cameraSettingsFromDriver.CameraProcAmpPropertySettings[1].VideoProcAmpProperty}, " +
                $"Value={cameraSettingsFromDriver.CameraProcAmpPropertySettings[1].Value}, " +
                $"CameraControlFlag={cameraSettingsFromDriver.CameraProcAmpPropertySettings[1].VideoProcAmpFlag}");

            Console.WriteLine($"WhiteBalance: CameraControlProperty={cameraSettingsFromDriver.CameraProcAmpPropertySettings[6].VideoProcAmpProperty}, " +
                $"Value={cameraSettingsFromDriver.CameraProcAmpPropertySettings[6].Value}, " +
                $"CameraControlFlag={cameraSettingsFromDriver.CameraProcAmpPropertySettings[6].VideoProcAmpFlag}");

            // Сохранение позиций маркеров
            Console.WriteLine($"MarkerPositions: RedMarkerX={redMarkerPositionFromConfig.X}, " +
                $"RedMarkerY={redMarkerPositionFromConfig.Y}");
            Console.WriteLine($"MarkerPositions: GreenMarkerX={greenMarkerPositionFromConfig.X}, " +
                $"GreenMarkerY={greenMarkerPositionFromConfig.Y}");
            Console.WriteLine($"MarkerPositions: BlueMarkerX={blueMarkerPositionFromConfig.X}, " +
                $"BlueMarkerY={blueMarkerPositionFromConfig.Y}");

            // Сохранение размера указателя
            Console.WriteLine($"PointerSize: IndicatorSize={indicatorSizeFromConfig}");
        }

        private void GetConfigSettings()
        {
            Console.WriteLine($"Exposure: {GetCameraProperty("Exposure")}");
            Console.WriteLine($"Brightness: {GetProcAmpProperty("Brightness")}");
            Console.WriteLine($"Hue: {GetProcAmpProperty("Hue")}");
            Console.WriteLine($"Contrast: {GetProcAmpProperty("Contrast")}");
            Console.WriteLine($"WhiteBalance: {GetProcAmpProperty("WhiteBalance")}");
        }

        #endregion

        #region Update Camera Settings

        private async Task UpdateCameraSettingsAsync()
        {
            while (true)
            {
                using var frame = Frame;
                if (frame == null) continue;

                // Определение зон исключения
                var exclusionZones = new List<Rectangle>
                {
                    new Rectangle(_redMarkerPositionFromConfig.X - indicatorSizeFromConfig / 2, _redMarkerPositionFromConfig.Y - indicatorSizeFromConfig / 2, indicatorSizeFromConfig, indicatorSizeFromConfig),
                    new Rectangle(_greenMarkerPositionFromConfig.X - indicatorSizeFromConfig / 2, _greenMarkerPositionFromConfig.Y - indicatorSizeFromConfig / 2, indicatorSizeFromConfig, indicatorSizeFromConfig),
                    new Rectangle(_blueMarkerPositionFromConfig.X - indicatorSizeFromConfig / 2, _blueMarkerPositionFromConfig.Y - indicatorSizeFromConfig / 2, indicatorSizeFromConfig, indicatorSizeFromConfig)
                };

                // Анализируем цвет бублика в заданной позиции
                var redMarkerData = GetMarkerColor(frame, _redMarkerPositionFromConfig, indicatorSizeFromConfig + 30, exclusionZones);
                Console.WriteLine($"red Marker Color: {redMarkerData.Color}, Brightness: {redMarkerData.Brightness}");
                var greenMarkerData = GetMarkerColor(frame, _greenMarkerPositionFromConfig, indicatorSizeFromConfig + 30, exclusionZones);
                Console.WriteLine($"green Marker Color: {greenMarkerData.Color}, Brightness: {greenMarkerData.Brightness}");
                var blueMarkerData = GetMarkerColor(frame, _blueMarkerPositionFromConfig, indicatorSizeFromConfig + 30, exclusionZones);
                Console.WriteLine($"blue Marker Color: {blueMarkerData.Color}, Brightness: {blueMarkerData.Brightness}");            

                lock (_locker)
                {
                    //UpdateExposure(redMarkerData.Brightness, greenMarkerData.Brightness, blueMarkerData.Brightness);
                    //UpdateBrightness(redMarkerData.Brightness, greenMarkerData.Brightness, blueMarkerData.Brightness);
                    //UpdateHue(redMarkerData.Brightness, greenMarkerData.Brightness, blueMarkerData.Brightness);
                    //UpdateWhiteBalance(redMarkerData.Brightness, greenMarkerData.Brightness, blueMarkerData.Brightness);
                    //UpdateContrast(redMarkerData.Brightness, greenMarkerData.Brightness, blueMarkerData.Brightness);

                    //    DriverConfigSettings(_cameraSettingsFromConfig, _cameraSettingsFromDriver,
                    //_redMarkerPositionFromConfig, _greenMarkerPositionFromConfig, _blueMarkerPositionFromConfig, indicatorSizeFromConfig);

                    GetConfigSettings();
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

        private int GetCameraProperty(string property)
        {
            if (_videoSource is VideoCaptureDevice videoDevice)
            {
                if (Enum.TryParse(typeof(CameraControlProperty), property, out var propertyEnum))
                {
                    videoDevice.GetCameraProperty(
                        (CameraControlProperty)propertyEnum,
                        out int value,
                        out CameraControlFlags controlFlags);

                    return value; // Возвращаем значение
                }
                else
                {
                    throw new ArgumentException($"Invalid property name: {property}", nameof(property));
                }
            }

            throw new InvalidOperationException("Video source is not a VideoCaptureDevice.");
        }

        //private void GetCameraProperty(string property, int value)
        //{
        //    if (_videoSource is VideoCaptureDevice videoDevice)
        //    {
        //        videoDevice.SetCameraProperty(
        //            (CameraControlProperty)Enum.Parse(typeof(CameraControlProperty), property),
        //            value,
        //            CameraControlFlags.Manual);
        //        videoDevice.GetCameraProperty
        //    }
        //}


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



        //private int GetProcAmpProperty1(string property)
        //{
        //    if (_videoSource is VideoCaptureDevice videoDevice)
        //    {
        //        videoDevice.GetVideoProcAmpProperty(
        //            (VideoProcAmpProperty)Enum.Parse(typeof(VideoProcAmpProperty), property),
        //            value,
        //            VideoProcAmpFlags.Manual);
        //    }
        //}

        private int GetProcAmpProperty(string property)
        {
            if (_videoSource is VideoCaptureDevice videoDevice)
            {
                if (Enum.TryParse(typeof(VideoProcAmpProperty), property, out var propertyEnum))
                {
                    videoDevice.GetVideoProcAmpProperty(
                        (VideoProcAmpProperty)propertyEnum,
                        out int value,
                        out VideoProcAmpFlags controlFlags);

                    return value; // Возвращаем значение
                }
                else
                {
                    throw new ArgumentException($"Invalid property name: {property}", nameof(property));
                }
            }

            throw new InvalidOperationException("Video source is not a VideoCaptureDevice.");
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

                    System.Drawing.Color pixel = frame.GetPixel(x, y);
                    //System.Windows.Media.Color pixel = frame.GetPixel(x, y);
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
                Frame = AddColorMarkers(frame, _redMarkerPositionFromConfig, _greenMarkerPositionFromConfig, _blueMarkerPositionFromConfig, indicatorSizeFromConfig);
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

            g.FillEllipse(System.Drawing.Brushes.Red, redMarkerRect);
            g.FillEllipse(System.Drawing.Brushes.Green, greenMarkerRect);
            //g.FillEllipse(System.Windows.Media.Brushes.Blue, blueMarkerRect);
            g.FillEllipse(System.Drawing.Brushes.Blue, blueMarkerRect);

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
            // Save the settings to the INI file after updating
            //SaveCameraSettingsToIni(iniFilePath);
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

