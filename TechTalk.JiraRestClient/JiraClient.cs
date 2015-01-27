﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using RestSharp;
using RestSharp.Deserializers;
using RestSharp.Serializers;

namespace TechTalk.JiraRestClient
{
    //JIRA REST API documentation: https://docs.atlassian.com/jira/REST/latest

    public class JiraClient<TIssueFields> : IJiraClient<TIssueFields> where TIssueFields : IssueFields, new()
    {
        private readonly string username;
        private readonly string password;
        private readonly RestClient client;
        private readonly JsonDeserializer deserializer;
        private readonly JsonSerializer serializer;

        public JiraClient(string baseUrl, string username, string password)
        {
            this.username = username;
            this.password = password;
            deserializer = new JsonDeserializer();
            serializer = new JsonSerializer();
            client = new RestClient(baseUrl + (baseUrl.EndsWith("/") ? "" : "/") + "rest/api/2/");
        }

        private RestRequest CreateRequest(Method method, String path)
        {
            var request = new RestRequest { Method = method, Resource = path, RequestFormat = DataFormat.Json };
            request.AddHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(String.Format("{0}:{1}", username, password))));
            return request;
        }

        private void AssertStatus(IRestResponse response, HttpStatusCode status)
        {
            if (response.ErrorException != null)
                throw new JiraClientException("Transport level error: " + response.ErrorMessage, response.ErrorException);
            if (response.StatusCode != status)
                throw new JiraClientException("JIRA returned error status: " + response.StatusDescription, response.Content);
        }


        public ProjectList GetProjects()
        {
            var jql = String.Format("project");
            var path = String.Format("{0}?&fields&expand", jql);
            var request = CreateRequest(Method.GET, path);

            var response = client.Execute(request);
            AssertStatus(response, HttpStatusCode.OK);

            var projectData = deserializer.Deserialize<List<Project>>(response);
            ProjectList list = new ProjectList();
            list.projects = projectData;
            return list;
        }

        public Issue GetIssue(String issueKey)
        {
            var jql = String.Format("issue/{0}", Uri.EscapeUriString(issueKey));
            var path = String.Format("{0}?&fields&expand", jql);
            var request = CreateRequest(Method.GET, path);

            var response = client.Execute(request);
            AssertStatus(response, HttpStatusCode.OK);

            var issueData = deserializer.Deserialize<Issue<IssueFields>>(response);
            Issue issue = Issue.From(issueData);

            jql = String.Format("issue/{0}/worklog?search?&startAt={1}&maxResults={2}&fields&expand", Uri.EscapeUriString(issueKey), 0, 50);
            request = CreateRequest(Method.GET, jql);
            
            response = client.Execute(request);
            AssertStatus(response, HttpStatusCode.OK);

            var workData = deserializer.Deserialize<Worklog>(response);
            issue.fields.worklog = workData;

            return issue;
        }

        public Worklog CreateWorklog(String issueKey, Worklog worklog)
        {
            IRestResponse response = null;
            Worklog responseLog = new Worklog();
            foreach (WorklogEntry entry in worklog.worklogs)
            {
                var userData = new Dictionary<string, object>();
                var updateUser = new Dictionary<string, object>();
                var workData = new Dictionary<string, object>();
                if (entry.timeSpent != null)
                    workData.Add("timeSpent", entry.timeSpent);
                if(entry.author != null)
                {
                    if (entry.author.name != null)
                        userData.Add("name", entry.author.name);

                    userData.Add("active", entry.author.active);
                    workData.Add("author", userData);
                }
                if (entry.updateAuthor != null)
                {
                    if (entry.updateAuthor.name != null)
                        updateUser.Add("name", entry.author.name);

                    workData.Add("updateAuthor", updateUser);
                }
                if (entry.comment != null)
                    workData.Add("comment", entry.comment);
                if (entry.started != null)
                    workData.Add("started", entry.started);
                try
                {
                    var path = String.Format("issue/{0}/worklog", issueKey);
                    var request = CreateRequest(Method.POST, path);
                    request.AddHeader("ContentType", "application/json");
                    request.AddBody(workData);

                    response = client.Execute(request);
                    AssertStatus(response, HttpStatusCode.Created);

                    responseLog.worklogs.Add(deserializer.Deserialize<WorklogEntry>(response));
                }
                catch (Exception ex)
                {
                    Trace.TraceError("CreateWorklog(issue, worklog) error: {0}", ex);
                    String resp = "";
                    if (response != null)
                        resp = response.Content;
                    throw new JiraClientException("Could not create worklog:" + resp, ex);
                }
            }
            return responseLog;
        }

        public int GetWorklogCount(String issueKey)
        {
            var jql = String.Format("issue/{0}/worklog?search?&startAt={1}&maxResults={2}&fields&expand", Uri.EscapeUriString(issueKey), 0, 1);
            var request = CreateRequest(Method.GET, jql);

            var response = client.Execute(request);
            AssertStatus(response, HttpStatusCode.OK);

            var workData = deserializer.Deserialize<Worklog>(response);

            return workData.total;
        }

        public Worklog GetWorklog(String issueKey, int startAt = 0, int queryCount = 20)
        {
            var jql = String.Format("issue/{0}/worklog?search?&startAt={1}&maxResults={2}&fields&expand", Uri.EscapeUriString(issueKey), startAt, queryCount);
            var request = CreateRequest(Method.GET, jql);

            var response = client.Execute(request);
            AssertStatus(response, HttpStatusCode.OK);

            var workData = deserializer.Deserialize<Worklog>(response);

            return workData;
        }

        public IEnumerable<Issue<TIssueFields>> GetIssues(String projectKey)
        {
            return EnumerateIssues(projectKey, null).ToArray();
        }

        public IEnumerable<Issue<TIssueFields>> GetIssues(String projectKey, String issueType)
        {
            return EnumerateIssues(projectKey, issueType).ToArray();
        }

        public IEnumerable<Issue> GetIssuesQuery(Filter filter)
        {
            return EnumerateIssuesQuery(filter).ToArray();
        }

        public IEnumerable<Issue<TIssueFields>> EnumerateIssues(String projectKey)
        {
            return EnumerateIssues(projectKey, null);
        }

        public IEnumerable<Issue<TIssueFields>> EnumerateIssues(String projectKey, String issueType)
        {
            try
            {
                return EnumerateIssuesInternal(projectKey, issueType);
            }
            catch (Exception ex)
            {
                Trace.TraceError("EnumerateIssues(projectKey, issueType) error: {0}", ex);
                throw new JiraClientException("Could not load issues", ex);
            }
        }

        public IEnumerable<Issue> EnumerateIssuesQuery(Filter filter)
        {
            try
            {
                return EnumerateIssuesInternalQuery(filter);
            }
            catch (Exception ex)
            {
                Trace.TraceError("EnumerateIssues(query) error: {0}", ex);
                throw new JiraClientException("Could not load issues", ex);
            }
        }

        private IEnumerable<Issue<TIssueFields>> EnumerateIssuesInternal(String projectKey, String issueType)
        {
            var queryCount = 50;
            var resultCount = 0;
            while (true)
            {
                var jql = String.Format("project={0}", Uri.EscapeUriString(projectKey));
                if (!String.IsNullOrEmpty(issueType))
                    jql += String.Format("+AND+issueType={0}", Uri.EscapeUriString(issueType));
                var path = String.Format("search?jql={0}&startAt={1}&maxResults={2}", jql, resultCount, queryCount);
                var request = CreateRequest(Method.GET, path);

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.OK);

                var data = deserializer.Deserialize<IssueContainer<TIssueFields>>(response);
                var issues = data.issues ?? Enumerable.Empty<Issue<TIssueFields>>();

                foreach (var item in issues) yield return item;
                resultCount += issues.Count();

                if (resultCount < data.total) continue;
                else /* all issues received */ break;
            }
        }

        private IEnumerable<Issue> EnumerateIssuesInternalQuery(Filter filter)
        {
            var queryCount = 150;
            var resultCount = 0;
            while (true)
            {
                var path = String.Format("search?jql={0}&startAt={1}&maxResults={2}", filter.jql, resultCount, queryCount);
                var request = CreateRequest(Method.GET, path);

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.OK);

                var data = deserializer.Deserialize<IssueContainer<TIssueFields>>(response);
                var issues = data.issues ?? Enumerable.Empty<Issue<TIssueFields>>();

                foreach (var item in issues)
                {
                    Issue temp = new Issue();
                    temp.key = item.key;
                    temp.id = item.id;
                    temp.fields = item.fields;
                    yield return temp;
                }
                resultCount += issues.Count();

                if (resultCount < data.total) continue;
                else /* all issues received */ break;
            }
        }

        private Filter GetFilter(Filter filter)
        {
            FilterFields fields = new FilterFields();
            var jql = String.Format("filter?{0}", serializer.Serialize(fields));
            var request = CreateRequest(Method.POST, jql);

            var response = client.Execute(request);
            AssertStatus(response, HttpStatusCode.OK);

            var data = deserializer.Deserialize<Filter>(response);
            filter.searchUrl = data.searchUrl;
            
            return filter;
        }

        public Issue<TIssueFields> LoadIssue(IssueRef issueRef)
        {
            if (String.IsNullOrEmpty(issueRef.id))
                return LoadIssue(issueRef.key);
            else /* we have an id */
                return LoadIssue(issueRef.id);
        }

        public Issue<TIssueFields> LoadIssue(String issueRef)
        {
            try
            {
                var path = String.Format("issue/{0}", issueRef);
                var request = CreateRequest(Method.GET, path);

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.OK);

                var issue = deserializer.Deserialize<Issue<TIssueFields>>(response);
                issue.fields.comments = GetComments(issue).ToList();
                issue.fields.watchers = GetWatchers(issue).ToList();
                Issue.ExpandLinks(issue);
                return issue;
            }
            catch (Exception ex)
            {
                Trace.TraceError("GetIssue(issueRef) error: {0}", ex);
                throw new JiraClientException("Could not load issue", ex);
            }
        }

        public Issue<TIssueFields> CreateIssue(String projectKey, String issueType, String summary)
        {
            return CreateIssue(projectKey, issueType, new TIssueFields { summary = summary });
        }

        public Issue<TIssueFields> CreateIssue(String projectKey, String issueType, TIssueFields issueFields)
        {
            try
            {
                var request = CreateRequest(Method.POST, "issue");
                request.AddHeader("ContentType", "application/json");

                var issueData = new Dictionary<string, object>();
                issueData.Add("project", new { key = projectKey });
                issueData.Add("issuetype", new { name = issueType });

                if (issueFields.summary != null)
                    issueData.Add("summary", issueFields.summary);
                if (issueFields.description != null)
                    issueData.Add("description", issueFields.description);
                if (issueFields.labels != null)
                    issueData.Add("labels", issueFields.labels);

                if (issueFields.priority != null)
                {
                    var priorityData = new Dictionary<string, object>();
                    if (issueFields.priority.name != null)
                    {
                        priorityData.Add("name", issueFields.priority.name);
                    }
                    issueData.Add("priority", issueFields.priority);
                }

                var propertyList = typeof(TIssueFields).GetProperties().Where(p => p.Name.StartsWith("customfield_"));
                foreach (var property in propertyList)
                {
                    var value = property.GetValue(issueFields, null);
                    if (value != null) issueData.Add(property.Name, value);
                }

                request.AddBody(new { fields = issueData });

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.Created);

                var issueRef = deserializer.Deserialize<IssueRef>(response);
                return LoadIssue(issueRef);
            }
            catch (Exception ex)
            {
                Trace.TraceError("CreateIssue(projectKey, issueType, issueFields) error: {0}", ex);
                throw new JiraClientException("Could not create issue", ex);
            }
        }

        public Issue<TIssueFields> CreateIssue(String projectKey, String issueType, Issue issue)
        {
            IRestResponse response = null;
            try
            {
                var request = CreateRequest(Method.POST, "issue");
                request.AddHeader("ContentType", "application/json");
                request.AddBody(serializer.Serialize(issue));

                response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.Created);

                var issueRef = deserializer.Deserialize<IssueRef>(response);
                return LoadIssue(issueRef);
            }
            catch (Exception ex)
            {
                Trace.TraceError("CreateIssue(projectKey, issueType, issue) error: {0}", ex);
                String resp = "";
                if(response != null)
                    resp = response.Content;
                throw new JiraClientException("Could not create issue:" + resp, ex);
            }
        }

        public Issue ProgressWorkflowAction(String issueKey, String action, String actionID)
        {
            try
            {
                var path = String.Format("issue/{0}/transitions", issueKey);
                var request = CreateRequest(Method.POST, path);
                request.AddHeader("ContentType", "application/json");
                var trans = new Dictionary<String, object>();
                trans.Add("id", actionID);
                trans.Add("name", action);
                var transTo = new Dictionary<String, String>();
                transTo.Add("id", "10000");
                transTo.Add("name", "Done");
                trans.Add("to", transTo);
                request.AddBody(new { transition = trans } );

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.NoContent);

                return GetIssue(issueKey);
            }
            catch (Exception ex)
            {
                Trace.TraceError("ProgressWorkflowAction(issueKey, action, actionID) error: {0}", ex);
                throw new JiraClientException("Could not update issue", ex);
            }
        }

        public Issue<TIssueFields> UpdateIssue(Issue<TIssueFields> issue)
        {
            try
            {
                var path = String.Format("issue/{0}", issue.id);
                var request = CreateRequest(Method.PUT, path);
                request.AddHeader("ContentType", "application/json");

                var updateData = new Dictionary<string, object>();
                if (issue.fields.summary != null)
                    updateData.Add("summary", new[] { new { set = issue.fields.summary } });
                if (issue.fields.description != null)
                    updateData.Add("description", new[] { new { set = issue.fields.description } });
                if (issue.fields.labels != null)
                    updateData.Add("labels", new[] { new { set = issue.fields.labels } });
                if (issue.fields.timetracking != null)
                    updateData.Add("timetracking", new[] { new { set = new { originalEstimate = issue.fields.timetracking.originalEstimate } } });

                var propertyList = typeof(TIssueFields).GetProperties().Where(p => p.Name.StartsWith("customfield_"));
                foreach (var property in propertyList)
                {
                    var value = property.GetValue(issue.fields, null);
                    if (value != null) updateData.Add(property.Name, new[] { new { set = value } });
                }

                request.AddBody(new { update = updateData });

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.NoContent);

                return LoadIssue(issue);
            }
            catch (Exception ex)
            {
                Trace.TraceError("UpdateIssue(issue) error: {0}", ex);
                throw new JiraClientException("Could not update issue", ex);
            }
        }

        public void DeleteIssue(IssueRef issue)
        {
            try
            {
                var path = String.Format("issue/{0}?deleteSubtasks=true", issue.id);
                var request = CreateRequest(Method.DELETE, path);

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.NoContent);
            }
            catch (Exception ex)
            {
                Trace.TraceError("DeleteIssue(issue) error: {0}", ex);
                throw new JiraClientException("Could not delete issue", ex);
            }
        }


        public IEnumerable<Transition> GetTransitions(IssueRef issue)
        {
            try
            {
                var path = String.Format("issue/{0}/transitions?expand=transitions.fields", issue.id);
                var request = CreateRequest(Method.GET, path);

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.OK);

                var data = deserializer.Deserialize<TransitionsContainer>(response);
                return data.transitions;
            }
            catch (Exception ex)
            {
                Trace.TraceError("GetTransitions(issue) error: {0}", ex);
                throw new JiraClientException("Could not load issue transitions", ex);
            }
        }

        public Issue<TIssueFields> TransitionIssue(IssueRef issue, Transition transition)
        {
            try
            {
                var path = String.Format("issue/{0}/transitions", issue.id);
                var request = CreateRequest(Method.POST, path);
                request.AddHeader("ContentType", "application/json");

                var update = new Dictionary<string, object>();
                update.Add("transition", new { id = transition.id });
                if (transition.fields != null)
                    update.Add("fields", transition.fields);

                request.AddBody(update);

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.NoContent);

                return LoadIssue(issue);
            }
            catch (Exception ex)
            {
                Trace.TraceError("TransitionIssue(issue, transition) error: {0}", ex);
                throw new JiraClientException("Could not transition issue state", ex);
            }
        }


        public IEnumerable<JiraUser> GetWatchers(IssueRef issue)
        {
            try
            {
                var path = String.Format("issue/{0}/watchers", issue.id);
                var request = CreateRequest(Method.GET, path);

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.OK);

                return deserializer.Deserialize<WatchersContainer>(response).watchers;
            }
            catch (Exception ex)
            {
                Trace.TraceError("GetWatchers(issue) error: {0}", ex);
                throw new JiraClientException("Could not load watchers", ex);
            }
        }


        public IEnumerable<Comment> GetComments(IssueRef issue)
        {
            try
            {
                var path = String.Format("issue/{0}/comment", issue.id);
                var request = CreateRequest(Method.GET, path);

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.OK);

                var data = deserializer.Deserialize<CommentsContainer>(response);
                return data.comments ?? Enumerable.Empty<Comment>();
            }
            catch (Exception ex)
            {
                Trace.TraceError("GetComments(issue) error: {0}", ex);
                throw new JiraClientException("Could not load comments", ex);
            }
        }

        public Comment CreateComment(IssueRef issue, String comment)
        {
            try
            {
                var path = String.Format("issue/{0}/comment", issue.id);
                var request = CreateRequest(Method.POST, path);
                request.AddHeader("ContentType", "application/json");
                request.AddBody(new Comment { body = comment });

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.Created);

                return deserializer.Deserialize<Comment>(response);
            }
            catch (Exception ex)
            {
                Trace.TraceError("CreateComment(issue, comment) error: {0}", ex);
                throw new JiraClientException("Could not create comment", ex);
            }
        }

        public void DeleteComment(IssueRef issue, Comment comment)
        {
            try
            {
                var path = String.Format("issue/{0}/comment/{1}", issue.id, comment.id);
                var request = CreateRequest(Method.DELETE, path);

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.NoContent);
            }
            catch (Exception ex)
            {
                Trace.TraceError("DeleteComment(issue, comment) error: {0}", ex);
                throw new JiraClientException("Could not delete comment", ex);
            }
        }


        public IEnumerable<Attachment> GetAttachments(IssueRef issue)
        {
            return LoadIssue(issue).fields.attachment;
        }

        public Attachment CreateAttachment(IssueRef issue, Stream fileStream, String fileName)
        {
            try
            {
                var path = String.Format("issue/{0}/attachments", issue.id);
                var request = CreateRequest(Method.POST, path);
                request.AddHeader("X-Atlassian-Token", "nocheck");
                request.AddHeader("ContentType", "multipart/form-data");
                request.AddFile("file", stream => fileStream.CopyTo(stream), fileName);

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.OK);

                return deserializer.Deserialize<List<Attachment>>(response).Single();
            }
            catch (Exception ex)
            {
                Trace.TraceError("CreateAttachment(issue, fileStream, fileName) error: {0}", ex);
                throw new JiraClientException("Could not create attachment", ex);
            }
        }

        public void DeleteAttachment(Attachment attachment)
        {
            try
            {
                var path = String.Format("attachment/{0}", attachment.id);
                var request = CreateRequest(Method.DELETE, path);

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.NoContent);
            }
            catch (Exception ex)
            {
                Trace.TraceError("DeleteAttachment(attachment) error: {0}", ex);
                throw new JiraClientException("Could not delete attachment", ex);
            }
        }


        public IEnumerable<IssueLink> GetIssueLinks(IssueRef issue)
        {
            return LoadIssue(issue).fields.issuelinks;
        }

        public IssueLink LoadIssueLink(IssueRef parent, IssueRef child, String relationship)
        {
            try
            {
                var issue = LoadIssue(parent);
                var links = issue.fields.issuelinks
                    .Where(l => l.type.name == relationship)
                    .Where(l => l.inwardIssue.id == parent.id)
                    .Where(l => l.outwardIssue.id == child.id)
                    .ToArray();

                if (links.Length > 1)
                    throw new JiraClientException("Ambiguous issue link");
                return links.SingleOrDefault();
            }
            catch (Exception ex)
            {
                Trace.TraceError("LoadIssueLink(parent, child, relationship) error: {0}", ex);
                throw new JiraClientException("Could not load issue link", ex);
            }
        }

        public IssueLink CreateIssueLink(IssueRef parent, IssueRef child, String relationship)
        {
            try
            {
                var request = CreateRequest(Method.POST, "issueLink");
                request.AddHeader("ContentType", "application/json");
                request.AddBody(new
                {
                    type = new { name = relationship },
                    inwardIssue = new { id = parent.id },
                    outwardIssue = new { id = child.id }
                });

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.Created);

                return LoadIssueLink(parent, child, relationship);
            }
            catch (Exception ex)
            {
                Trace.TraceError("CreateIssueLink(parent, child, relationship) error: {0}", ex);
                throw new JiraClientException("Could not link issues", ex);
            }
        }

        public void DeleteIssueLink(IssueLink link)
        {
            try
            {
                var path = String.Format("issueLink/{0}", link.id);
                var request = CreateRequest(Method.DELETE, path);

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.NoContent);
            }
            catch (Exception ex)
            {
                Trace.TraceError("DeleteIssueLink(link) error: {0}", ex);
                throw new JiraClientException("Could not delete issue link", ex);
            }
        }


        public IEnumerable<RemoteLink> GetRemoteLinks(IssueRef issue)
        {
            try
            {
                var path = string.Format("issue/{0}/remotelink", issue.id);
                var request = CreateRequest(Method.GET, path);
                request.AddHeader("ContentType", "application/json");

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.OK);

                return deserializer.Deserialize<List<RemoteLinkResult>>(response)
                    .Select(RemoteLink.Convert).ToList();
            }
            catch (Exception ex)
            {
                Trace.TraceError("GetRemoteLinks(issue) error: {0}", ex);
                throw new JiraClientException("Could not load external links for issue", ex);
            }
        }

        public RemoteLink CreateRemoteLink(IssueRef issue, RemoteLink remoteLink)
        {
            try
            {
                var path = string.Format("issue/{0}/remotelink", issue.id);
                var request = CreateRequest(Method.POST, path);
                request.AddHeader("ContentType", "application/json");
                request.AddBody(new
                {
                    application = new
                    {
                        type = "TechTalk.JiraRestClient",
                        name = "JIRA REST client"
                    },
                    @object = new
                    {
                        url = remoteLink.url,
                        title = remoteLink.title,
                        summary = remoteLink.summary
                    }
                });

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.Created);

                //returns: { "id": <id>, "self": <url> }
                var linkId = deserializer.Deserialize<RemoteLink>(response).id;
                return GetRemoteLinks(issue).Single(rl => rl.id == linkId);
            }
            catch (Exception ex)
            {
                Trace.TraceError("CreateRemoteLink(issue, remoteLink) error: {0}", ex);
                throw new JiraClientException("Could not create external link for issue", ex);
            }
        }

        public RemoteLink UpdateRemoteLink(IssueRef issue, RemoteLink remoteLink)
        {
            try
            {
                var path = string.Format("issue/{0}/remotelink/{1}", issue.id, remoteLink.id);
                var request = CreateRequest(Method.PUT, path);
                request.AddHeader("ContentType", "application/json");

                var updateData = new Dictionary<string, object>();
                if (remoteLink.url != null) updateData.Add("url", remoteLink.url);
                if (remoteLink.title != null) updateData.Add("title", remoteLink.title);
                if (remoteLink.summary != null) updateData.Add("summary", remoteLink.summary);
                request.AddBody(new { @object = updateData });

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.NoContent);

                return GetRemoteLinks(issue).Single(rl => rl.id == remoteLink.id);
            }
            catch (Exception ex)
            {
                Trace.TraceError("UpdateRemoteLink(issue, remoteLink) error: {0}", ex);
                throw new JiraClientException("Could not update external link for issue", ex);
            }
        }

        public void DeleteRemoteLink(IssueRef issue, RemoteLink remoteLink)
        {
            try
            {
                var path = string.Format("issue/{0}/remotelink/{1}", issue.id, remoteLink.id);
                var request = CreateRequest(Method.DELETE, path);
                request.AddHeader("ContentType", "application/json");

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.NoContent);
            }
            catch (Exception ex)
            {
                Trace.TraceError("DeleteRemoteLink(issue, remoteLink) error: {0}", ex);
                throw new JiraClientException("Could not delete external link for issue", ex);
            }
        }

        public IEnumerable<IssueType> GetIssueTypes()
        {
            try
            {
                var request = CreateRequest(Method.GET, "issuetype");
                request.AddHeader("ContentType", "application/json");

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.OK);

                var data = deserializer.Deserialize<List<IssueType>>(response);
                return data;

            }
            catch (Exception ex)
            {
                Trace.TraceError("GetIssueTypes() error: {0}", ex);
                throw new JiraClientException("Could not load issue types", ex);
            }
        }

        public ServerInfo GetServerInfo()
        {
            try
            {
                var request = CreateRequest(Method.GET, "serverInfo");
                request.AddHeader("ContentType", "application/json");

                var response = client.Execute(request);
                AssertStatus(response, HttpStatusCode.OK);

                return deserializer.Deserialize<ServerInfo>(response);
            }
            catch (Exception ex)
            {
                Trace.TraceError("GetServerInfo() error: {0}", ex);
                throw new JiraClientException("Could not retrieve server information", ex);
            }
        }
    }
}
