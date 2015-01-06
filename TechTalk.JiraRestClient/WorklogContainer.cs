using System;
using System.Collections.Generic;

namespace TechTalk.JiraRestClient
{
    internal class WorklogContainer<TWorklogFields> where TWorklogFields : WorklogFields, new()
    {
        public string expand { get; set; }

        public int maxResults { get; set; }
        public int total { get; set; }
        public int startAt { get; set; }

        public List<Worklog<TWorklogFields>> Worklogs { get; set; }
    }
}
