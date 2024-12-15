using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Shapes;

namespace MSFS_AutoFPS
{
    public class MemoryManager
    {
        private ServiceModel Model;

        private long addrTLOD;
        private long addrOLOD;
        private long addrTLOD_VR;
        private long addrOLOD_VR;
        private long addrCloudQ;
        private long addrCloudQ_VR;
        private long addrVrMode;
        private long addrFgMode;
        private long addrDynSet;
        private long addrDynSetVR;
        private bool allowMemoryWrites = false;
        private bool isDX12 = false;
        private long moduleBase;

        public MemoryManager(ServiceModel model)
        {
            try
            {
                this.Model = model;

                if (Model.isMSFS2024)
                {
                    MemoryInterface.Attach("FlightSimulator2024");
                    moduleBase = MemoryInterface.GetModuleAddress("FlightSimulator2024.exe");
                }
                else
                {
                    MemoryInterface.Attach("FlightSimulator");
                    moduleBase = MemoryInterface.GetModuleAddress(Model.SimModule);
                }

                GetActiveDXVersion();
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", $"Trying offsetModuleBase: 0x{model.OffsetModuleBase.ToString("X8")}");
                // Set initial base memory address to the one contained in the config file
                if (Model.isMSFS2024) GetMSFSMemoryAddresses2024(Model.OffsetModuleBase);
                else GetMSFSMemoryAddresses();
                // Run the memory boundary test to confirm that the offset is still valid
                MemoryBoundaryTest();
                // If the memory boundary test fails, try searching for a new valid offset
                if (!allowMemoryWrites)
                {
                    Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", $"Boundary tests failed - possible MSFS memory map change");
                    if (model.isMSFS2024) ModuleOffsetSearch2024();
                    else ModuleOffsetSearch();
                }
                else Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", $"Boundary tests passed - memory writes enabled");
                // If a valid offset is still not found, leave the app disabled, notify the user and log detailed initial offset memory boundary test data
                if (!allowMemoryWrites)
                {
                    Logger.Log(LogLevel.Debug, "MemoryManager:MemoryManager", $"Module offset search failed to find a valid offset - memory writes disabled");
                    if (Model.isMSFS2024) GetMSFSMemoryAddresses2024(Model.OffsetModuleBase);
                    else GetMSFSMemoryAddresses();
                    MemoryBoundaryTest(true);
                }
                // Otherwise reload the valid memory addresses and log the settings' initial values
                else
                {
                    if (model.isMSFS2024) GetMSFSMemoryAddresses2024(Model.OffsetModuleBase);
                    else GetMSFSMemoryAddresses();
                    VerifyAddresses();
                } 
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MemoryManager:MemoryManager", $"Exception {ex}: {ex.Message}");
            }
        }

        private void ModuleOffsetSearch()
        {
            long offsetBase = 0x00400000;
            bool offsetFound = false;
            long offset = 0;

            long moduleBase = MemoryInterface.GetModuleAddress(Model.SimModule);

            // 0x004AF3C8 was muumimorko version offsetBase
            // 0x004B2368 was Fragtality version offsetBase
            Logger.Log(LogLevel.Debug, "MemoryManager:ModuleOffsetSearch", $"OffsetModuleBase search started");
            
            while (offset < 0x100000 && !offsetFound)
            {
                addrTLOD = MemoryInterface.ReadMemory<long>(moduleBase + offsetBase + offset) + Model.OffsetPointerMain;
                if (addrTLOD > 0)
                {
                    addrTLOD_VR = MemoryInterface.ReadMemory<long>(addrTLOD) + Model.OffsetPointerTlodVr;
                    addrTLOD = MemoryInterface.ReadMemory<long>(addrTLOD) + Model.OffsetPointerTlod;
                    addrOLOD_VR = addrTLOD_VR + Model.OffsetPointerOlod;
                    addrOLOD = addrTLOD + Model.OffsetPointerOlod;
                    addrCloudQ = addrTLOD + Model.OffsetPointerCloudQ;
                    addrCloudQ_VR = addrCloudQ + Model.OffsetPointerCloudQVr;
                    addrVrMode = addrTLOD - Model.OffsetPointerVrMode;
                    addrFgMode = addrTLOD - Model.OffsetPointerFgMode;
                    MemoryBoundaryTest();
                }
                if (allowMemoryWrites) offsetFound = true;
                else offset++;
            }
            if (offsetFound)
            {
                Model.SetSetting("offsetModuleBase", "0x" + (offsetBase + offset).ToString("X8"));
                Logger.Log(LogLevel.Debug, "MemoryManager:ModuleOffsetSearch", $"New offsetModuleBase found and saved: 0x{(offsetBase + offset).ToString("X8")}");
            }
            else Logger.Log(LogLevel.Debug, "MemoryManager:ModuleOffsetSearch", $"OffsetModuleBase not found after {offset} iterations");

        }

        // From MSFS2024_AutoFPS_kayJay1c6 code 
        private void ModuleOffsetSearch2024()
        {
            Logger.Log(LogLevel.Debug, "MemoryManager:ModuleOffsetSearch", "Starting module offset search");

            if (moduleBase == 0)
            {
                Logger.Log(LogLevel.Error, "MemoryManager:ModuleOffsetSearch", "Failed to get module base address");
                return;
            }

            // 1.1.10.0 offsets: Steam 0x0A14F010, MS Store 0x09E18000
            // 1.2.7.0 offsets: Steam 0x0A241C60, MS Store 0x09F0DC40
            
            // Define search range (adjust as needed)
            long startOffset = Model.isMSFS2024 && File.Exists(App.msConfigSteam2024) ? 0x0A000000 : 0x09B00000; // Start 4 MB before the expected offset for Steam and MS Store version respectively
            long endOffset = startOffset + 0x01000000;   // End 12 MB after the expected offset
            long step = 0x10;              // Keep small step size for precision

            int attempts = 0;
            Model.OffsetSearchingActive = true;
            for (long testOffset = startOffset; testOffset < endOffset; testOffset += step)
            {
                attempts++;

                try
                {
                    GetMSFSMemoryAddresses2024(testOffset);
                    MemoryBoundaryTest();

                    if (allowMemoryWrites)
                    {
                        Logger.Log(LogLevel.Debug, "MemoryManager:ModuleOffsetSearch",
                            $"Found valid module offset: 0x{testOffset:X8} after {attempts} attempts");
                        Model.SetSetting("offsetModuleBase", "0x" + (testOffset).ToString("X8"));
                        Logger.Log(LogLevel.Debug, "MemoryManager:ModuleOffsetSearch", $"New offsetModuleBase found and saved: 0x{(testOffset).ToString("X8")}");

                        Model.OffsetSearchingActive = false;
                        return; // Exit the method if a valid offset is found
                    }
                }
                catch
                {
                    // Ignore exceptions and continue searching
                }

                // Log progress every 1 MB
                if (attempts % 262144 == 0)
                {
                    Logger.Log(LogLevel.Debug, "MemoryManager:ModuleOffsetSearch",
                        $"Searched {(testOffset - startOffset) / 1024 / 1024} MB... Current offset: 0x{testOffset:X8}");
                }
            }

            Logger.Log(LogLevel.Error, "MemoryManager:ModuleOffsetSearch",
                $"Failed to find a valid module offset after {attempts} attempts");
            Model.OffsetSearchingActive = false;
        }

        private void MemoryBoundaryTest(bool logResult = false)
        {
            // Boundary check a few known setting memory addresses to see if any fail which likely indicates MSFS memory map has changed
            if (addrTLOD < 0 || GetTLOD_PC() < 10 || GetTLOD_PC() > 1000 || GetTLOD_VR() < 10 || GetTLOD_VR() > 1000
                || GetOLOD_PC() < 10 || GetOLOD_PC() > 1000 || GetOLOD_VR() < 10 || GetOLOD_VR() > 1000
                || GetCloudQ_PC() < 0 || GetCloudQ_PC() > 3 || GetCloudQ_VR() < 0 || GetCloudQ_VR() > 3
                || MemoryInterface.ReadMemory<int>(addrVrMode) < 0 || MemoryInterface.ReadMemory<int>(addrVrMode) > 1
                || MemoryInterface.ReadMemory<int>(addrTLOD + Model.OffsetPointerAnsio) < 0 || MemoryInterface.ReadMemory<int>(addrTLOD + Model.OffsetPointerAnsio) > 16
                || (!Model.isMSFS2024 && !(MemoryInterface.ReadMemory<int>(addrTLOD + Model.OffsetPointerWaterWaves) == 128 || MemoryInterface.ReadMemory<int>(addrTLOD + Model.OffsetPointerWaterWaves) == 256 || MemoryInterface.ReadMemory<int>(addrTLOD + Model.OffsetPointerWaterWaves) == 512))
                || (Model.isMSFS2024 && !(MemoryInterface.ReadMemory<int>(addrTLOD + Model.OffsetPointerCubeMap) >= 64 || MemoryInterface.ReadMemory<int>(addrTLOD + Model.OffsetPointerCubeMap) % 32 == 0 || MemoryInterface.ReadMemory<int>(addrTLOD + Model.OffsetPointerCubeMap) <= 384)))
                // Even though valid cube map values are 128, 196, 256 and 384 in MSFS 2024, one user encountered a value of 64 so the cube map test has been expanded to cover passing with this value, even if not technically a valid setting
            {
                allowMemoryWrites = false;
            }
            else allowMemoryWrites = true;
            if (logResult && (ServiceModel.TestVersion || !allowMemoryWrites))
            {
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"Address TLOD: 0x{addrTLOD:X}");
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"Address OLOD: 0x{addrOLOD:X}");
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"Address CloudQ: 0x{addrCloudQ:X}");
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"Address TLOD VR: 0x{addrTLOD_VR:X}");
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"Address OLOD VR: 0x{addrOLOD_VR:X}");
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"Address CloudQ VR: 0x{addrCloudQ_VR:X}");
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"Address VrMode: 0x{addrVrMode:X}");
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"Address FgMode: 0x{addrFgMode:X}");
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"Address Ansio Filter: 0x{(addrTLOD + Model.OffsetPointerAnsio):X}");
                if (Model.isMSFS2024) Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"Address Cubemap Reflections: 0x{(addrTLOD + Model.OffsetPointerCubeMap):X}");
                else Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"Address Water Waves: 0x{(addrTLOD + Model.OffsetPointerWaterWaves):X}");
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"TLOD PC: {GetTLOD_PC()}");
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"TLOD VR: {GetTLOD_VR()}");
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"OLOD PC: {GetOLOD_PC()}");
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"OLOD VR: {GetOLOD_VR()}");
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"Cloud Quality PC: {ServiceModel.CloudQualityText(GetCloudQ_PC())}");
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"Cloud Quality VR: {ServiceModel.CloudQualityText(GetCloudQ_VR())}");
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"VR Mode: {MemoryInterface.ReadMemory<int>(addrVrMode)}");
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"FG Mode: {MemoryInterface.ReadMemory<int>(addrFgMode)}");
                Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"Ansio Filter: {MemoryInterface.ReadMemory<int>(addrTLOD + Model.OffsetPointerAnsio)}X");
                if (Model.isMSFS2024) Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"Cubemap Reflections: {MemoryInterface.ReadMemory<int>(addrTLOD + Model.OffsetPointerCubeMap)}");
                else Logger.Log(LogLevel.Debug, "MemoryManager:MemoryBoundaryTest", $"Water Waves: {MemoryInterface.ReadMemory<int>(addrTLOD + Model.OffsetPointerWaterWaves)}");
            }
        }
        private void GetMSFSMemoryAddresses()
        {
            addrTLOD = MemoryInterface.ReadMemory<long>(moduleBase + Model.OffsetModuleBase) + Model.OffsetPointerMain;
            if (addrTLOD > 0)
            {
                addrTLOD_VR = MemoryInterface.ReadMemory<long>(addrTLOD) + Model.OffsetPointerTlodVr;
                addrTLOD = MemoryInterface.ReadMemory<long>(addrTLOD) + Model.OffsetPointerTlod;
                addrOLOD_VR = addrTLOD_VR + Model.OffsetPointerOlod;
                addrOLOD = addrTLOD + Model.OffsetPointerOlod;
                addrCloudQ = addrTLOD + Model.OffsetPointerCloudQ;
                addrCloudQ_VR = addrCloudQ + Model.OffsetPointerCloudQVr;
                addrVrMode = addrTLOD - Model.OffsetPointerVrMode;
                addrFgMode = addrTLOD - Model.OffsetPointerFgMode;
                if (allowMemoryWrites)
                {
                    Logger.Log(LogLevel.Debug, "MemoryManager:GetMSFSMemoryAddresses", $"Address TLOD: 0x{addrTLOD:X} / {addrTLOD}");
                    Logger.Log(LogLevel.Debug, "MemoryManager:GetMSFSMemoryAddresses", $"Address OLOD: 0x{addrOLOD:X} / {addrOLOD}");
                    Logger.Log(LogLevel.Debug, "MemoryManager:GetMSFSMemoryAddresses", $"Address CloudQ: 0x{addrCloudQ:X} / {addrCloudQ}");
                    Logger.Log(LogLevel.Debug, "MemoryManager:GetMSFSMemoryAddresses", $"Address TLOD VR: 0x{addrTLOD_VR:X} / {addrTLOD_VR}");
                    Logger.Log(LogLevel.Debug, "MemoryManager:GetMSFSMemoryAddresses", $"Address OLOD VR: 0x{addrOLOD_VR:X} / {addrOLOD_VR}");
                    Logger.Log(LogLevel.Debug, "MemoryManager:GetMSFSMemoryAddresses", $"Address CloudQ VR: 0x{addrCloudQ_VR:X} / {addrCloudQ_VR}");
                    Logger.Log(LogLevel.Debug, "MemoryManager:GetMSFSMemoryAddresses", $"Address VrMode: 0x{addrVrMode:X} / {addrVrMode}");
                    Logger.Log(LogLevel.Debug, "MemoryManager:GetMSFSMemoryAddresses", $"Address FgMode: 0x{addrFgMode:X} / {addrFgMode}");
                }
            }
        }

        private bool GetMSFSMemoryAddresses2024(long BaseOffset)
        {
            try
            {
                long pointerBase = moduleBase + BaseOffset;
                long pointer = pointerBase;

                // Calculate addresses
                addrTLOD = CalculateAddress(pointer, Model.OffsetPointerTlod, "TLOD");
                addrOLOD = CalculateAddress(pointer, Model.OffsetPointerOlod, "OLOD");
                addrTLOD_VR = CalculateAddress(pointer, Model.OffsetPointerTlodVr, "TLOD VR");
                addrOLOD_VR = CalculateAddress(pointer, Model.OffsetPointerOlodVr, "OLOD VR"); 

                // Calculate other addresses
                addrVrMode = CalculateAddress(pointer, Model.OffsetPointerVrMode, "VR Mode");
                addrCloudQ = CalculateAddress(pointer, Model.OffsetPointerCloudQ, "CloudQ");
                addrCloudQ_VR = CalculateAddress(pointer, Model.OffsetPointerCloudQVr, "CloudQ VR");
                addrFgMode = CalculateAddress(pointer, Model.OffsetPointerFgMode, "FG Mode");
                addrDynSet = CalculateAddress(pointer, Model.OffsetPointerDynSet, "Dynamic Setting");
                addrDynSetVR = CalculateAddress(pointer, Model.OffsetPointerDynSetVr, "Dynamic Setting VR");

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "GetMSFSMemoryAddresses", $"Error: {ex.Message}");
                return false;
            }
        }

        private long CalculateAddress(long baseAddress, long offset, string propertyName)
        {
            long address = baseAddress + offset;
            //if (allowMemoryWrites) Logger.Log(LogLevel.Debug, "Address Calculation", $"{propertyName} Address: 0x{address:X}");
            return address;
        }

        private void VerifyAddresses()
        {
            VerifyAddress(addrTLOD, "TLOD", "float");
            VerifyAddress(addrOLOD, "OLOD", "float");
            VerifyAddress(addrTLOD_VR, "TLOD VR", "float");
            VerifyAddress(addrOLOD_VR, "OLOD VR", "float");
            Logger.Log(LogLevel.Debug, "Address Verification", $"CloudQ Value: {ServiceModel.CloudQualityText(GetCloudQ_PC())}");
            Logger.Log(LogLevel.Debug, "Address Verification", $"CloudQ VR Value: {ServiceModel.CloudQualityText(GetCloudQ_VR())}");
            VerifyAddress(addrVrMode, "VR Mode", "bool");
            VerifyAddress(addrFgMode, "FG Mode", "bool");
            if (Model.isMSFS2024)
            {
                VerifyAddress(addrDynSet, "Dynamic Setting", "bool");
                VerifyAddress(addrDynSetVR, "Dynamic Setting VR", "bool");
            }
        }

        private void VerifyAddress(long address, string propertyName, string dataType)
        {
            try
            {
                if (dataType == "float")
                {
                    float value = (float)Math.Round(MemoryInterface.ReadMemory<float>(address) * 100.0f);
                    Logger.Log(LogLevel.Debug, "Address Verification", $"{propertyName} Value: {value}");
                }
                else if (dataType == "int")
                {
                    int value = MemoryInterface.ReadMemory<int>(address);
                    Logger.Log(LogLevel.Debug, "Address Verification", $"{propertyName} Value: {value}");
                }
                else if (dataType == "bool")
                {
                    bool value = MemoryInterface.ReadMemory<byte>(address) == 1;
                    Logger.Log(LogLevel.Debug, "Address Verification", $"{propertyName} Value: {value}");
                }
                else Logger.Log(LogLevel.Debug, "Address Verification", $"{propertyName} Unknown data type");

                }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Address Verification", $"Failed to read {propertyName}: {ex.Message}");
            }
        }

        private void GetActiveDXVersion()
        {
            string filecontents;
            if (Model.isMSFS2024)
            {
                isDX12 = true;
                Logger.Log(LogLevel.Debug, "MemoryManager:GetActiveDXVersion", (File.Exists(App.msConfigSteam2024) ? "Steam" : "MS Store") + $" MSFS2024 detected - DX12");
            }
            else if (File.Exists(App.msConfigSteam))
            {
                StreamReader sr = new StreamReader(App.msConfigSteam);
                filecontents = sr.ReadToEnd();
                if (filecontents.Contains("PreferD3D12 1")) isDX12 = true;
                sr.Close();
                Logger.Log(LogLevel.Debug, "MemoryManager:GetActiveDXVersion", $"Steam MSF2020 version detected - " + (isDX12 ? "DX12" : "DX11"));
            }
            else
            {
                if (File.Exists(App.msConfigStore))
                {
                    StreamReader sr = new StreamReader(App.msConfigStore);
                    filecontents = sr.ReadToEnd();
                    if (filecontents.Contains("PreferD3D12 1")) isDX12 = true;
                    sr.Close();
                    Logger.Log(LogLevel.Debug, "MemoryManager:GetActiveDXVersion", $"MS Store MSFS2020 version detected - " + (isDX12 ? "DX12" : "DX11"));
                }
            }

        }

        public bool MemoryWritesAllowed()
        {
            return allowMemoryWrites;
        }
        public bool IsVrModeActive()
        {
            try
            {
                return MemoryInterface.ReadMemory<int>(addrVrMode) == 1; 
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MemoryManager:IsVrModeActive", $"Exception {ex}: {ex.Message}");
            }

            return false;
        }
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        public bool IsActiveWindowMSFS()
        {
            const int nChars = 256;
            string activeWindowTitle;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                activeWindowTitle = Buff.ToString();
                if (activeWindowTitle.Length > 26 && activeWindowTitle.Substring(0, 26) == "Microsoft Flight Simulator")
                    return true;
            }
            return false;
        }
 
        public bool IsDX12()
        {
            return isDX12;
        }
        public bool IsFgModeEnabled()
        {
            try
            {
                if (isDX12 && !Model.MemoryAccess.IsVrModeActive()) 
                    return MemoryInterface.ReadMemory<byte>(addrFgMode) == 1;
                else return false;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MemoryManager:IsFgModeEnabled", $"Exception {ex}: {ex.Message}");
            }

            return false;
        }

        public float GetTLOD_PC()
        {
            try
            {
                return (float)Math.Round(MemoryInterface.ReadMemory<float>(addrTLOD) * 100.0f);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MemoryManager:GetTLOD", $"Exception {ex}: {ex.Message}");
            }

            return 0.0f;
        }

        public float GetTLOD_VR()
        {
            try
            {
                return (float)Math.Round(MemoryInterface.ReadMemory<float>(addrTLOD_VR) * 100.0f);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MemoryManager:GetTLOD_VR", $"Exception {ex}: {ex.Message}");
            }

            return 0.0f;
        }

        public float GetOLOD_PC()
        {
            try
            {
                return (float)Math.Round(MemoryInterface.ReadMemory<float>(addrOLOD) * 100.0f);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MemoryManager:GetOLOD", $"Exception {ex}: {ex.Message}");
            }

            return 0.0f;
        }

         public float GetOLOD_VR()
        {
            try
            {
                return (float)Math.Round(MemoryInterface.ReadMemory<float>(addrOLOD_VR) * 100.0f);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MemoryManager:GetOLOD_VR", $"Exception {ex}: {ex.Message}");
            }

            return 0.0f;
        }

        public int GetCloudQ_PC()
        {
            try
            {
                return MemoryInterface.ReadMemory<int>(addrCloudQ);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MemoryManager:GetCloudQ", $"Exception {ex}: {ex.Message}");
            }

            return -1;
        }
        public int GetCloudQ_VR()
        {
            try
            {
                return MemoryInterface.ReadMemory<int>(addrCloudQ_VR);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MemoryManager:GetCloudQ VR", $"Exception {ex}: {ex.Message}");
            }

            return -1;
        }
        public bool GetDynSet()
        {
            try
            {
                return MemoryInterface.ReadMemory<byte>(addrDynSet) == 1;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MemoryManager:GetDynSet", $"Exception {ex}: {ex.Message}");
            }

            return false;
        }
        public bool GetDynSetVR()
        {
            try
            {
                return MemoryInterface.ReadMemory<byte>(addrDynSetVR) == 1;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MemoryManager:GetDynSetVR", $"Exception {ex}: {ex.Message}");
            }

            return false;
        }

        public void SetDynSet(bool value)
        {
            if (allowMemoryWrites)
            {
                try
                {
                    MemoryInterface.WriteMemory<byte>(addrDynSet, value);
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, "MemoryManager:SetDynSet", $"Exception {ex}: {ex.Message}");
                }
            }
        }

        public void SetDynSetVR(bool value)
        {
            if (allowMemoryWrites)
            {
                try
                {
                    MemoryInterface.WriteMemory<byte>(addrDynSetVR, value);
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, "MemoryManager:SetDynSetVR", $"Exception {ex}: {ex.Message}");
                }
            }
        }
        public void SetTLOD(float value)
        {
            if (allowMemoryWrites)
            {   
                SetTLOD_PC(value);
                SetTLOD_VR(value);
            }
        }
        public void SetTLOD_PC(float value)
        {
            try
            {
                MemoryInterface.WriteMemory<float>(addrTLOD, value / 100.0f);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MemoryManager:SetTLOD", $"Exception {ex}: {ex.Message}");
            }
        }
        public void SetTLOD_VR(float value)
        {
            try
            {
                MemoryInterface.WriteMemory<float>(addrTLOD_VR, value / 100.0f);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MemoryManager:SetTLOD VR", $"Exception {ex}: {ex.Message}");
            }
        }
        public void SetOLOD(float value)
        {
            if (allowMemoryWrites)
            {
                SetOLOD_PC(value);
                SetOLOD_VR(value);
            }
        }
        public void SetOLOD_PC(float value)
        {
            try
            {
                MemoryInterface.WriteMemory<float>(addrOLOD, value / 100.0f);
                
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MemoryManager:SetOLOD", $"Exception {ex}: {ex.Message}");
            }
        }
        public void SetOLOD_VR(float value)
        {
            try
            {
                MemoryInterface.WriteMemory<float>(addrOLOD_VR, value / 100.0f);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "MemoryManager:SetOLOD VR", $"Exception {ex}: {ex.Message}");
            }
        }
        public void SetCloudQ(int value)
        {
            if (allowMemoryWrites)
            {
                try
                {
                    MemoryInterface.WriteMemory<int>(addrCloudQ, value);
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, "MemoryManager:SetCloudQ", $"Exception {ex}: {ex.Message}");
                }
            }
        }
        public void SetCloudQ_VR(int value)
        {
            if (allowMemoryWrites)
            {
                try
                {
                    MemoryInterface.WriteMemory<int>(addrCloudQ_VR, value);
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, "MemoryManager:SetCloudQ VR", $"Exception {ex}: {ex.Message}");
                }
            }
        }
    }
}
