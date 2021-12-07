﻿namespace PgOutput2Json.Core
{
    public class ReplicationListenerOptions
    {
        public string ConnectionString { get; set; }
        public string PublicationName { get; set; }
        public string ReplicationSlotName { get; set; }

        public Dictionary<string, PartionConfig> Partitions { get; set; } 
            = new Dictionary<string, PartionConfig>();

        /// <summary>
        /// Should nulls be written to JSON output. Default is false.
        /// </summary>
        public bool WriteNulls { get; set; } = false;

        

        public ReplicationListenerOptions(string connectionString,
                                          string publicationName,
                                          string replicationSlotName)
        {
            ConnectionString = connectionString;
            PublicationName = publicationName;
            ReplicationSlotName = replicationSlotName;
        }
    }
}
