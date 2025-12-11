using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Text;
using WindowTool.Model;

namespace WindowTool.Service {
    internal static class AudioHelper {
        public static AudioSessionControl? FindAudioSession(int pid) {
            var enumerator = new MMDeviceEnumerator();
            MMDevice device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessionManager = device.AudioSessionManager;
            for (int i = 0; i < sessionManager.Sessions.Count; i++) {
                var session = sessionManager.Sessions[i];
                if (session.GetProcessID == pid) {
                    return session;
                }
            }
            return null;
        }
    }
}
