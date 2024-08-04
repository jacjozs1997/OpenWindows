using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace OpenWindows
{
    internal class Program
    {
        static readonly Config m_config = InitConfig();

        static readonly string m_disablePrivacyRegistry = @"SOFTWARE\Policies\Microsoft\Windows\OOBE";

        static readonly string m_disablePrivacyRegistryName = "DisablePrivacyExperience";

        static readonly SelectQuery m_userSelectQuery = new SelectQuery("Win32_UserAccount", "LocalAccount = True", new string[] { "Name", "Disabled" });

        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        static void Main(string[] args)
        {
            if (Environment.UserName.ToLower() == "defaultuser0")
            {
                while (CheckInternet())
                {
                    Console.WriteLine("Your pc connected to internet.");
                    Console.WriteLine("Please disconnect from the internet.");
                    Console.WriteLine("Please press enter if the pc is disconnected from the internet...");
                    Console.ReadLine();
                }
                Console.WriteLine($"Open windows system");
                bool loop = true;
                do
                {
                    using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(m_userSelectQuery))
                    {
                        DeleteHpUser(searcher.Get());

                        Console.WriteLine($"Open microsoft account window");
                        Process.Start("ms-cxh://SETADDNEWUSER");//Open login dialog
                    Wait:
                        Console.WriteLine($"Wait for microsoft account window...");

                        Thread.Sleep(1000);

                        IntPtr WindowToFind = FindWindow(m_config.AppClassName, null);

                        IntPtr ForegroundWindow = GetForegroundWindow();

                        if (WindowToFind != IntPtr.Zero)
                        {
                            if (WindowToFind != ForegroundWindow && !SetForegroundWindow(WindowToFind))
                            {
                                goto Wait;
                            }
                            if (WindowToFind == ForegroundWindow)
                            {
                                Thread.Sleep(500);
                                SendKeys.SendWait(m_config.UserName + "{enter}");
                                Thread.Sleep(500);
                            }
                        }
                        else
                        {
                            goto Wait;
                        }

                        loop = DeleteOtherUser(searcher.Get());
                    }
                } while (loop);

                DisablePrivacyRegistry();

                if (m_config.AutoRestart)
                {
                    Thread.Sleep(1000);
                }
                else
                {
                    Console.WriteLine("Press a enter to restart the pc.");
                    Console.ReadLine();
                }

                DeleteDefulteUser0();

                Console.WriteLine("Restarting...");
                Process.Start("shutdown.exe", "/r /t 0");//Restart
                //Console.ReadLine();
                return;
            }
        }
        /// <summary>
        /// Initial config
        /// </summary>
        /// <returns></returns>
        static Config InitConfig()
        {
            Config config = new Config();
            try
            {
                config = File.ReadAllText("./config.json").FromJson<Config>();
            }
            catch (Exception)
            {

            }
            return config;
        }
        static bool CheckInternet()
        {
            try
            {
                return (new Ping().Send("google.com", 900, new byte[32], new PingOptions()).Status == IPStatus.Success);
            }
            catch (Exception)
            {
                return false;
            }
        }
        /// <summary>
        /// Delete old hp user
        /// </summary>
        /// <param name="envVars"></param>
        static void DeleteHpUser(ManagementObjectCollection envVars)
        {
            Console.WriteLine("Old hp user searching...");
            foreach (ManagementObject envVar in envVars)
            {
                if (envVar["Name"].ToString() == m_config.UserName)
                {
                    Console.WriteLine("Old hp user deleting...");
                    Process.Start("net", $"user {envVar["Name"]} /DELETE").WaitForExit();
                }
            }
            if (Directory.Exists($"C:\\Users\\{m_config.UserName}"))
            {
                Process.Start("cmd", $"/c rmdir \"C:\\Users\\{m_config.UserName}\" /s /q").WaitForExit();
            }
        }
        /// <summary>
        /// Deletes all active users who are not needed
        /// </summary>
        /// <param name="envVars"></param>
        /// <returns></returns>
        static bool DeleteOtherUser(ManagementObjectCollection envVars)
        {
            bool result = true;
            foreach (ManagementObject envVar in envVars)
            {
                if (envVar["Name"].ToString() == m_config.UserName)
                {
                    Console.WriteLine($"Break user: {envVar["Name"]}");
                    result = false;
                }
                else if (!(bool)envVar["Disabled"] && envVar["Name"].ToString() != "defaultuser0")
                {
                    Console.WriteLine($"Delete user: {envVar["Name"]}");
                    Process.Start("net", $"user {envVar["Name"]} /DELETE").WaitForExit();
                }
            }
            return result;
        }
        static void DeleteDefulteUser0()
        {
            Console.WriteLine("Add startup delete defaultuser0...");

            using (var file = new StreamWriter("C:\\defaultuser0.cmd"))
            {
                file.WriteLine($"rmdir /s /q C:\\Users\\defaultuser0");
                file.WriteLine($"net user defaultuser0 /delete");
                file.WriteLine($"start /b \"\" cmd /c del \" % ~f0\"&exit /b");
            }

            Registry.SetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\RunOnce", "RunScript", "C:\\defaultuser0.cmd", RegistryValueKind.String);
        }
        /// <summary>
        /// Set "Don't launch privacy settings experience on user logon" registry
        /// https://admx.help/HKLM/Software/Policies/Microsoft/Windows/OOBE
        /// </summary>
        static void DisablePrivacyRegistry()
        {
            Console.WriteLine("Disable Privacy Experience");
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(m_disablePrivacyRegistry, true))
            {
                RegistryKey registry = key;
                if (registry == null)
                    registry = Registry.LocalMachine.CreateSubKey(m_disablePrivacyRegistry);

                if (registry.GetValue(m_disablePrivacyRegistryName) == null)
                    registry.SetValue(m_disablePrivacyRegistryName, 1);
                else
                    Console.Error.WriteLine("Failed disable privacy experience");

                registry.Dispose();
            }
        }
    }
}
