using System;
using System.Collections.Generic;

namespace TechTalk.JiraRestClient
{
    public class WorklogFields
    {
        public int total { get; set; }
        public int startAt { get; set; }
        public int maxResults { get; set; }
        public List<WorklogEntry> worklogs { get; set; }

        public WorklogFields()
        {
            worklogs = new List<WorklogEntry>();
        }
    }
}
