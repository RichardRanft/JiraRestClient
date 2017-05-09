using System;

namespace TechTalk.JiraRestClient
{
    public class IssueRef
    {
        public string id { get; set; }
        public string key { get; set; }
        public string JiraIdentifier
        {
            get { return key; }
            set { key = value; }
        }
    }
}
