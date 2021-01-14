namespace NCode.Identity.DataContracts
{
    public class Secret : ISupportId
    {
        public long Id { get; set; }

        public string KeyId { get; set; }

        public string Encoding { get; set; }

        public string Algorithm { get; set; }

        public string Type { get; set; }

        public string Value { get; set; }
    }
}
