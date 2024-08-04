using System;

namespace OpenWindows
{
    [Serializable]
    internal class Config
    {
        public string UserName { get; set; } = "hp";
        public string AppClassName { get; set; } = "ApplicationFrameWindow";
        public bool AutoRestart { get; set; } = true;
        public bool OpenAdmin { get; set; } = false;
    }
}
