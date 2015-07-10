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
            _connection.Insert(host);
        }

        public Host GetHostByName(string name)
        {
            return _connection.Table<Host>().FirstOrDefault(h => h.Name == name || h.Alias == name);
        }

        public void RemoveHost(string machine)
        {
            Host hostToDelete = GetHostByName(machine);
            if (hostToDelete != null)
            {
                _connection.Delete<Host>(hostToDelete.MacAddress);
            }
        }
    }
}
