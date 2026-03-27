// CrossbowNic.cs  —  CROSSBOW shared NIC detection helper
//
// Detects the correct local IP for socket binding based on the 192.168.1.x
// subnet range policy:
//   Internal (.1–.99)   — A2 engineering GUI, all five controllers
//   External (.200–.254) — A3 THEIA HMI, MCC and BDC only
//
// Used by all controller classes (MCC, BDC, TMC, FMC) to avoid hardcoding
// a specific IP and to handle dual-NIC machines correctly regardless of
// which physical adapter holds which IP.
//
// ICD reference: IPGD-0006 ARCHITECTURE.md Section 2 — IP Range Policy

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace CROSSBOW
{
    public static class CrossbowNic
    {
        /// <summary>
        /// Returns the first 192.168.1.x address with last octet in 1–99.
        /// Used by all eng GUI controller classes to bind A2 to the internal NIC.
        /// Returns "0.0.0.0" as safe fallback if none found (Windows picks adapter).
        /// </summary>
        public static string GetInternalIP()
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var parts = addr.Address.ToString().Split('.');
                    if (parts.Length == 4 &&
                        parts[0] == "192" && parts[1] == "168" && parts[2] == "1" &&
                        int.TryParse(parts[3], out int octet) &&
                        octet >= 1 && octet <= 99)
                        return addr.Address.ToString();
                }
            }
            return "0.0.0.0";   // fallback — unbound, Windows picks adapter
        }

        /// <summary>
        /// Returns the first 192.168.1.x address with last octet in 200–254.
        /// Used by THEIA HMI for A3 External bind (MCC and BDC only).
        /// Returns "0.0.0.0" as safe fallback if none found.
        /// </summary>
        public static string GetExternalIP()
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var parts = addr.Address.ToString().Split('.');
                    if (parts.Length == 4 &&
                        parts[0] == "192" && parts[1] == "168" && parts[2] == "1" &&
                        int.TryParse(parts[3], out int octet) &&
                        octet >= 200 && octet <= 254)
                        return addr.Address.ToString();
                }
            }
            return "0.0.0.0";   // fallback
        }
    }
}
