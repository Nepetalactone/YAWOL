using System;
using System.Linq;
using SQLite;

namespace YAWOL
{
    class Database
    {
        private static SQLiteConnection _connection;

        public Database()
        {
            _connection = new SQLiteConnection("DB");
            _connection.CreateTable<Host>();
        }

        public void AssignAlias(Host host)
        {
            _connection.Update(host, typeof(Host));
        }

        public Host[] GetHosts()
        {
            return _connection.Table<Host>().ToArray();
        }

        public void SaveHost(Host host)
        {
            if (GetHostByMac(host.MacAddress) == null)
            {
                _connection.Insert(host);
            }
            else
            {
                Console.WriteLine("Skipping {0} because it already exists", BitConverter.ToString(host.MacAddress));
            }
        }

        public Host GetHostByName(string name)
        {
            return _connection.Table<Host>().FirstOrDefault(h => h.Name == name || h.Alias == name);
        }

        public Host GetHostByMac(byte[] mac)
        {
            return _connection.Table<Host>().FirstOrDefault(h => h.MacAddress.SequenceEqual(mac));
        }

        public void RemoveHost(string machine)
        {
            var hostToDelete = GetHostByName(machine);
            if (hostToDelete != null)
            {
                _connection.Delete<Host>(hostToDelete.MacAddress);
            }
        }
    }
}
