namespace YAWOL
{
    class NIC
    {
        public string Name { get; set; }
        public string AssignedIP { get; set; }

        public NIC(string name, string assignedIP)
        {
            Name = name;
            AssignedIP = assignedIP;
        }
    }
}
