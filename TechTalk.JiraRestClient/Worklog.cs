using System;
using System.Globalization;

namespace TechTalk.JiraRestClient
{
    public class Worklog
    {
        public string started { get; set; }
        public string timeSpent { get; set; }
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
