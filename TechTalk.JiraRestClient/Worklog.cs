using System;
using System.Collections.Generic;
using System.Globalization;

namespace TechTalk.JiraRestClient
{
    //public class 
    //{
    //    public int total { get; set; }
    //    public int startAt { get; set; }
    //    public int maxResults { get; set; }
    //    public List<WorklogEntry> worklogs { get; set; }
    //    public IEnumerator<WorklogEntry> GetEnumerator()
    //    {
    //        return worklogs.GetEnumerator();
    //    }
    //}
    public class Worklog<TWorklogFields> : WorklogRef where TWorklogFields : WorklogFields, new()
    {
        public Worklog() { fields = new TWorklogFields(); }

        public string expand { get; set; }

        public string self { get; set; }

        public TWorklogFields fields { get; set; }
    }

    public class WorklogEntry
    {
        public string started { get; set; }
        public string timeSpent { get; set; }
        public string comment { get; set; }
        public string created { get; set; }
        public string updated { get; set; }
        public string id { get; set; }
        public string self { get; set; }
        public Author author { get; set; }
        public Author updateAuthor { get; set; }
        public int timeSpentSeconds { get; set; }

        private const decimal DayToSecFactor = 8 * 3600;
        public decimal timeSpentDays
        {
            get
            {
                return (decimal)timeSpentSeconds / DayToSecFactor;
            }
            set
            {
                timeSpent = string.Format(CultureInfo.InvariantCulture, "{0}d", value);
                timeSpentSeconds = (int)(value * DayToSecFactor);
            }
        }
    }
}
