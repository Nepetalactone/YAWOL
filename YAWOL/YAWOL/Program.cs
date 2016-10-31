using System;
using System.Linq;
using System.Collections.Generic;

namespace YAWOL
{
    class Program
    {
        public static NIC Nic { get; private set; }
        private static readonly Database Db = new Database();
        static void Main(string[] args)
        {
            if (String.IsNullOrEmpty(Properties.Settings.Default.NICIP) || String.IsNullOrEmpty(Properties.Settings.Default.NICName))
            {
                SetInterface();
            }
            else
            {
                Nic = new NIC(Properties.Settings.Default.NICName, Properties.Settings.Default.NICIP);
            }

            if (args != null && args.Length > 0)
            {
                switch (args[0])
                {
                    case "set-nic":
                        SetInterface();
                        break;
                    case "show-current-nic":
                        ShowCurrentInterface();
                        break;
                    case "wake":
                        string[] hostsToWake;
                        if (args.Length > 1)
                        {
                            hostsToWake = new string[args.Length - 1];
                            Array.Copy(args, 1, hostsToWake, 0, args.Length - 1);
                        }
                        else
                        {
                            ShowKnownHosts();
                            Console.WriteLine("Enter hosts to wake");
                            hostsToWake = Console.ReadLine().Split(' ');
                        }
                        for (int i = 0; i < hostsToWake.Length; i++)
                        {
                            Wake(hostsToWake[i]);
                        }
                        break;
                    case "scan-network":
                        Scan();
                        break;
                    case "known-hosts":
                        ShowKnownHosts();
                        break;
                    case "assign-alias":
                        AssignAlias();
                        break;
                    case "add-host":
                        AddHost();
                        break;
                    case "remove-host":
                        string[] hostsToRemove;
                        if (args.Length > 2)
                        {
                            hostsToRemove = new string[args.Length - 1];
                            Array.Copy(args, 1, hostsToRemove, 0, args.Length - 1);
                        }
                        else
                        {
                            ShowKnownHosts();
                            Console.WriteLine("Enter hosts to remove");
                            hostsToRemove = Console.ReadLine().Split(' ');
                        }
                        for (int i = 0; i < hostsToRemove.Length; i++)
                        {
                            RemoveHost(hostsToRemove[i]);
                        }
                        break;
                }
            }
            else
            {
                Console.WriteLine("Available Commands:\n{0}\n{1}\n{2}\n{3}\n{4}\n{5}\n{6}\n{7}\n", "set-nic",
                    "show-current-nic", "wake {machine name}", "scan-network", "known-hosts", "assign-alias", "add-host",
                    "remove-host");
            }
        }

        private static void ShowCurrentInterface()
        {
            Console.WriteLine("{0} {1}", Nic.Name, Nic.AssignedIP);
        }

        private static void AddHost()
        {
            var host = new Host();
            Console.WriteLine("Enter a name for the host");
            host.Name = Console.ReadLine();
            while (Db.GetHostByName(host.Name) != null)
            {
                Console.WriteLine("Hostname already exists, choose a different hostname");
                host.Name = Console.ReadLine();
            }
            Console.WriteLine("Enter a MAC-Address for the host (use \":\" as delimiter");
            string macString = Console.ReadLine();

            try
            {
                host.MacAddress = macString.Split(':').Select(b => byte.Parse(b)).ToArray();
            } 
            catch (Exception)
            {
                Console.WriteLine("Couldn't parse MAC-address");
                return;
            }
            Console.WriteLine("Enter an alias for the host (optional)");
            host.Alias = Console.ReadLine();
            Console.WriteLine("Enter an IP-address for the host (optional)");
            var ipString = Console.ReadLine();

            byte ipPart;
            if (!String.IsNullOrEmpty(ipString) && ipString.Split('.').All(b => byte.TryParse(b, out ipPart)))
            {
                host.LastKnownIp = ipString;
                Db.SaveHost(host);
            }
        }

        private static void RemoveHost(string machine)
        {
            Db.RemoveHost(machine);
        }


        private static void AssignAlias()
        {
            int i = 0;
            var hosts = GetHosts();
            foreach (var host in hosts)
            {
                Console.WriteLine("{0}): {1} {2} {3}", i++, host.Name, host.LastKnownIp, BitConverter.ToString(host.MacAddress));
            }
            Console.WriteLine("Enter the number of the host you wish to assign an alias to");
            int aliasHost;
            if (Int32.TryParse(Console.ReadLine(), out aliasHost))
            {
                Console.WriteLine("Enter the alias");
                var alias = Console.ReadLine();
                var host = hosts[aliasHost];
                host.Alias = alias;
                Db.AssignAlias(host);
            }
        }

        private static void ShowKnownHosts()
        {
            foreach (var host in GetHosts())
            {
                Console.WriteLine("{0} {1} {2} \"{3}\"", host.Name, host.LastKnownIp, BitConverter.ToString(host.MacAddress), host.Alias);
            }
        }

        private static Host[] GetHosts()
        {
            return Db.GetHosts();
        }

        private static void Scan()
        {
            Console.WriteLine("Enter starting ip");
            byte start;
            start = byte.TryParse(Console.ReadLine(), out start) ? start : (byte)1;
            
            Console.WriteLine("Enter ending ip");
            byte end;
            end = byte.TryParse(Console.ReadLine(), out end) ? end : (byte)255;
            
            Console.WriteLine("Enter hosts to exclude");
            var exclusions = Console.ReadLine().Split(' ').Select(e => int.Parse(e));

            Console.WriteLine("Index - Host - MAC - IP - Known Host");
            var hosts = NetworkScanner.Scan(Nic.AssignedIP, exclusions.ToArray(), start, end).ToArray();
            int i = 0;
            foreach (var host in hosts)
            {
                var knownHost = Db.GetHostByMac(host.MacAddress);
                if (knownHost == null)
                {
                    Console.WriteLine("{0}): {1} {2} {3} {4}", i++, host.Name, BitConverter.ToString(host.MacAddress), host.LastKnownIp,
                        "No");
                }
                else
                {
                    Console.WriteLine("{0}): {1} \"{2}\" {3} {4} {5}", i++, knownHost.Name, knownHost.Alias, BitConverter.ToString(knownHost.MacAddress), host.LastKnownIp,
                        "Yes");
                }
            }

            Console.WriteLine("Enter the numbers of the hosts to save");

            foreach (var host in Console.ReadLine().Split(' '))
            {
                int parsedHost = 0;
                if (Int32.TryParse(host, out parsedHost))
                {
                    if (hosts[parsedHost].Name == "Unknown")
                    {
                        Console.WriteLine("{0} {1} doesn't have a name, enter one", BitConverter.ToString(hosts[parsedHost].MacAddress), hosts[parsedHost].LastKnownIp);
                        hosts[parsedHost].Name = Console.ReadLine();
                        while (Db.GetHostByName(hosts[parsedHost].Name) != null)
                        {
                            Console.WriteLine("Hostname already exists, enter a new one");
                            hosts[parsedHost].Name = Console.ReadLine();
                        }
                    }
                    Db.SaveHost(hosts[parsedHost]);
                }
            }
        }

        private static void SetInterface()
        {
            int i = 0;
            var nics = NetworkScanner.GetNetworkInterfaces();
            foreach (var nic in nics)
            {
                Console.WriteLine(i + "): " + nic.Name + " " + nic.AssignedIP);
                i++;
            }

            Console.WriteLine("Choose interface");
            int choice;
            if (Int32.TryParse(Console.ReadLine(), out choice) && choice < nics.Count() && choice >= 0)
            {
                Nic = nics.ElementAt(choice);
                Properties.Settings.Default.NICIP = Nic.AssignedIP;
                Properties.Settings.Default.NICName = Nic.Name;
                Properties.Settings.Default.Save();
            }
        }

        private static void Wake(string machine)
        {
            var host = Db.GetHostByName(machine);
            if (host != null)
            {
                Console.WriteLine("Waking {0}", (String.IsNullOrEmpty(host.Alias)) ? host.Name : host.Alias);
                NetworkScanner.Wake(host.MacAddress);
            }
            else
            {
                Console.WriteLine("Unknown name");
            }
        }
    }
}
