using CameraExample.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using UMapx.Video;
using UMapx.Video.DirectShow;

namespace CameraExample.Core
{
    public static class VideoSourceUtils
    {
        /// <summary>
        /// Returns configured camera device.
        /// </summary>
        /// <param name="cameraSettings">Camera settings</param>
        /// <param name="logger">Logger</param>
        /// <returns>IVideoSource</returns>
        public static IVideoSource GetVideoDevice(CameraSettings cameraSettings)
        {
            var monikerString = cameraSettings.MonikerString;
            var camIndex = cameraSettings.CameraId;
            var resIndex = cameraSettings.ResolutionId;

            // enumerate camera devices
            try
            {
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                int i = 0;
                foreach (var device in videoDevices)
                {
                    Console.WriteLine($"Device {i}: {device.Name}");
                    i++;
                }

                // check available cameras
                if (videoDevices.Count == 0)
                {
                    return null;
                }

                // device
                VideoCaptureDevice videoDevice = null;

                if (!string.IsNullOrEmpty(monikerString))
                {
                    var device = videoDevices.FirstOrDefault(x => x.MonikerString == monikerString);

                    if (device != null)
                    {
                        videoDevice = new VideoCaptureDevice(monikerString);
                    }
                }
                else
                {
                    videoDevice = new VideoCaptureDevice(videoDevices[camIndex].MonikerString);
                }

                //var videoDevice = new VideoCaptureDevice(!string.IsNullOrEmpty(monikerString) ? monikerString : videoDevices[camIndex].MonikerString);
                Console.WriteLine($"Selected device: {camIndex}");

                // resolution
                var videoCapabilities = videoDevice.VideoCapabilities;
                i = 0;
                foreach (var capabilty in videoCapabilities)
                {
                    Console.WriteLine($"Resolution {i}: {capabilty.FrameSize.Width} x {capabilty.FrameSize.Height}");
                    i++;
                }

                // amcap filters
                // applying camera control filters
                if (cameraSettings.CameraControlPropertySettings != null)
                {
                    foreach (var cameraControlPropertySettings in cameraSettings.CameraControlPropertySettings)
                    {
                        if (cameraControlPropertySettings?.Value != 0)
                        {
                            videoDevice.SetCameraProperty(
                                (CameraControlProperty)Enum.Parse(typeof(CameraControlProperty), cameraControlPropertySettings.CameraControlProperty),
                                cameraControlPropertySettings.Value,
                                (CameraControlFlags)Enum.Parse(typeof(CameraControlFlags), cameraControlPropertySettings.CameraControlFlag));
                        }
                    }
                }

                // applying camera amp filters
                if (cameraSettings.CameraProcAmpPropertySettings != null)
                {
                    foreach (var cameraProcAmpPropertySettings in cameraSettings.CameraProcAmpPropertySettings)
                    {
                        if (cameraProcAmpPropertySettings?.Value != 0)
                        {
                            videoDevice.SetVideoProcAmpProperty(
                                (VideoProcAmpProperty)Enum.Parse(typeof(VideoProcAmpProperty), cameraProcAmpPropertySettings.VideoProcAmpProperty),
                                cameraProcAmpPropertySettings.Value,
                                (VideoProcAmpFlags)Enum.Parse(typeof(VideoProcAmpFlags), cameraProcAmpPropertySettings.VideoProcAmpFlag));
                        }
                    }
                }

                videoDevice.VideoResolution = videoCapabilities.Length == 0 || videoCapabilities.Length < resIndex ? default : videoCapabilities[resIndex];
                Console.WriteLine($"Selected resolution: {resIndex}");
                return videoDevice;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        /// <summary>
        /// Retrieves current camera settings from the device.
        /// </summary>
        /// <param name="cameraId">Camera index</param>
        /// <returns>CameraSettings</returns>
        public static CameraSettings GetCameraSettingsFromDevice(int cameraId)
        {
            try
            {
                // Enumerate camera devices
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (cameraId >= videoDevices.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(cameraId), "Invalid camera index.");
                }

                // Select the device
                var videoDevice = new VideoCaptureDevice(videoDevices[cameraId].MonikerString);

                // Create CameraSettings object
                var cameraSettings = new CameraSettings
                {
                    CameraId = cameraId,
                    MonikerString = videoDevices[cameraId].MonikerString,
                    CameraProcAmpPropertySettings = new List<CameraProcAmpPropertySettings>(),
                    CameraControlPropertySettings = new List<CameraControlPropertySettings>()
                };

                // Retrieve CameraControl properties
                foreach (CameraControlProperty property in Enum.GetValues(typeof(CameraControlProperty)))
                {
                    int value = 0;
                    CameraControlFlags flag = 0;

                    if (videoDevice.GetCameraProperty(property, out value, out flag))
                    {
                        cameraSettings.CameraControlPropertySettings.Add(new CameraControlPropertySettings
                        {
                            CameraControlProperty = property.ToString(),
                            Value = value,
                            CameraControlFlag = flag.ToString()
                        });
                    }
                }

                // Retrieve VideoProcAmp properties
                foreach (VideoProcAmpProperty property in Enum.GetValues(typeof(VideoProcAmpProperty)))
                {
                    int value = 0;
                    VideoProcAmpFlags flag = 0;

                    if (videoDevice.GetVideoProcAmpProperty(property, out value, out flag))
                    {
                        cameraSettings.CameraProcAmpPropertySettings.Add(new CameraProcAmpPropertySettings
                        {
                            VideoProcAmpProperty = property.ToString(),
                            Value = value,
                            VideoProcAmpFlag = flag.ToString()
                        });
                    }
                }

                // Retrieve resolution settings
                var videoCapabilities = videoDevice.VideoCapabilities;
                cameraSettings.ResolutionId = Array.IndexOf(videoCapabilities, videoDevice.VideoResolution);

                return cameraSettings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving camera settings: {ex.Message}");
                return null;
            }
        }
    }
}
