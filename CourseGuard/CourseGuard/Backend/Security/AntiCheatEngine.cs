using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CourseGuard.Backend.Security
{
    public static class AntiCheatEngine
    {
        private static readonly string[] BlacklistedProcesses = new[]
        {
            // Remote desktop / Remote control
            "teamviewer",
            "ultraviewer",
            "ultraviewer_desktop",
            "anydesk",
            "rustdesk",
            "logmein",
            "chrome remote desktop",
            
            // Video recording / Stream
            "obs",
            "obs64",
            "camtasia",
            "camtasiastudio",
            "sharex",
            "bandicam"
        };

        private static readonly string[] RemoteControlProcesses = new[]
        {
            "teamviewer",
            "ultraviewer",
            "ultraviewer_desktop",
            "anydesk",
            "rustdesk",
            "logmein"
        };

        public static bool HasMultipleScreens()
        {
            return Screen.AllScreens.Length > 1;
        }

        public static bool IsRunningInVirtualMachine()
        {
            try
            {
                // 1. Check BIOS/System information in Registry
                using (var biosKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS"))
                {
                    if (biosKey != null)
                    {
                        string vendor = (biosKey.GetValue("BIOSVendor")?.ToString() ?? "").ToLowerInvariant();
                        string manufacturer = (biosKey.GetValue("SystemManufacturer")?.ToString() ?? "").ToLowerInvariant();
                        string productName = (biosKey.GetValue("SystemProductName")?.ToString() ?? "").ToLowerInvariant();

                        string[] vmKeywords = { "vmware", "virtualbox", "vbox", "qemu", "virtual", "xen", "hyper-v", "microsoft corporation" };
                        foreach (var keyword in vmKeywords)
                        {
                            if (vendor.Contains(keyword) || manufacturer.Contains(keyword) || productName.Contains(keyword))
                            {
                                return true;
                            }
                        }
                    }
                }

                // 2. Check System Motherboard/BIOS identifier
                using (var sysKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System"))
                {
                    if (sysKey != null)
                    {
                        string identifier = (sysKey.GetValue("Identifier")?.ToString() ?? "").ToLowerInvariant();
                        string systemBiosVersion = (sysKey.GetValue("SystemBiosVersion")?.ToString() ?? "").ToLowerInvariant();
                        if (identifier.Contains("virtual") || systemBiosVersion.Contains("vbox") || systemBiosVersion.Contains("vmware") || systemBiosVersion.Contains("virtual"))
                        {
                            return true;
                        }
                    }
                }

                // 3. Check services list (VirtualBox, VMware, Hyper-V, QEMU services registry check)
                string[] vmServices = new[]
                {
                    @"SYSTEM\CurrentControlSet\Services\VBoxGuest",
                    @"SYSTEM\CurrentControlSet\Services\VMTools",
                    @"SYSTEM\CurrentControlSet\Services\vmbus",
                    @"SYSTEM\CurrentControlSet\Services\qemu-ga"
                };

                foreach (var svcPath in vmServices)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(svcPath))
                    {
                        if (key != null)
                        {
                            return true;
                        }
                    }
                }
            }
            catch
            {
                // Fail-safe: if permission error or key not found, do not block legitimate users
            }
            return false;
        }

        public static List<string> GetRunningBlacklistedApps(out bool hasRemoteControl)
        {
            hasRemoteControl = false;
            var detected = new List<string>();
            try
            {
                var processes = Process.GetProcesses();
                foreach (var proc in processes)
                {
                    try
                    {
                        string procName = proc.ProcessName.ToLowerInvariant();
                        foreach (var blacklisted in BlacklistedProcesses)
                        {
                            if (procName.Contains(blacklisted))
                            {
                                if (!detected.Contains(proc.ProcessName))
                                {
                                    detected.Add(proc.ProcessName);
                                }

                                foreach (var rc in RemoteControlProcesses)
                                {
                                    if (procName.Contains(rc))
                                    {
                                        hasRemoteControl = true;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Some system processes might throw Access Denied
                    }
                }
            }
            catch
            {
                // Access/general issues
            }
            return detected;
        }

        public static void ClearClipboard()
        {
            try
            {
                Clipboard.Clear();
            }
            catch
            {
                // STA thread issues if called incorrectly
            }
        }
    }
}
