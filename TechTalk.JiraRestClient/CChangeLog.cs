using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TechTalk.JiraRestClient
{
    public class ChangeLog
    {
        public ChangeLog()
        {
            histories = new List<Histories>();
        }

        public int total { get; set; }
        public int startAt { get; set; }
        public int maxResults { get; set; }
        public List<Histories> histories { get; set; }
    }

    public class Histories
    {
        public Histories()
        {
            items = new List<HistoryItems>();
        }

        public string id { get; set; }
        public Author author { get; set; }
        public string created { get; set; }
        public List<HistoryItems> items { get; set; }
    }

    public class HistoryItems
    {
        public string field { get; set; }
        public string fieldtype { get; set; }
        public string from { get; set; }
        public string fromString { get; set; }
        public string to { get; set; }
        public string toString { get; set; }
    }
}
