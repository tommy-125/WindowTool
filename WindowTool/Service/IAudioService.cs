using System;
using System.Collections.Generic;
using System.Text;

namespace WindowTool.Service {
    internal interface IAudioService {
        bool MuteProcess(int Pid, int second)

        void DelayMute();
    }
}
