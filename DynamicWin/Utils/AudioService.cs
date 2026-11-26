using DynamicWin.Main;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace DynamicWin.Utils
{
    public class AudioOutput
    {
        public string Id = "";
        public string Name = "";
    }

    /// <summary>
    /// Ses seviyesi, sessiz ve çıkış cihazı seçimi (Ses widget'ı). Tüm COM çağrıları UI thread'indedir; seviye ve
    /// sessiz durumu bildirimlerle önbelleklenir, her karede COM'a gidilmez.
    /// Varsayılan çıkış değişince (buradan ya da Windows'tan) <see cref="DynamicWinMain.defaultDevice"/> güncellenir,
    /// böylece V1 ses göstergesi ve sessiz uyarısı yeni cihazı izler.
    /// Varsayılan cihazı değiştirmek için Windows'un belgelenmemiş ama yaygın kullanılan IPolicyConfig arayüzü kullanılır.
    /// </summary>
    public static class AudioService
    {
        public static float Volume { get; private set; } = 1f;
        public static bool Muted { get; private set; }
        public static string DefaultId { get; private set; } = "";
        public static List<AudioOutput> Outputs { get; private set; } = new List<AudioOutput>();

        /// <summary>Varsayılan çıkış değişti (yeni cihaz).</summary>
        public static event Action<MMDevice>? DefaultChanged;

        static MMDeviceEnumerator? enumerator;
        static MMDevice? device;
        static NotificationClient? client;
        static bool started;

        public static void Start()
        {
            if (started) return;
            started = true;
            try
            {
                enumerator = new MMDeviceEnumerator();
                client = new NotificationClient();
                enumerator.RegisterEndpointNotificationCallback(client);
                RefreshOutputs();
                AttachDefault();
            }
            catch (Exception e) { Debug.WriteLine("Ses servisi başlatılamadı: " + e.Message); }
        }

        public static void RefreshOutputs()
        {
            if (enumerator == null) return;
            try
            {
                Outputs = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                    .Select(d => new AudioOutput { Id = d.ID, Name = d.FriendlyName })
                    .OrderBy(o => o.Name).ToList();
            }
            catch (Exception e) { Debug.WriteLine("Ses cihazları okunamadı: " + e.Message); }
        }

        static void AttachDefault()
        {
            if (enumerator == null) return;
            try
            {
                if (device != null) device.AudioEndpointVolume.OnVolumeNotification -= OnVolume;
                device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                DefaultId = device.ID;
                Volume = device.AudioEndpointVolume.MasterVolumeLevelScalar;
                Muted = device.AudioEndpointVolume.Mute;
                device.AudioEndpointVolume.OnVolumeNotification += OnVolume;

                if (DynamicWinMain.defaultDevice?.ID != device.ID)
                {
                    DynamicWinMain.defaultDevice = device;
                    DefaultChanged?.Invoke(device);
                }
            }
            catch (Exception e) { Debug.WriteLine("Varsayılan ses cihazı okunamadı: " + e.Message); }
        }

        static void OnVolume(AudioVolumeNotificationData data)
        {
            Volume = data.MasterVolume;
            Muted = data.Muted;
        }

        public static void SetVolume(float value)
        {
            value = Math.Clamp(value, 0f, 1f);
            Volume = value;
            try
            {
                if (device == null) return;
                device.AudioEndpointVolume.MasterVolumeLevelScalar = value;
                if (Muted && value > 0) { device.AudioEndpointVolume.Mute = false; Muted = false; }
            }
            catch (Exception e) { Debug.WriteLine("Ses ayarlanamadı: " + e.Message); }
        }

        public static void ToggleMute()
        {
            try
            {
                if (device == null) return;
                Muted = !Muted;
                device.AudioEndpointVolume.Mute = Muted;
            }
            catch (Exception e) { Debug.WriteLine("Sessiz yapılamadı: " + e.Message); }
        }

        /// <summary>Seçilen cihazı tüm roller (konsol, multimedya, iletişim) için Windows varsayılan çıkışı yapar.</summary>
        public static void SetDefault(string id)
        {
            try
            {
                var policy = (IPolicyConfig)new CPolicyConfigClient();
                for (int role = 0; role < 3; role++) policy.SetDefaultEndpoint(id, role);
                Marshal.ReleaseComObject(policy);
                AttachDefault(); // bildirim de gelecek, ama arayüz hemen güncellensin
            }
            catch (Exception e) { Debug.WriteLine("Varsayılan cihaz değiştirilemedi: " + e.Message); }
        }

        /// <summary>Windows cihaz değişikliklerini UI thread'ine aktarır.</summary>
        class NotificationClient : IMMNotificationClient
        {
            static void OnUi(Action a) => Application.Current?.Dispatcher.BeginInvoke(a);

            public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
            {
                if (flow == DataFlow.Render && role == Role.Multimedia) OnUi(AttachDefault);
            }
            public void OnDeviceAdded(string pwstrDeviceId) => OnUi(RefreshOutputs);
            public void OnDeviceRemoved(string deviceId) => OnUi(RefreshOutputs);
            public void OnDeviceStateChanged(string deviceId, DeviceState newState) => OnUi(RefreshOutputs);
            public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
        }

        [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
        class CPolicyConfigClient { }

        [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPolicyConfig
        {
            [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr format);
            [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, int isDefault, IntPtr format);
            [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
            [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr endpointFormat, IntPtr mixFormat);
            [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, int isDefault, IntPtr defaultPeriod, IntPtr minPeriod);
            [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr period);
            [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
            [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
            [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr key, IntPtr value);
            [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr key, IntPtr value);
            [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
            [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, int visible);
        }
    }
}
