using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace YAWOL
{
    class NetworkScanner
    {
        [DllImport("iphlpapi.dll")]
        private static extern int SendARP(int DestIP, int SrcIP, [Out] byte[] pMacAddr, ref int PhyAddrLen);

        public static Host[] Scan(string localNetworkAddress, int[] excludedHosts, int low = 1, int high = 255)
        {
            high = high < 256 && high >= 0? high : 255;
            low = low < 256 && low >= 0 ? low : 0;
            localNetworkAddress = localNetworkAddress.Remove(localNetworkAddress.LastIndexOf('.') + 1);

            ConcurrentBag<PingReply> successfulPingReplies = new ConcurrentBag<PingReply>();
            Parallel.For(low, high, i =>
            {
                if (excludedHosts != null && !excludedHosts.Contains(i))
                {
                    Ping ping = new Ping();
                    PingReply pingReply = ping.Send(localNetworkAddress + i);
                    if (pingReply.Status == IPStatus.Success)
                    {
                        successfulPingReplies.Add(pingReply);
                    }
                }
            });

            ConcurrentBag<Host> scannedHosts = new ConcurrentBag<Host>();
            Parallel.ForEach(successfulPingReplies, reply =>
            {
                byte[] mac = new byte[6];
                int length = mac.Length;
                SendARP(BitConverter.ToInt32(reply.Address.GetAddressBytes(), 0), 0, mac, ref length);

                String hostname = null;
                try
                {
                    hostname = Dns.GetHostEntry(reply.Address).HostName;
                }
                catch (Exception)
                {
                    //don't care
                }

                scannedHosts.Add(new Host
                {
                    MacAddress = mac,
                    LastKnownIp = reply.Address.ToString(),
                    Name = hostname ?? "Unknown"
                });
            });

            return scannedHosts.ToArray();
        }

        public static void Wake(byte[] mac)
        {
            using (UdpClient client = new UdpClient())
            {
                client.Connect(IPAddress.Broadcast, 9);

                byte[] packet = new byte[17 * 6];

                for (int i = 0; i < 6; i++)
                {
                    packet[i] = 0xFF;
                }

                for (int i = 1; i <= 16; i++)
                {
                    for (int j = 0; j < 6; j++)
                    {
                        packet[i * 6 + j] = mac[j];
                    }
                }

                client.Send(packet, packet.Length);
            }
        }

        public static NIC[] GetNetworkInterfaces()
        {
            List<NIC> nics = new List<NIC>();
            foreach (string nic in NetworkInterface.GetAllNetworkInterfaces().Select(nic => nic.Name))
            {
                nics.Add(new NIC(nic, GetInterfaceIpAddress(nic)));
            }
            return nics.ToArray();
        }

        private static string GetInterfaceIpAddress(string interfacename)
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces().First(n => n.Name == interfacename);

            if (nic.OperationalStatus == OperationalStatus.Up)
            {
                UnicastIPAddressInformation uni =
                    nic.GetIPProperties()
                        .UnicastAddresses.FirstOrDefault(
                            ip =>
                                ip.Address.AddressFamily == AddressFamily.InterNetwork &&
                                !IPAddress.IsLoopback(ip.Address));

                if (uni != null)
                {
                    byte[] addressbytes = uni.Address.GetAddressBytes();
                    return string.Format("{0}.{1}.{2}.{3}", addressbytes[0], addressbytes[1], addressbytes[2],
                        addressbytes[3]);
                }
            }
            return null;
        }
    }
}
