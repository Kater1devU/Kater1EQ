using System;
using System.Runtime.InteropServices;

namespace Kater1EQ.Services
{
    // Minimal wrapper around Windows Core Audio API to read master volume scalar (0.0 - 1.0)
    public class SystemVolumeService
    {
        private const string IID_IAudioEndpointVolume = "5CDF2C82-841E-4546-9722-0CF74078229A";

        public float GetMasterVolumeScalar()
        {
            try
            {
                var enumerator = new MMDeviceEnumeratorCom() as IMMDeviceEnumerator;
                if (enumerator == null) return 1.0f;

                enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device);
                if (device == null) return 1.0f;

                var iid = new Guid(IID_IAudioEndpointVolume);
                device.Activate(ref iid, CLSCTX.ALL, IntPtr.Zero, out var obj);
                if (obj == null) return 1.0f;

                var epv = (IAudioEndpointVolume)obj;
                epv.GetMasterVolumeLevelScalar(out float level);
                // clamp
                if (level < 0) level = 0;
                if (level > 1) level = 1;
                return level;
            }
            catch
            {
                return 1.0f;
            }
        }

        #region COM Interop
        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorCom { }

        private enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
        private enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

        [Flags]
        private enum CLSCTX : uint
        {
            INPROC_SERVER = 0x1,
            INPROC_HANDLER = 0x2,
            LOCAL_SERVER = 0x4,
            INPROC_SERVER16 = 0x8,
            REMOTE_SERVER = 0x10,
            INPROC_HANDLER16 = 0x20,
            ALL = INPROC_SERVER | INPROC_HANDLER | LOCAL_SERVER | REMOTE_SERVER
        }

        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        private interface IMMDeviceEnumerator
        {
            int EnumAudioEndpoints(EDataFlow dataFlow, int dwStateMask, out IntPtr ppDevices);
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
            // rest not needed
        }

        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        private interface IMMDevice
        {
            int Activate(ref Guid iid, CLSCTX dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            // rest not needed
        }

        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        private interface IAudioEndpointVolume
        {
            // many methods; we only need this one
            int RegisterControlChangeNotify(IntPtr pNotify);
            int UnregisterControlChangeNotify(IntPtr pNotify);
            int GetChannelCount(out uint pnChannelCount);
            int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
            int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
            int GetMasterVolumeLevel(out float pfLevelDB);
            int GetMasterVolumeLevelScalar(out float pfLevel);
            int SetChannelVolumeLevel(uint nChannel, float fLevelDB, Guid pguidEventContext);
            int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, Guid pguidEventContext);
            int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, Guid pguidEventContext);
            int GetMute(out bool pbMute);
            int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
            int VolumeStepUp(Guid pguidEventContext);
            int VolumeStepDown(Guid pguidEventContext);
            int QueryHardwareSupport(out uint pdwHardwareSupportMask);
            int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
        }
        #endregion
    }
}
