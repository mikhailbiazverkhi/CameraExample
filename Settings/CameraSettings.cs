using System.Collections.Generic;

namespace CameraExample.Settings
{
    public class CameraSettings
    {
        /// <summary>
        /// Gets or sets camera id.
        /// </summary>
        public int CameraId { get; set; } = 0;

        /// <summary>
        /// Gets or sets resolution id.
        /// </summary>
        public int ResolutionId { get; set; } = 0;

        /// <summary>
        /// Gets or sets moniker string.
        /// </summary>
        public string MonikerString { get; set; }

        /// <summary>
        /// Gets or sets camera control property settings.
        /// </summary>
        public List<CameraControlPropertySettings> CameraControlPropertySettings { get; set; }

        /// <summary>
        /// Gets or sets camera amp property settings.
        /// </summary>
        public List<CameraProcAmpPropertySettings> CameraProcAmpPropertySettings { get; set; }
    }
}
