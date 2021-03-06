﻿using System;
using System.Collections.Generic;

namespace TechTalk.JiraRestClient
{
    public class IssueFields
    {
        public IssueFields()
        {
            status = new Status();
            priority = new Priority();
            timetracking = new Timetracking();
            worklog = new Worklog();
            issuetype = new IssueType();
            labels = new List<String>();
            comments = new List<Comment>();
            issuelinks = new List<IssueLink>();
            attachment = new List<Attachment>();
            watchers = new List<JiraUser>();
            subtasks = new List<Issue>();
            changelog = new ChangeLog();
            customfield = new List<KeyValuePair<String, String>>();
        }

        public String summary { get; set; }
        public String description { get; set; }
        public Timetracking timetracking { get; set; }
        public Worklog worklog { get; set; }
        public Status status { get; set; }
        public Priority priority { get; set; }
        public IssueType issuetype { get; set; }

        public JiraUser reporter { get; set; }
        public JiraUser assignee { get; set; }
        public ChangeLog changelog { get; set; }
        public List<JiraUser> watchers { get; set; } 

        public List<String> labels { get; set; }
        public List<Comment> comments { get; set; }
        public List<IssueLink> issuelinks { get; set; }
        public List<Attachment> attachment { get; set; }
        public List<Issue> subtasks { get; set; }
        public List<KeyValuePair<String, String>> customfield { get; set; }
    }
}
