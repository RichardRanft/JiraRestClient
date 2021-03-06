﻿using System;
using System.Collections.Generic;
using System.Globalization;

namespace TechTalk.JiraRestClient
{
    public class Worklog
    {
        public Worklog()
        {
            worklogs = new List<WorklogEntry>();
        }

        public int total { get; set; }
        public int startAt { get; set; }
        public int maxResults { get; set; }
        public List<WorklogEntry> worklogs { get; set; }
    }

    public class WorklogEntry
    {
        public WorklogEntry()
        {
            author = new Author();
            updateAuthor = new Author();
        }

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

    public class WorklogEntryFields
    {
        public WorklogEntryFields()
        {
            author = new Author();
            updateAuthor = new Author();
        }

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
    }
}
