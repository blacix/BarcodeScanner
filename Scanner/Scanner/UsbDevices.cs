using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Scanner.UsbDevices;

namespace Scanner
{

    class UsbDevices
    {

        public enum EventType
        {
            UNKNOWN, EMPTY, DEVICE_INSERTED, DEVICE_REMOVED
        }

        public static readonly int SCANNER_VID = 0x2e15;
        public static readonly int SCANNER_PID = 0x0003;


        private static readonly Guid GUID_DEVCLASS_USB = new Guid("{36fc9e60-c465-11cf-8056-444553540000}"); //USB device class GUID
        private static readonly Guid GUID_DEVCLASS_COM_PORT = new Guid("{4d36e978-e325-11ce-bfc1-08002be10318}"); //COM port class GUID

        private const string NAME = "Name";
        private const string DEVICE_ID = "DeviceID";

        private ManagementEventWatcher usbInsertWatcher;
        private ManagementEventWatcher usbRemoveWatcher;
        public OrderedDictionary UsbDevicesCollection { private set; get; }
        public string[] UsbDevicesFriendlyNames
        {
            get
            {
                string[] deviceNames = new string[this.UsbDevicesCollection.Count];
                int index = 0;
                foreach (UsbDeviceInfo deviceInfo in this.UsbDevicesCollection.Values) deviceNames[index++] = deviceInfo.FriendlyName;
                return deviceNames;
            }
        }

        public delegate void HandleUsbDevicesChanged(object sender, EventArgs e);
        public event HandleUsbDevicesChanged UsbDevicesChangedEvent = delegate { };

        public UsbDevices()
        {
            this.UsbDevicesCollection = new OrderedDictionary();
            fillUsbDevicesCollection();
            startWatchingUsb();
        }

        private void GloryApplicationInstance_OnApplicationExitEvent(object sender, EventArgs e)
        {
            stopWatchingUsb();
        }

        private void fillUsbDevicesCollection()
        {
            this.UsbDevicesCollection.Clear();
            foreach (ManagementObject managementObject in GetUsbComPorts())
            {
                UsbDeviceInfo deviceInfo = getDeviceInfo((string)managementObject.GetPropertyValue(NAME), (string)managementObject.GetPropertyValue(DEVICE_ID));
                if (deviceInfo != null && !this.UsbDevicesCollection.Contains(deviceInfo.DeviceSignature)) this.UsbDevicesCollection.Add(deviceInfo.DeviceSignature, deviceInfo);
            }
        }

        private UsbDeviceInfo getDeviceInfo(string friendlyName, string deviceId)
        {
            string shortName = GetPortNameFromInfoString(friendlyName);
            string vids = GetValueFromInfoString(deviceId, "VID_", true);
            string pids = GetValueFromInfoString(deviceId, "PID_", true);
            int vid = 0;
            int pid = 0;
            try
            {
                vid = Int32.Parse(vids, System.Globalization.NumberStyles.HexNumber);
                pid = Int32.Parse(pids, System.Globalization.NumberStyles.HexNumber);
            }
            catch (System.FormatException)
            {
                //Logger.loge(this, "Could not get USB device's VID/PID: " + friendlyName + " " + deviceId);
                return null;
            }
            return new UsbDeviceInfo(shortName, friendlyName, vid, pid);
        }

        public string GetScannerSerialPortName()
        {
            // VID and PID must be 
            List<UsbDeviceInfo> result = GetDeviceInfoById(SCANNER_VID, SCANNER_PID);
            return result.Count > 0 ? result.ElementAt(0).ShortName : null;
        }

        public UsbDeviceInfo GetDeviceInfoByPortName(string portName)
        {
            foreach (UsbDeviceInfo usbDeviceInfo in this.UsbDevicesCollection.Values)
            {
                if (usbDeviceInfo.ShortName == portName) return usbDeviceInfo;
            }
            return null;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="vid"></param>
        /// <param name="pid"></param>
        /// <returns></returns>
        public List<UsbDeviceInfo> GetDeviceInfoById(int vid, int pid)
        {
            List<UsbDeviceInfo> result = new List<UsbDeviceInfo>();
            foreach (UsbDeviceInfo usbDeviceInfo in this.UsbDevicesCollection.Values)
            {
                if (usbDeviceInfo.VID == vid && usbDeviceInfo.PID == pid) result.Add(usbDeviceInfo);
            }
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="shortName"></param>
        /// <returns></returns>
        public List<UsbDeviceInfo> GetDeviceInfoByShortName(string shortName)
        {
            List<UsbDeviceInfo> result = new List<UsbDeviceInfo>();
            foreach (UsbDeviceInfo usbDeviceInfo in this.UsbDevicesCollection.Values)
            {
                if (usbDeviceInfo.ShortName == shortName) result.Add(usbDeviceInfo);
            }
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        public void LogAllDevices()
        {
            foreach (UsbDeviceInfo usbDeviceInfo in this.UsbDevicesCollection.Values)
            {
                Console.WriteLine("usb device: " + usbDeviceInfo.ToString());
            }
        }

        #region WQL

        private void startWatchingUsb()
        {
            WqlEventQuery insertQuery = new WqlEventQuery("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'");
            usbInsertWatcher = new ManagementEventWatcher(insertQuery);
            usbInsertWatcher.EventArrived += new EventArrivedEventHandler(HandleDeviceArrived);
            usbInsertWatcher.Start();

            WqlEventQuery removeQuery = new WqlEventQuery("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity'");
            usbRemoveWatcher = new ManagementEventWatcher(removeQuery);
            usbRemoveWatcher.EventArrived += new EventArrivedEventHandler(HandleDeviceRemoved);
            usbRemoveWatcher.Start();
        }

        private void stopWatchingUsb()
        {
            usbInsertWatcher.Stop();
            usbRemoveWatcher.Stop();
        }

        private void HandleDeviceArrived(object sender, EventArrivedEventArgs e)
        {
            handleDeviceChanged(e, EventType.DEVICE_INSERTED);
        }

        private void HandleDeviceRemoved(object sender, EventArrivedEventArgs e)
        {
            handleDeviceChanged(e, EventType.DEVICE_REMOVED);
        }

        private void handleDeviceChanged(EventArrivedEventArgs e, EventType eventType)
        {
            ManagementBaseObject instance = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            if (new Guid((string)instance["ClassGuid"]) != GUID_DEVCLASS_COM_PORT)
            {
                return;
            }
            string infoString = "";
            foreach (var property in instance.Properties)
            {
                infoString += property.Name + " = " + property.Value + Environment.NewLine;
            }

            //Logger.logd(this, infoString);

            UsbDeviceInfo usbDeviceInfo = getDeviceInfo((string)instance.Properties[NAME].Value, (string)instance.Properties[DEVICE_ID].Value);
            if (this.UsbDevicesCollection.Contains(usbDeviceInfo.DeviceSignature))
            {
                this.UsbDevicesCollection.Remove(usbDeviceInfo.DeviceSignature);
            }
            else
            {
                this.UsbDevicesCollection.Add(usbDeviceInfo.DeviceSignature, usbDeviceInfo);
            }

            //Logger.logd(this, "Devices after: " + eventType);
            foreach (UsbDeviceInfo udi in this.UsbDevicesCollection.Values)
                //Logger.logd(this, udi.ToString() + Environment.NewLine);
            //Logger.logd(this, "End of devices list.");

            this.UsbDevicesChangedEvent(this, new UsbDevicesChangedEventArgs(usbDeviceInfo, eventType));
        }

        #endregion

        #region STATIC

        /// <summary>
        /// 
        /// </summary>
        /// <param name="infoString"></param>
        /// <returns></returns>
        private static string GetPortNameFromInfoString(string infoString)
        {
            String[] split = Regex.Split(infoString, @"(COM\d+)");
            if (split.Length > 1)
            {
                return split[1];
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="infoString"></param>
        /// <returns></returns>
        private static string GetValueFromInfoString(string infoString, string propertyName, bool removePropertyName)
        {
            String[] split = Regex.Split(infoString, "(" + propertyName + @".{4})");
            if (split.Length > 1)
            {
                return removePropertyName ? split[1].Replace(propertyName, "") : split[1];
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static ManagementObjectCollection GetUsbComPorts()
        {
            return GetUsbDevicesByVidPid(0, 0, true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vid"></param>
        /// <param name="pid"></param>
        /// <param name="onlyComPorts"></param>
        /// <returns></returns>
        public static ManagementObjectCollection GetUsbDevicesByVidPid(int vid, int pid, bool onlyComPorts)
        {
            pid = vid == 0 ? vid : pid;
            ManagementObjectCollection objectCollection;
            ManagementObjectSearcher objectSearcher;
            string query = @"SELECT * FROM Win32_PnPEntity";
            query += onlyComPorts || vid != 0 ? " WHERE" : "";
            query += vid != 0 ? " DeviceID like '%VID_" + vid + (pid != 0 ? "&PID_" + pid : "") + "%'" : "";
            query += onlyComPorts && vid != 0 ? " AND" : "";
            query += onlyComPorts ? " Caption like '%(COM%' " : "";
            objectSearcher = new ManagementObjectSearcher(query);
            objectCollection = objectSearcher.Get();
            return objectCollection;
        }

        #endregion

    }

    class UsbDevicesChangedEventArgs : EventArgs
    {

        public UsbDeviceInfo UsbDeviceInfo { private set; get; }
        public UsbDevices.EventType EventType { private set; get; }

        public UsbDevicesChangedEventArgs(UsbDeviceInfo usbDeviceInfo, UsbDevices.EventType eventType)
        {
            this.UsbDeviceInfo = usbDeviceInfo;
            this.EventType = eventType;
        }

    }

    class UsbDeviceInfo
    {

        public string ShortName { private set; get; }
        public string FriendlyName { private set; get; }
        public int VID { private set; get; }
        public int PID { private set; get; }
        public string DeviceSignature
        {
            get
            {
                return this.VID + "&" + this.PID;
            }
        }

        public static UsbDeviceInfo Empty = new UsbDeviceInfo("", "", 0, 0);

        public UsbDeviceInfo(string shortName, string friendlyName, int vid, int pid)
        {
            this.ShortName = shortName;
            this.FriendlyName = friendlyName;
            this.VID = vid;
            this.PID = pid;
        }


        public override string ToString()
        {
            return
                "FN: " + this.FriendlyName + "; " +
                "SN: " + this.ShortName + "; " +
                "ID: " + this.DeviceSignature + "; ";
        }

    }

}
