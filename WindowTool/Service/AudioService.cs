using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using WindowTool.Model;

namespace WindowTool.Service {
    internal class AudioService {
        public async Task MuteProcess(ProcessInfo process, AudioSessionControl session, bool isMute, int delayMuteSec, int fadeDurationSec, CancellationToken ctsToken) {
            try {
                await Task.Delay(delayMuteSec*1000, ctsToken);
                if(isMute) {
                    process.Volume = session.SimpleAudioVolume.Volume;
                    try {
                        int step = fadeDurationSec * 1000 / 50; // 50 ms per step
                        for (int i = 0; i < 50; i++) {
                            ctsToken.ThrowIfCancellationRequested();
                            float newVolume = process.Volume.Value * (1 - (i + 1) / 50.0f);
                            session.SimpleAudioVolume.Volume = newVolume;
                            await Task.Delay(step, ctsToken);
                        }
                    }
                    catch (TaskCanceledException) {

                    }
                }
            }
            catch (TaskCanceledException) {
                return;
            }
        }
    }
}
