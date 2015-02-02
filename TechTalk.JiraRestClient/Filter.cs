using System;
using System.Collections.Generic;
using System.Globalization;

namespace TechTalk.JiraRestClient
{
    public class Filter
    {
        public String id { get; set; }
        public String name { get; set; }
        public String description { get; set; }
        public Author owner { get; set; }
        public String jql { get; set; }
        public String searchUrl { get; set; }

        public Filter()
        {
            owner = new Author();
        }
    }

    public class FilterFields
    {
        public String name { get; set; }
        public String description { get; set; }
        public String jql { get; set; }
        public String favorite { get; set; }
    }
}
