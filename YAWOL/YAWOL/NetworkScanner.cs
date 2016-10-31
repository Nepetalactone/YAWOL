﻿using System;
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

        public static IEnumerable<Host> Scan(string localNetworkAddress, int[] excludedHosts, byte low = 1, byte high = 255)
        {
            low = low == 0 ? (byte)1 : low;
            localNetworkAddress = localNetworkAddress.Remove(localNetworkAddress.LastIndexOf('.') + 1);

            var successfulPingReplies = new ConcurrentBag<PingReply>();
            Parallel.For(low, high, i =>
            {
                if (excludedHosts != null && !excludedHosts.Contains(i))
                {
                    var ping = new Ping();
                    var pingReply = ping.Send(localNetworkAddress + i);
                    if (pingReply.Status == IPStatus.Success)
                    {
                        successfulPingReplies.Add(pingReply);
                    }
                }
            });

            var scannedHosts = new ConcurrentBag<Host>();
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

            return scannedHosts;
        }

        public static void Wake(byte[] mac)
        {
            using (var client = new UdpClient())
            {
                client.Connect(IPAddress.Broadcast, 9);

                var packet = new byte[17 * 6];

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

        public static IEnumerable<NIC> GetNetworkInterfaces()
        {
            return NetworkInterface.GetAllNetworkInterfaces().Select(nic => new NIC(nic.Name, GetInterfaceIpAddress(nic.Name)));
        }

        private static string GetInterfaceIpAddress(string interfacename)
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces().First(n => n.Name == interfacename);

            if (nic.OperationalStatus == OperationalStatus.Up)
            {
                var uni = nic.GetIPProperties().UnicastAddresses.FirstOrDefault(ip =>
                                ip.Address.AddressFamily == AddressFamily.InterNetwork 
                                && !IPAddress.IsLoopback(ip.Address));

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
