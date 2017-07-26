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
                        foreach (string host in hostsToWake)
                        {
                            Wake(host);
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
                        foreach (string host in hostsToRemove)
                        {
                            RemoveHost(host);
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
            Host host = new Host();
            Console.WriteLine("Enter a name for the host");
            host.Name = Console.ReadLine();
            while (Db.GetHostByName(host.Name) != null)
            {
                Console.WriteLine("Hostname already exists, choose a different hostname");
                host.Name = Console.ReadLine();
            }
            Console.WriteLine("Enter a MAC-Address for the host (use \":\" as delimiter");
            var macString = Console.ReadLine();

            int i = 0;
            var macAddress = new byte[6];
            foreach (var part in macString.Split(':'))
            {
                byte macPart;
                if (byte.TryParse(part, out macPart))
                {
                    macAddress[i] = macPart;
                }
                else
                {
                    Console.WriteLine("Couldn't parse MAC-address");
                    return;
                }
                i++;
            }
            host.MacAddress = macAddress;
            Console.WriteLine("Enter an alias for the host (optional)");
            host.Alias = Console.ReadLine();
            Console.WriteLine("Enter an IP-address for the host (optional)");
            var ipString = Console.ReadLine();

            if (!String.IsNullOrEmpty(ipString))
            {
                foreach (var part in ipString.Split('.'))
                {
                    int ipPart;
                    if (!Int32.TryParse(part, out ipPart) && ipPart < 256 && ipPart >= 0)
                    {
                        Console.WriteLine("Couldn't parse IP-address");
                        return;
                    }
                }
                host.LastKnownIp = ipString;
            }
            Db.SaveHost(host);
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
                string alias = Console.ReadLine();
                var host = hosts.ElementAt(aliasHost);
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

        private static IEnumerable<Host> GetHosts()
        {
            return Db.GetHosts();
        }

        private static void Scan()
        {
            Console.WriteLine("Enter starting ip");
            byte start;
            start = byte.TryParse(Console.ReadLine(), out start) ? start : byte.MinValue;

            Console.WriteLine("Enter ending ip");
            byte end;
            end = byte.TryParse(Console.ReadLine(), out end) ? end : byte.MaxValue;

            Console.WriteLine("Enter hosts to exclude");
            List<int> exclusions = new List<int>();
            foreach (var number in Console.ReadLine().Split(' '))
            {
                int parsedNumber;
                if (Int32.TryParse(number, out parsedNumber))
                {
                    exclusions.Add(parsedNumber);
                }
            }

            Console.Clear();

            const string hostFormat = "{0,-5} {1,-25} {2,-17} {3,-12} {4,-3}";
            Console.WriteLine(hostFormat, "Index", "Host", "MAC", "IP", "Known Host");

            var hosts = NetworkScanner.Scan(Nic.AssignedIP, exclusions.ToArray(), start, end);
            int i = 0;
            foreach (var host in hosts)
            {
                Host knownHost = Db.GetHostByMac(host.MacAddress);

                Console.WriteLine(hostFormat, i++ + "):", knownHost == null ? host.Name : Db.GetHostByMac(host.MacAddress).Name, BitConverter.ToString(host.MacAddress),
                        host.LastKnownIp,
                        knownHost == null ? "No" : "Yes");
            }

            Console.WriteLine("Enter the numbers of the hosts to save");

            foreach (var host in Console.ReadLine().Split(' '))
            {
                int parsedHost = 0;
                if (Int32.TryParse(host, out parsedHost))
                {
                    if (hosts.ElementAt(parsedHost).Name == "Unknown")
                    {
                        Console.WriteLine("{0} {1} doesn't have a name, enter one", BitConverter.ToString(hosts.ElementAt(parsedHost).MacAddress), hosts.ElementAt(parsedHost).LastKnownIp);
                        hosts.ElementAt(parsedHost).Name = Console.ReadLine();
                        while (Db.GetHostByName(hosts.ElementAt(parsedHost).Name) != null)
                        {
                            Console.WriteLine("Hostname already exists, enter a new one");
                            hosts.ElementAt(parsedHost).Name = Console.ReadLine();
                        }
                    }
                    Db.SaveHost(hosts.ElementAt(parsedHost));
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
            Host host = Db.GetHostByName(machine);
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
