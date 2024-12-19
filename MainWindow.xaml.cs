﻿using CameraExample.Config;
using CameraExample.Core;
using CameraExample.Settings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using UMapx.Distribution;
using UMapx.Imaging;
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

        //private readonly IVideoSource _videoSource;
        private IVideoSource _videoSource;
        private static readonly object _locker = new();
        private CameraSettings _cameraSettings;
        private Bitmap _frame;

        ////private int _currentExposure = -3; // Подходит для большинства стандартных условий
        //private int _currentExposure; // Подходит для большинства стандартных условий
        ////private int _currentBrightness = 8; // Лёгкая компенсация для большинства камер
        //private int _currentBrightness, currentBrightness; // Лёгкая компенсация для большинства камер
        ////private const int TargetBrightnessMin = 100; // Минимальная целевая яркость
        ////private const int TargetBrightnessMax = 150; // Максимальная целевая яркость
        ////private int _currentContrast = 50; // Нейтральное значение для большинства случаев
        //private int _currentContrast; // Нейтральное значение для большинства случаев
        ////private int _currentHue = 0;              // Нейтральный оттенок
        private int _currentHue;              // Нейтральный оттенок
        ////private int _currentWhiteBalance = 4500; // Нейтральная цветовая температура
        //private int _currentWhiteBalance; // Нейтральная цветовая температура

        private IniFile ini;
        private int targetExposure;
        private int targetBrightness;
        private int targetHue;
        private int targetContrast;
        private int targetWhiteBalance;
        private int targetSaturation;

        //from config
        private string iniFilePath = Path.Combine(AppContext.BaseDirectory, "CameraConfig.ini"); //путь конфиг файла
        private System.Drawing.Point _redMarkerPosition, _greenMarkerPosition, _blueMarkerPosition;
        private int indicatorSize; // Размер цветовой точки места установки маркера




        #endregion

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing;

            //подтягиваются параметры поумолчанию для начальной конфигурации
            //создается конф файл если его не было
            LoadCameraSettings(iniFilePath);
            LoadIndicatorSettings(iniFilePath);

            _videoSource = VideoSourceUtils.GetVideoDevice(_cameraSettings);

            if (_videoSource != null)
            {
                _videoSource.NewFrame += OnNewFrame;
                _videoSource.Start();
                Console.WriteLine("Video source has been successfully started");
            }

            if (File.Exists(iniFilePath) && new FileInfo(iniFilePath).Length != 0)
            {
                // Получаем текущее значение экспозиции
                ini = new IniFile(iniFilePath);
                targetExposure = int.Parse(ini.Read("Exposure", "Value")); // Целевое значение из конфигурации
                targetBrightness = int.Parse(ini.Read("Brightness", "Value")); // Целевое значение из конфигурации
                targetHue = int.Parse(ini.Read("Hue", "Value")); // Целевое значение из конфигурации
                targetContrast = int.Parse(ini.Read("Contrast", "Value")); // Целевое значение из конфигурации
                targetWhiteBalance = int.Parse(ini.Read("WhiteBalance", "Value")); // Целевое значение из конфигурации
                targetSaturation = int.Parse(ini.Read("Saturation", "Value")); // Целевое значение из конфигурации
            }
            _ = UpdateCameraSettingsAsync();
        }

        #endregion

        #region Camera and Markers Settings (config file)

        //Метод загружает поумолчанию в свойства камеры Id камеры и Id разрешения
        private void LoadCameraSettings(string filePath)
        {
            var ini = new IniFile(filePath);

            _cameraSettings = new CameraSettings
            {
                CameraId = int.Parse(ini.Read("Camera", "CameraId", "1")),
                ResolutionId = int.Parse(ini.Read("Camera", "ResolutionId", "0")),
            };
        }

        //Метод загружает поумолчанию координаты цв маркеров; размер указателя
        private void LoadIndicatorSettings(string filePath)
        {
            var ini = new IniFile(filePath);

            // Load marker positions
            _redMarkerPosition = new System.Drawing.Point(
                int.Parse(ini.Read("MarkerPositions", "RedMarkerX", "40")),
                int.Parse(ini.Read("MarkerPositions", "RedMarkerY", "40")));

            _greenMarkerPosition = new System.Drawing.Point(
                int.Parse(ini.Read("MarkerPositions", "GreenMarkerX", "600")),
                int.Parse(ini.Read("MarkerPositions", "GreenMarkerY", "40")));

            _blueMarkerPosition = new System.Drawing.Point(
                int.Parse(ini.Read("MarkerPositions", "BlueMarkerX", "300")),
                int.Parse(ini.Read("MarkerPositions", "BlueMarkerY", "400")));


            indicatorSize = int.Parse(ini.Read("PointerSize", "IndicatorSize", "20"));
        }

        //Метод записывает в конфигурационный файл "CameraConfig.ini" настройки камеры:
        //заданные в LoadCameraSettings (CameraId=1; ResolutionId=0),
        //заданные в LoadIndicatorSettings (координаты цв маркеров; размер указателя)
        //сделанные через драйвер "amcap.exe" (параметры: Exposure, Brightness, Hue, Contrast, WhiteBalance, Saturation)
        private void SaveCameraSettingsToIni(string filePath)
        {
            // Используем StringBuilder для создания содержимого INI-файла
            var iniContent = new StringBuilder();

            // Сохранение настроек камеры
            iniContent.AppendLine("[Camera]");
            iniContent.AppendLine($"CameraId={_cameraSettings.CameraId}");
            iniContent.AppendLine($"ResolutionId={_cameraSettings.ResolutionId}");

            // Сохранение свойств управления камерой
            iniContent.AppendLine("[Exposure]");
            iniContent.AppendLine($"CameraControlProperty=Exposure");
            iniContent.AppendLine($"Value={GetCameraProperty("Exposure").value}");
            iniContent.AppendLine($"CameraControlFlag={GetCameraProperty("Exposure").flags}");

            // Сохранение свойств ProcAmp камеры (настройки изображения)
            iniContent.AppendLine("[Brightness]");
            iniContent.AppendLine($"VideoProcAmpProperty=Brightness");
            iniContent.AppendLine($"Value={GetProcAmpProperty("Brightness").value}");
            iniContent.AppendLine($"VideoProcAmpFlag={GetProcAmpProperty("Brightness").flags}");

            iniContent.AppendLine("[Hue]");
            iniContent.AppendLine($"VideoProcAmpProperty=Hue");
            iniContent.AppendLine($"Value={GetProcAmpProperty("Hue").value}");
            iniContent.AppendLine($"VideoProcAmpFlag={GetProcAmpProperty("Hue").flags}");

            iniContent.AppendLine("[Contrast]");
            iniContent.AppendLine($"VideoProcAmpProperty=Contrast");
            iniContent.AppendLine($"Value={GetProcAmpProperty("Contrast").value}");
            iniContent.AppendLine($"VideoProcAmpFlag={GetProcAmpProperty("Contrast").flags}");

            iniContent.AppendLine("[WhiteBalance]");
            iniContent.AppendLine($"VideoProcAmpProperty=WhiteBalance");
            iniContent.AppendLine($"Value={GetProcAmpProperty("WhiteBalance").value}");
            iniContent.AppendLine($"VideoProcAmpFlag={GetProcAmpProperty("WhiteBalance").flags}");

            iniContent.AppendLine("[Saturation]");
            iniContent.AppendLine($"VideoProcAmpProperty=Saturation");
            iniContent.AppendLine($"Value={GetProcAmpProperty("Saturation").value}");
            iniContent.AppendLine($"VideoProcAmpFlag={GetProcAmpProperty("Saturation").flags}");

            // Сохранение позиций маркеров
            iniContent.AppendLine("[MarkerPositions]");
            iniContent.AppendLine($"RedMarkerX={_redMarkerPosition.X}");
            iniContent.AppendLine($"RedMarkerY={_redMarkerPosition.Y}");
            iniContent.AppendLine($"GreenMarkerX={_greenMarkerPosition.X}");
            iniContent.AppendLine($"GreenMarkerY={_greenMarkerPosition.Y}");
            iniContent.AppendLine($"BlueMarkerX={_blueMarkerPosition.X}");
            iniContent.AppendLine($"BlueMarkerY={_blueMarkerPosition.Y}");

            // Сохранение размера указателя
            iniContent.AppendLine("[PointerSize]");
            iniContent.AppendLine($"IndicatorSize={indicatorSize}");

            // Сохранение позиций маркеров
            var redMarkerData = GetMarkerData(_redMarkerPosition);
            var greenMarkerData = GetMarkerData(_greenMarkerPosition);
            var blueMarkerData = GetMarkerData(_blueMarkerPosition);

            iniContent.AppendLine("[MarkerParameters]");
            iniContent.AppendLine($"RedMarkerColor={redMarkerData.Color}");
            iniContent.AppendLine($"RedMarkerBrightnessR={redMarkerData.BrightnessR}");
            iniContent.AppendLine($"RedMarkerBrightnessG={redMarkerData.BrightnessG}");
            iniContent.AppendLine($"RedMarkerBrightnessB={redMarkerData.BrightnessB}");

            iniContent.AppendLine($"GreenMarkerColor={greenMarkerData.Color}");
            iniContent.AppendLine($"GreenMarkerBrightnessR={greenMarkerData.BrightnessR}");
            iniContent.AppendLine($"GreenMarkerBrightnessG={greenMarkerData.BrightnessG}");
            iniContent.AppendLine($"GreenMarkerBrightnessB={greenMarkerData.BrightnessB}");

            iniContent.AppendLine($"BlueMarkerColor={blueMarkerData.Color}");
            iniContent.AppendLine($"BlueMarkerBrightnessR={blueMarkerData.BrightnessR}");
            iniContent.AppendLine($"BlueMarkerBrightnessG={blueMarkerData.BrightnessG}");
            iniContent.AppendLine($"BlueMarkerBrightnessB={blueMarkerData.BrightnessB}");

            // Запись в файл
            File.WriteAllText(filePath, iniContent.ToString());
        }


        private (string Color, int BrightnessR, int BrightnessG, int BrightnessB) GetMarkerData(System.Drawing.Point position)
        {
            using var frame = Frame;

            // Определение зон исключения
            var exclusionZones = new List<Rectangle>
                {
                    new Rectangle(_redMarkerPosition.X - indicatorSize / 2, _redMarkerPosition.Y - indicatorSize / 2, indicatorSize, indicatorSize),
                    new Rectangle(_greenMarkerPosition.X - indicatorSize / 2, _greenMarkerPosition.Y - indicatorSize / 2, indicatorSize, indicatorSize),
                    new Rectangle(_blueMarkerPosition.X - indicatorSize / 2, _blueMarkerPosition.Y - indicatorSize / 2, indicatorSize, indicatorSize)
                };

            var markerData = GetMarkerColor(frame, position, indicatorSize + 30, exclusionZones);

            return (markerData);
        }


        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) // Проверяем, была ли нажата клавиша Enter
            {
                SaveCameraSettingsToIni(iniFilePath);
                MessageBox.Show("Настройки сохранены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }



        #endregion

        #region Update Camera Settings

        private async Task UpdateCameraSettingsAsync()
        {
                while (true)
                {

                using var frame = Frame;
                if (frame == null) continue;

                //    // Определение зон исключения
                //    var exclusionZones = new List<Rectangle>
                //{
                //    new Rectangle(_redMarkerPosition.X - indicatorSize / 2, _redMarkerPosition.Y - indicatorSize / 2, indicatorSize, indicatorSize),
                //    new Rectangle(_greenMarkerPosition.X - indicatorSize / 2, _greenMarkerPosition.Y - indicatorSize / 2, indicatorSize, indicatorSize),
                //    new Rectangle(_blueMarkerPosition.X - indicatorSize / 2, _blueMarkerPosition.Y - indicatorSize / 2, indicatorSize, indicatorSize)
                //};

                // Анализируем цвет бублика в заданной позиции
                //var redMarkerData = GetMarkerColor(frame, _redMarkerPosition, indicatorSize + 30, exclusionZones);
                var redMarkerData = GetMarkerData(_redMarkerPosition);
                Console.WriteLine($"Red Marker Color: {redMarkerData.Color}, BrightnessR: {redMarkerData.BrightnessR}, BrightnessG: {redMarkerData.BrightnessG}, BrightnessB: {redMarkerData.BrightnessB}");
                //var greenMarkerData = GetMarkerColor(frame, _greenMarkerPosition, indicatorSize + 30, exclusionZones);
                var greenMarkerData = GetMarkerData(_greenMarkerPosition);
                Console.WriteLine($"Green Marker Color: {greenMarkerData.Color}, BrightnessR: {greenMarkerData.BrightnessR}, BrightnessG: {greenMarkerData.BrightnessG}, BrightnessB: {greenMarkerData.BrightnessB}");
                //var blueMarkerData = GetMarkerColor(frame, _blueMarkerPosition, indicatorSize + 30, exclusionZones);
                var blueMarkerData = GetMarkerData(_blueMarkerPosition);
                Console.WriteLine($"Blue Marker Color: {blueMarkerData.Color}, BrightnessR: {blueMarkerData.BrightnessR}, BrightnessG: {blueMarkerData.BrightnessG}, BrightnessB: {blueMarkerData.BrightnessB}");


                if (File.Exists(iniFilePath) && new FileInfo(iniFilePath).Length != 0)
                {
                    Console.WriteLine($"INI file - yes.");

                    lock (_locker)
                    {
                        UpdateExposure(iniFilePath, redMarkerData.BrightnessR, redMarkerData.BrightnessG, redMarkerData.BrightnessB,
                        greenMarkerData.BrightnessR, greenMarkerData.BrightnessG, greenMarkerData.BrightnessB,
                        blueMarkerData.BrightnessR, blueMarkerData.BrightnessG, blueMarkerData.BrightnessB);

                        UpdateBrightness(iniFilePath, redMarkerData.BrightnessR, redMarkerData.BrightnessG, redMarkerData.BrightnessB,
                        greenMarkerData.BrightnessR, greenMarkerData.BrightnessG, greenMarkerData.BrightnessB,
                        blueMarkerData.BrightnessR, blueMarkerData.BrightnessG, blueMarkerData.BrightnessB);

                        UpdateHue(redMarkerData.BrightnessR, redMarkerData.BrightnessG, redMarkerData.BrightnessB,
                        greenMarkerData.BrightnessR, greenMarkerData.BrightnessG, greenMarkerData.BrightnessB,
                        blueMarkerData.BrightnessR, blueMarkerData.BrightnessG, blueMarkerData.BrightnessB);

                        UpdateWhiteBalance(redMarkerData.BrightnessR, redMarkerData.BrightnessG, redMarkerData.BrightnessB,
                        greenMarkerData.BrightnessR, greenMarkerData.BrightnessG, greenMarkerData.BrightnessB,
                        blueMarkerData.BrightnessR, blueMarkerData.BrightnessG, blueMarkerData.BrightnessB);

                    //    UpdateContrast(redMarkerData.BrightnessR, redMarkerData.BrightnessG, redMarkerData.BrightnessB,
                    //    greenMarkerData.BrightnessR, greenMarkerData.BrightnessG, greenMarkerData.BrightnessB,
                    //    blueMarkerData.BrightnessR, blueMarkerData.BrightnessG, blueMarkerData.BrightnessB);
                    }


                    } else
                    {
                        Console.WriteLine($"INI file - no. To create the file, use AMCAP.exe to set up parameters and click \"Enter\" on the image");
                    }

                    await Task.Delay(2500);
                    
                }
        }

        private int TargetAverageMarkerBrightness(string filePath)
        {
            var ini = new IniFile(filePath);

            // Считываем параметры яркости маркеров из файла конфигурации
            int[] redMarkerBrightness = {
        int.Parse(ini.Read("MarkerParameters", "RedMarkerBrightnessR")),
        int.Parse(ini.Read("MarkerParameters", "RedMarkerBrightnessG")),
        int.Parse(ini.Read("MarkerParameters", "RedMarkerBrightnessB"))
    };

            int[] greenMarkerBrightness = {
        int.Parse(ini.Read("MarkerParameters", "GreenMarkerBrightnessR")),
        int.Parse(ini.Read("MarkerParameters", "GreenMarkerBrightnessG")),
        int.Parse(ini.Read("MarkerParameters", "GreenMarkerBrightnessB"))
    };

            int[] blueMarkerBrightness = {
        int.Parse(ini.Read("MarkerParameters", "BlueMarkerBrightnessR")),
        int.Parse(ini.Read("MarkerParameters", "BlueMarkerBrightnessG")),
        int.Parse(ini.Read("MarkerParameters", "BlueMarkerBrightnessB"))
    };

            // Рассчитываем целевую среднюю яркость
            int targetAverageMarkerBrightness = (redMarkerBrightness.Sum() + greenMarkerBrightness.Sum() + blueMarkerBrightness.Sum()) / 9;

            return targetAverageMarkerBrightness;
        }

        private void UpdateExposure(string filePath, int redR, int redG, int redB, int greenR, int greenG, int greenB, int blueR, int blueG, int blueB)
        {
            // Рассчитываем среднюю яркость по всем маркерам
            int currentAverageMarkerBrightness = (redR + redG + redB + greenR + greenG + greenB + blueR + blueG + blueB) / 9;

            int targetAverageMarkerBrightness = TargetAverageMarkerBrightness(filePath);

            Console.WriteLine($"currentAverageMarkerBrightness = {currentAverageMarkerBrightness}");
            Console.WriteLine($"targetAverageMarkerBrightness = {targetAverageMarkerBrightness}");

            // Задаем порог, чтобы изображение не мигало
            if (Math.Abs(currentAverageMarkerBrightness - targetAverageMarkerBrightness) > 20) {
            // Корректируем значение экспозиции
            if (currentAverageMarkerBrightness < targetAverageMarkerBrightness)
            {
                targetExposure = Math.Clamp(targetExposure + 1, -8, 0);
            }
            else if (currentAverageMarkerBrightness > targetAverageMarkerBrightness)
            {
                targetExposure = Math.Clamp(targetExposure - 1, -8, 0);
            }
        }

            // Устанавливаем новое значение экспозиции
            SetCameraProperty("Exposure", targetExposure);
        }


        private void UpdateBrightness(string filePath, int redR, int redG, int redB, int greenR, int greenG, int greenB, int blueR, int blueG, int blueB)
        {
            var ini = new IniFile(filePath);

            // Расчет взвешенной яркости для каждого маркера
            double redWeight = 0.299;
            double greenWeight = 0.587;
            double blueWeight = 0.114;

            double weightedRed = (redR * redWeight + redG * greenWeight + redB * blueWeight) / 3.0;
            double weightedGreen = (greenR * redWeight + greenG * greenWeight + greenB * blueWeight) / 3.0;
            double weightedBlue = (blueR * redWeight + blueG * greenWeight + blueB * blueWeight) / 3.0;

            // Рассчитываем среднюю взвешенную яркость
            double currentAverageWeightedBrightness = (weightedRed + weightedGreen + weightedBlue) / 3.0;

            int targetAverageMarkerBrightness = TargetAverageMarkerBrightness(filePath);

            // Оценка отклонения и корректировка яркости
            if (currentAverageWeightedBrightness < targetAverageMarkerBrightness)
            {
                targetBrightness = Math.Clamp(targetBrightness + 8, -64, 64);
            }
            else if (currentAverageWeightedBrightness > targetAverageMarkerBrightness)
            {
                targetBrightness = Math.Clamp(targetBrightness - 8, -64, 64);
            }

            // Устанавливаем новое значение яркости
            SetProcAmpProperty("Brightness", targetBrightness);
        }


        private void UpdateHue(int redR, int redG, int redB, int greenR, int greenG, int greenB, int blueR, int blueG, int blueB)
        {
            // Рассчитываем корректировку оттенка (Hue)
            int hueAdjustment = CalculateHueAdjustment(redR, redG, redB, greenR, greenG, greenB, blueR, blueG, blueB);

            //_currentHue = GetProcAmpProperty("Hue").value;



        // Плавное сглаживание изменений оттенка
        int smoothedHue = (int)(targetHue * 0.8 + hueAdjustment * 0.2);

            // Применяем значение
            targetHue = Math.Clamp(smoothedHue, targetHue - 10, targetHue + 10);
            SetProcAmpProperty("Hue", targetHue);
        }

        //private void UpdateContrast(int red, int green, int blue)
            private void UpdateContrast(int redR, int redG, int redB, int greenR, int greenG, int greenB, int blueR, int blueG, int blueB)
        {
            //_currentContrast = GetProcAmpProperty("Contrast").value;

            // Рассчитываем корректировку контраста
            int contrastAdjustment = CalculateContrastAdjustment(redR, redG, redB, greenR, greenG, greenB, blueR, blueG, blueB);

            // Плавная корректировка контраста
            targetContrast = (int)(targetContrast * 0.8 + contrastAdjustment * 0.2);

            // Ограничиваем значение вблизи целевого
            targetContrast = Math.Clamp(targetContrast, targetContrast - 5, targetContrast + 5);
            SetProcAmpProperty("Contrast", targetContrast);
        }

        //private void UpdateWhiteBalance(int redR, int redG, int redB, int greenR, int greenG, int greenB, int blueR, int blueG, int blueB)
        ////private void UpdateWhiteBalance(int red, int green, int blue)
        //{
        //    // Рассчитываем среднее значение каналов
        //    int average = (red + green + blue) / 3;

        //    // Рассчитываем отклонения
        //    int deltaRed = red - average;
        //    int deltaBlue = blue - average;

        //    _currentWhiteBalance = GetProcAmpProperty("WhiteBalance").value;

        //    // Рассчитываем коррекцию для баланса белого
        //    int whiteBalanceCorrection = _currentWhiteBalance + deltaBlue * 10 - deltaRed * 10;

        //    // Ограничиваем диапазон значений
        //    whiteBalanceCorrection = Math.Clamp(whiteBalanceCorrection, _currentWhiteBalance - 500, _currentWhiteBalance + 500);

        //    // Плавное сглаживание изменений
        //    _currentWhiteBalance = (int)(_currentWhiteBalance * 0.8 + whiteBalanceCorrection * 0.2);

        //    // Применяем значение
        //    SetProcAmpProperty("WhiteBalance", _currentWhiteBalance);
        //}

        private void UpdateWhiteBalance(int redR, int redG, int redB, int greenR, int greenG, int greenB, int blueR, int blueG, int blueB)
        {
            // Рассчитываем средние значения каждого канала
            int averageRed = (redR + redG + redB) / 3;
            int averageGreen = (greenR + greenG + greenB) / 3;
            int averageBlue = (blueR + blueG + blueB) / 3;

            // Рассчитываем общее среднее значение всех каналов
            int overallAverage = (averageRed + averageGreen + averageBlue) / 3;

            // Рассчитываем отклонения
            int deltaRed = averageRed - overallAverage;
            int deltaBlue = averageBlue - overallAverage;

            //// Получаем текущее значение баланса белого
            //_currentWhiteBalance = GetProcAmpProperty("WhiteBalance").value;

            // Рассчитываем коррекцию для баланса белого
            int whiteBalanceCorrection = targetWhiteBalance + deltaBlue * 10 - deltaRed * 10;

            // Ограничиваем диапазон значений
            whiteBalanceCorrection = Math.Clamp(whiteBalanceCorrection, targetWhiteBalance - 500, targetWhiteBalance + 500);

            // Плавное сглаживание изменений
            targetWhiteBalance = (int)(targetWhiteBalance * 0.8 + whiteBalanceCorrection * 0.2);

            // Применяем новое значение
            SetProcAmpProperty("WhiteBalance", targetWhiteBalance);
        }

        private void UpdateSaturation(int redR, int redG, int redB, int greenR, int greenG, int greenB, int blueR, int blueG, int blueB)
        {
            // Рассчитываем средние значения каждого канала
            int averageRed = (redR + redG + redB) / 3;
            int averageGreen = (greenR + greenG + greenB) / 3;
            int averageBlue = (blueR + blueG + blueB) / 3;

            // Рассчитываем общую среднюю яркость
            int overallBrightness = (averageRed + averageGreen + averageBlue) / 3;

            // Рассчитываем насыщенность как разницу между максимальной и минимальной яркостью
            int minBrightness = Math.Min(averageRed, Math.Min(averageGreen, averageBlue));
            int maxBrightness = Math.Max(averageRed, Math.Max(averageGreen, averageBlue));
            int calculatedSaturation = maxBrightness - minBrightness;

            //// Получаем текущее 

            // Рассчитываем целевое значение насыщенности
            targetSaturation = targetSaturation + (calculatedSaturation - overallBrightness) / 2;

            // Ограничиваем диапазон значений
            targetSaturation = Math.Clamp(targetSaturation, targetSaturation - 20, targetSaturation + 20);

            // Плавное сглаживание изменений
            int smoothedSaturation = (int)(targetSaturation * 0.8 + targetSaturation * 0.2);

            // Обновляем текущее значение насыщенности
            SetProcAmpProperty("Saturation", smoothedSaturation);
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
        private (int value, CameraControlFlags flags) GetCameraProperty(string property)
        {
            if (_videoSource is VideoCaptureDevice videoDevice)
            {
                if (Enum.TryParse(typeof(CameraControlProperty), property, out var propertyEnum))
                {
                    videoDevice.GetCameraProperty(
                        (CameraControlProperty)propertyEnum,
                        out int value,
                        out CameraControlFlags controlFlags);

                    return (value, controlFlags); // Возвращаем оба значения через кортеж
                }
                else
                {
                    throw new ArgumentException($"Invalid property name: {property}", nameof(property));
                }
            }

            throw new InvalidOperationException("Video source is not a VideoCaptureDevice.");
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

        private (int value, VideoProcAmpFlags flags) GetProcAmpProperty(string property)
        {
            if (_videoSource is VideoCaptureDevice videoDevice)
            {
                if (Enum.TryParse(typeof(VideoProcAmpProperty), property, out var propertyEnum))
                {
                    videoDevice.GetVideoProcAmpProperty(
                        (VideoProcAmpProperty)propertyEnum,
                        out int value,
                        out VideoProcAmpFlags controlFlags);

                    return (value, controlFlags); // Возвращаем оба значения через кортеж
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

        private static (string Color, int BrightnessR, int BrightnessG, int BrightnessB) GetMarkerColor(Bitmap frame, System.Drawing.Point position, int size, List<Rectangle> exclusionZones)
        {
            // Конвертируем Bitmap в структуру RGB с помощью метода ToRGB
            float[][,] rgbData = frame.ToRGB();

            int frameWidth = frame.Width;
            int frameHeight = frame.Height;
            int halfSize = size / 2;

            int totalR = 0, totalG = 0, totalB = 0, pixelCount = 0;

            // Проходим по заданной области вокруг позиции маркера
            for (int y = Math.Max(0, position.Y - halfSize); y < Math.Min(frameHeight, position.Y + halfSize); y++)
            {
                for (int x = Math.Max(0, position.X - halfSize); x < Math.Min(frameWidth, position.X + halfSize); x++)
                {
                    var pixelPos = new System.Drawing.Point(x, y);

                    // Пропускаем пиксели, попадающие в зоны исключения
                    if (exclusionZones.Exists(zone => zone.Contains(pixelPos)))
                        continue;

                    // Извлекаем значения R, G, B из rgbData
                    int red = (int)(rgbData[0][y, x] * 255);
                    int green = (int)(rgbData[1][y, x] * 255);
                    int blue = (int)(rgbData[2][y, x] * 255);

                    totalR += red;
                    totalG += green;
                    totalB += blue;
                    pixelCount++;
                }
            }

            if (pixelCount == 0)
                return ("Unknown", 0, 0, 0); // Если нет пикселей для анализа

            // Рассчитываем средние значения интенсивностей
            int avgR = totalR / pixelCount;
            int avgG = totalG / pixelCount;
            int avgB = totalB / pixelCount;

            //Console.WriteLine($"avgR {avgR} avgG {avgG} avgB {avgB}");

            // Определение цвета на основе RGB
            if (avgR > avgG && avgR > avgB) return ("Red", avgR, avgG, avgB);
            if (avgG > avgR && avgG > avgB) return ("Green", avgR, avgG, avgB);
            if (avgB > avgR && avgB > avgG) return ("Blue", avgR, avgG, avgB);

            return ("Unknown", avgR, avgG, avgB);
        }

        #endregion

        #region Static methods (calculations)

        //private static int CalculateHueAdjustment(int redMarker, int greenMarker, int blueMarker)
        private static int CalculateHueAdjustment(int redR, int redG, int redB, int greenR, int greenG, int greenB, int blueR, int blueG, int blueB)
        {
            const int IdealRed = 255;
            const int IdealGreen = 255;
            const int IdealBlue = 255;

            // Средние значения цветов
            int avgRed = (redR + redG + redB) / 3;
            int avgGreen = (greenR + greenG + greenB) / 3;
            int avgBlue = (blueR + blueG + blueB) / 3;

            // Отклонения от идеальных значений
            int redDelta = IdealRed - avgRed;
            int greenDelta = IdealGreen - avgGreen;
            int blueDelta = IdealBlue - avgBlue;

            // Расчет коррекции
            int hueAdjustment = (redDelta - greenDelta + blueDelta) / 3;

            // Ограничение значений
            return Math.Clamp(hueAdjustment, -30, 30);
        }

        private int CalculateContrastAdjustment(int redR, int redG, int redB, int greenR, int greenG, int greenB, int blueR, int blueG, int blueB)
        {
            // Вычисляем минимальное и максимальное значения цвета
            int minColor = Math.Min(redR, Math.Min(greenR, blueR));
            int maxColor = Math.Max(redR, Math.Max(greenR, blueR));
            int brightnessRange = maxColor - minColor;

            //// Получаем текущую яркость
            //int currentBrightness = GetProcAmpProperty("Brightness").value;

            // Определяем целевой контраст
            //int targetContrast = currentContrast;

            if (brightnessRange < targetBrightness - 10)
            {
                targetContrast += 10;
            }
            else if (brightnessRange > targetBrightness + 10)
            {
                targetContrast -= 10;
            }

            // Ограничиваем целевой контраст в пределах допустимых значений
            return Math.Clamp(targetContrast, 0, 100);
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

            // Draw markers based on positions
            var blueMarkerRect = new Rectangle(redMarker.X - indicatorSize / 2, redMarker.Y - indicatorSize / 2, indicatorSize, indicatorSize);
            var greenMarkerRect = new Rectangle(greenMarker.X - indicatorSize / 2, greenMarker.Y - indicatorSize / 2, indicatorSize, indicatorSize);
            var redMarkerRect = new Rectangle(blueMarker.X - indicatorSize / 2, blueMarker.Y - indicatorSize / 2, indicatorSize, indicatorSize);

            //var redMarkerRect = new Rectangle(redMarker.X - indicatorSize / 2, redMarker.Y - indicatorSize / 2, indicatorSize, indicatorSize);
            //var greenMarkerRect = new Rectangle(greenMarker.X - indicatorSize / 2, greenMarker.Y - indicatorSize / 2, indicatorSize, indicatorSize);
            //var blueMarkerRect = new Rectangle(blueMarker.X - indicatorSize / 2, blueMarker.Y - indicatorSize / 2, indicatorSize, indicatorSize);

            g.FillEllipse(System.Drawing.Brushes.Red, redMarkerRect);
            g.FillEllipse(System.Drawing.Brushes.Green, greenMarkerRect);
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

