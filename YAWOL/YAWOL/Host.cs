using SQLite;

namespace YAWOL
{
    class Host
    {
        [PrimaryKey]
        public byte[] MacAddress { get; set; }
        [Unique]
        public string Name { get; set; }
        [Unique]
        public string Alias { get; set; }
        [MaxLength(15)]
        public string LastKnownIp { get; set; }
    }
}
