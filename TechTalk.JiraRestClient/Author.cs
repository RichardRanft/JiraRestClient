using System;
using System.Globalization;

namespace TechTalk.JiraRestClient
{
    public class Author
    {
        public string self { get; set; }
        public string name { get; set; }
        public string displayName { get; set; }
        public bool active { get; set; }
    }
}
