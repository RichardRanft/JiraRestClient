using System;
using System.Collections.Generic;
using System.Globalization;

namespace TechTalk.JiraRestClient
{
    public class ProjectList
    {
        public List<Project> projects { get; set; }
        public IEnumerator<Project> GetEnumerator()
        {
            return projects.GetEnumerator();
        }
    }

    public class Project
    {
        public string id { get; set; }
        public string self { get; set; }
        public string key { get; set; }
        public string name { get; set; }
        public ProjectCategory projectCategory { get; set; }
    }

    public class ProjectCategory
    {
        public string id { get; set; }
        public string self { get; set; }
        public string name { get; set; }
        public string description { get; set; }
    }
}
