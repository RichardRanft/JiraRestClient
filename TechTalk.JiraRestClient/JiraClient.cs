using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using RestSharp;
using RestSharp.Deserializers;
using RestSharp.Serializers;
using System.Text.RegularExpressions;

namespace TechTalk.JiraRestClient
{
    //JIRA REST API documentation: https://docs.atlassian.com/jira/REST/latest

    public class JiraClient<TIssueFields> : IJiraClient<TIssueFields> where TIssueFields : IssueFields, new()
    {
        private readonly string username;
        private readonly string password;
        private readonly JsonDeserializer deserializer;
        private readonly JsonSerializer serializer;
        private readonly string baseApiUrl;
        private RestClient client;

        /* Summary
           The constructor takes three parameters and creates a JIRA
           client object for interacting with a JIRA server.
           Parameters
           baseUrl :   The base URL of the server \-
                       http\://myJiraServer.com\:2020/
           username :  The user name to log in with
           password :  The user's password
           
           Remarks
           The base URL should have the trailing slash character as this
           string is not altered and the internal handling of this path
           does not add it for the consumer.
           
           The user account used to log in will need permission to add
           and edit issues within the project that you intend to
           interact with if you intend to add or update issues. Obviously,
           the account will need read access to even retrieve issue
           information.
           C# Syntax
           <c>   String address = "http://myJiraServer.com:2020/";</c>
           <c>   String user = "JIRA_Automation";</c>
           <c>   String password = "AutoPass88";</c>
           <c>   JiraClient client = new JiraClient(address, user, password);</c> */
        public JiraClient(string baseUrl, string username, string password)
        {
            this.username = username;
            this.password = password;
            
            baseApiUrl = new Uri(new Uri(baseUrl), "rest/api/2/").ToString();
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

        private IRestResponse ExecuteRequest(RestRequest request)
        {
            var client = new RestClient(baseApiUrl);
            return client.Execute(request);
        }

        private void AssertStatus(IRestResponse response, HttpStatusCode status)
        {
            if (response.ErrorException != null)
                throw new JiraClientException("Transport level error: " + response.ErrorMessage, response.ErrorException);
            if (response.StatusCode != status)
                throw new JiraClientException("JIRA returned error status: " + response.StatusDescription, response.Content);
        }


        /* Summary
           Gets a list of all projects on the connected JIRA server.
           
           
           Returns
           A <link TechTalk.JiraRestClient.ProjectList, ProjectList>
           object that contains a list of <link TechTalk.JiraRestClient.Project, Project>
           data objects.                                                                  */
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

        /* Summary
           Retrieve the issue with the specified key.
           Parameters
           issueKey :  The key of the issue to retrieve
           
           Returns
           An <link TechTalk.JiraRestClient.Issue, Issue> object
           corresponding to the specified key.                   */
        public Issue GetIssue(String issueKey)
        {
            var jql = String.Format("issue/{0}", Uri.EscapeUriString(issueKey));
            var path = String.Format("{0}?&fields&expand", jql);
            var request = CreateRequest(Method.GET, path);

            var response = client.Execute(request);
            AssertStatus(response, HttpStatusCode.OK);

            var issueData = deserializer.Deserialize<Issue<IssueFields>>(response);
            Issue issue = Issue.From(issueData);

            String[] respParts = response.Content.Split(',');
            foreach (String part in respParts)
            {
                if(part.Contains("customfield_"))
                {
                    String[] pieces = part.Split(':');
                    if (pieces[1] != "null")
                        issue.fields.customfield.Add(new KeyValuePair<String, String>(pieces[0], pieces[1]));
                }
            }

            jql = String.Format("issue/{0}/worklog?search?&startAt={1}&maxResults={2}&fields&expand", Uri.EscapeUriString(issueKey), 0, 50);
            request = CreateRequest(Method.GET, jql);
            
            response = client.Execute(request);
            AssertStatus(response, HttpStatusCode.OK);

            var workData = deserializer.Deserialize<Worklog>(response);
            issue.fields.worklog = workData;

            return issue;
        }

        /* Summary
           Gets the issue transaction history from the History tab of
           the specified JIRA issue.
           Parameters
           issueKey :  The issue key to retrieve change logs for
           Returns
           \Returns a <link TechTalk.JiraRestClient.ChangeLog, ChangeLog>
           object with the issue's history.                               */
        public ChangeLog GetIssueChangelog(String issueKey)
        {
            var jql = String.Format("issue/{0}", issueKey);
            var path = String.Format("{0}?search?&startAt={1}&maxResults={2}&expand=changelog&fields=\"\"", jql, 0, 50);
            var request = CreateRequest(Method.GET, path);

            var response = client.Execute(request);
            AssertStatus(response, HttpStatusCode.OK);
            CParser parser = new CParser();
            response.Content = parser.StripChangelog(response.Content);
            var issueData = deserializer.Deserialize<ChangeLog>(response);

            return issueData;
        }

        /* Summary
           Gets the <link TechTalk.JiraRestClient.EditMeta, EditMeta>
           data for the requested issue key. The <link TechTalk.JiraRestClient.EditMeta, EditMeta>
           is form data for the screen - in this case the edit issue
           screen - so it contains all available fields for the
           requested screen.
           Parameters
           issueKey :  The issue key to retrieve data for
           
           Returns
           The <link TechTalk.JiraRestClient.EditMeta, EditMeta> object
           for the requested issue key.                                                            */
        public EditMeta GetEditMeta(String issueKey)
        {
            var jql = String.Format("issue/{0}/editmeta", issueKey);
            var path = String.Format("{0}?&fields&expand", jql);
            var request = CreateRequest(Method.GET, path);

            var response = client.Execute(request);
            AssertStatus(response, HttpStatusCode.OK);

            var issueData = deserializer.Deserialize<EditMeta>(response);

            issueData.GetCustomFields(response.Content);

            return issueData;
        }

        /* Summary
           Creates a worklog entry and adds it to the requested issue.
           Parameters
           issueKey :  The key of the issue to add the worklog to
           worklog :   The new worklog to add to the issue
           
           Returns
           The newly created <link TechTalk.JiraRestClient.Worklog, Worklog>
           object from JIRA.                                                 */
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

        /* Summary
           Gets the number of work log entries associated with the
           specified key.
           Parameters
           issueKey :  The key of the issue to get the work log count for.
           
           Returns
           The number of work log entries in the issue specified.          */
        public int GetWorklogCount(String issueKey)
        {
            var jql = String.Format("issue/{0}/worklog?search?&startAt={1}&maxResults={2}&fields&expand", Uri.EscapeUriString(issueKey), 0, 1);
            var request = CreateRequest(Method.GET, jql);

            var response = client.Execute(request);
            AssertStatus(response, HttpStatusCode.OK);

            var workData = deserializer.Deserialize<Worklog>(response);

            return workData.total;
        }

        /* Summary
           Gets filled Worklog data for the specified issue key.
           Parameters
           issueKey :    The issue key to get data for
           startAt :     The index to start the data collection from
           queryCount :  The number of entries to retrieve
           
           Returns
           A <link TechTalk.JiraRestClient.Worklog, Worklog> object
           containing the time keeping data requested.               */
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

        /* Summary
           Gets all issues on the server that match the provided JQL
           query.
           Parameters
           filter :  The search query
           Returns
           An IEnumerable\<<link TechTalk.JiraRestClient.Issue, Issue>\>
           of all issues on the server matching the provided JQL query.
           Remarks
           JIRA uses a unique flavor of JQL that is Lucene-based - so
           please refer to <extlink https://confluence.atlassian.com/jira/performing-text-searches-185729616.html>Performing
           Text Searches</extlink> and <extlink https://confluence.atlassian.com/jira/advanced-searching-179442050.html>Advanced
           Searching</extlink> in Atlassian's JIRA documentation if
           you're not getting the results you expect.                                                                            */
        public IEnumerable<Issue> GetIssuesQuery(Filter filter)
        {
            return EnumerateIssuesQuery(filter).ToArray();
        }

        /* Summary
           \Returns a list of issues from the indicated project of the
           requested type that match the provided JQL query.
           Parameters
           projectKey :  The project to search
           issueType :   The desired issue type
           jqlQuery :    A string containing the JQL query to use for
                         the search
           
           Returns
           A List\<Issue\<TIssueFields\>\> containing the issues found
           using the provided query.
           Remarks
           JIRA uses a unique flavor of JQL that is Lucene-based - so
           please refer to <extlink https://confluence.atlassian.com/jira/performing-text-searches-185729616.html>Performing
           Text Searches</extlink> and <extlink https://confluence.atlassian.com/jira/advanced-searching-179442050.html>Advanced
           Searching</extlink> in Atlassian's JIRA documentation if
           you're not getting the results you expect.                                                                            */
        public IEnumerable<Issue<TIssueFields>> GetIssuesByQuery(string projectKey, string issueType, string jqlQuery)
        {
            return EnumerateIssuesInternal(projectKey, issueType, jqlQuery);
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

        /* Summary
           Get a list of issues using a JQL query.
           Parameters
           filter :  A string containing a JQL query
           
           Returns
           An IEnumerable\<<link TechTalk.JiraRestClient.Issue, Issue>\>
           of issues that satisfy the query.
           Remarks
           JIRA uses a unique flavor of JQL that is Lucene-based - so
           please refer to <extlink https://confluence.atlassian.com/jira/performing-text-searches-185729616.html>Performing
           Text Searches</extlink> and <extlink https://confluence.atlassian.com/jira/advanced-searching-179442050.html>Advanced
           Searching</extlink> in Atlassian's JIRA documentation if
           you're not getting the results you expect.                                                                            */
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
            return EnumerateIssuesInternal(projectKey, issueType);
        }

        private IEnumerable<Issue<TIssueFields>> EnumerateIssuesInternal(String projectKey, String issueType, String jqlQuery = null)
        {
            var queryCount = 50;
            var resultCount = 0;
            while (true)
            {
                var jql = String.Format("project={0}", Uri.EscapeUriString(projectKey));
                if (!String.IsNullOrEmpty(issueType))
                    jql += String.Format("+AND+issueType={0}", Uri.EscapeUriString(issueType));
                if (!String.IsNullOrEmpty(jqlQuery))
                    jql += String.Format("+AND+{0}", Uri.EscapeUriString(jqlQuery));
                var path = String.Format("search?jql={0}&startAt={1}&maxResults={2}", jql, resultCount, queryCount);
                var request = CreateRequest(Method.GET, path);

                var response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.OK);

                var data = deserializer.Deserialize<IssueContainer<TIssueFields>>(response);
                var issues = data.issues ?? Enumerable.Empty<Issue<TIssueFields>>();

                foreach (var item in issues) yield return LoadIssue(item.key);
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
                var path = String.Format("search?jql={0}&startAt={1}&maxResults={2}&fields&expand", filter.jql, resultCount, queryCount);
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
                    Issue<TIssueFields> fields = LoadIssue(item.key);
                    temp.fields = fields.fields;
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

        /* \ \ 
           Summary
           Gets additional information for an <link TechTalk.JiraRestClient.IssueRef, IssueRef>
           of a retrieved JIRA issue.
           
           
           Parameters
           issueRef :  The IssueRef object to retrieve additional
                       information for
           
           Returns
           A new <link TechTalk.JiraRestClient.Issue, Issue> object with
           additional data.                                                                     */
        public Issue<TIssueFields> LoadIssue(IssueRef issueRef)
        {
            return LoadIssue(issueRef.JiraIdentifier);
        }

        /* \ \ 
           Summary
           Gets a more detailed view of the specified JIRA issue.
           
           
           Parameters
           issueRef :  The issue key to retrieve
           
           Returns
           A filled <link TechTalk.JiraRestClient.Issue, Issue> object. */
        public Issue<TIssueFields> LoadIssue(String issueRef)
        {
            try
            {
                var path = String.Format("issue/{0}", issueRef);
                var request = CreateRequest(Method.GET, path);

                var response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.OK);

                var issue = deserializer.Deserialize<Issue<TIssueFields>>(response);
                issue.fields.comments = GetComments(issue).ToList();
                issue.fields.watchers = GetWatchers(issue).ToList();
                Issue.ExpandLinks(issue);

                String[] respParts = response.Content.Split(',');
                foreach (String part in respParts)
                {
                    if (part.Contains("customfield_"))
                    {
                        String[] pieces = part.Split(':');
                        if (pieces[1] != "null")
                            issue.fields.customfield.Add(new KeyValuePair<String, String>(pieces[0], pieces[1]));
                    }
                }
                
                return issue;
            }
            catch (Exception ex)
            {
                Trace.TraceError("GetIssue(issueRef) error: {0}", ex);
                throw new JiraClientException("Could not load issue", ex);
            }
        }

        /* Summary
           Creates a new JIRA issue with only bare information.
           Parameters
           projectKey :  The key of the project within which to
                         create the issue
           issueType :   The issue type
           summary\ :    \Summary text
           
           Returns
           A new <link TechTalk.JiraRestClient.Issue, Issue> object from
           the newly created JIRA issue.                                 */
        public Issue<TIssueFields> CreateIssue(String projectKey, String issueType, String summary)
        {
            return CreateIssue(projectKey, issueType, new TIssueFields { summary = summary });
        }

        /* Summary
           Takes a collection of IssueFields to create an issue in JIRA.
           Parameters
           projectKey :   The project key to create the issue in
           issueType :    The type of issue to create
           issueFields :  The <link TechTalk.JiraRestClient.IssueFields, IssueFields>
                          collection with the issue data
           
           Returns
           \Returns an <link TechTalk.JiraRestClient.Issue, Issue>
           object representing the new issue.                                         */
        public Issue<TIssueFields> CreateIssue(String projectKey, String issueType, TIssueFields issueFields)
        {
            IRestResponse response = null;
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
                if (issueFields.labels != null && issueFields.labels.Count > 0)
                    issueData.Add("labels", issueFields.labels);
                if (issueFields.watchers.Count > 0)
                    issueData.Add("watchers", issueFields.watchers);

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

                response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.Created);

                var issueRef = deserializer.Deserialize<IssueRef>(response);
                return LoadIssue(issueRef);
            }
            catch (Exception ex)
            {
                Trace.TraceError("CreateIssue(projectKey, issueType, issueFields) error: {0}", ex);
                String resp = "";
                if (response != null)
                    resp = response.Content;
                throw new JiraClientException("Could not create issue:" + resp, ex);
            }
        }

        /* Summary
           Creates a JIRA issue using a filled Issue object.
           Parameters
           projectKey :  _nt_ The key of the project within which to
                         create the issue
           issueType :   _nt_ The issue type
           issue :       _nt_ An Issue object with its fields filled.
           Returns
           A new <link TechTalk.JiraRestClient.Issue, Issue> object
           retrieved from the newly created JIRA issue.               */
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
            var request = CreateRequest(Method.POST, "");
            String responseMsg = "";
            try
            {
                var path = String.Format("issue/{0}/transitions", issueKey);
                request = CreateRequest(Method.POST, path);
                request.AddHeader("ContentType", "application/json");
                var trans = new Dictionary<String, object>();
                trans.Add("id", actionID);
                trans.Add("name", action);
                var transTo = new Dictionary<String, String>();
                transTo.Add("name", "Fixed");
                trans.Add("to", transTo);
                request.AddBody(new { transition = trans } );

                var response = client.Execute(request);
                responseMsg = response.ErrorMessage;
                AssertStatus(response, HttpStatusCode.NoContent);

                return GetIssue(issueKey);
            }
            catch (Exception ex)
            {
                Trace.TraceError("ProgressWorkflowAction(issueKey, action, actionID) error: {0}", ex);
                if (responseMsg == null)
                    responseMsg = "";
                String parameters = Environment.NewLine + responseMsg + Environment.NewLine;
                foreach (Parameter parm in request.Parameters)
                    parameters += "{" + parm.Name + ":" + parm.Value + "}";
                throw new JiraClientException("Could not update issue : " + parameters, ex);
            }
        }

        public Issue ProgressWorkflowAction(String issueKey, String action, String actionID, String resolution)
        {
            var request = CreateRequest(Method.POST, "");
            String responseMsg = "";
            try
            {
                var path = String.Format("issue/{0}/transitions", issueKey);
                request = CreateRequest(Method.POST, path);
                request.AddHeader("ContentType", "application/json");
                var trans = new Dictionary<String, object>();
                trans.Add("id", actionID);
                trans.Add("name", action);
                var transTo = new Dictionary<String, String>();
                transTo.Add("name", resolution);
                trans.Add("to", transTo);
                request.AddBody(new { transition = trans });

                var response = client.Execute(request);
                responseMsg = response.ErrorMessage;
                AssertStatus(response, HttpStatusCode.NoContent);
            }
            catch (Exception ex)
            {
                Trace.TraceError("ProgressWorkflowAction(issueKey, action, actionID, resolution) error: {0}", ex);
                if (responseMsg == null)
                    responseMsg = "";
                String parameters = Environment.NewLine + responseMsg + Environment.NewLine;
                foreach(Parameter parm in request.Parameters)
                    parameters += "{" + parm.Name + ":" + parm.Value + "}";
                throw new JiraClientException("Could not update issue : " + parameters, ex);
            }
            try
            {
                var path = String.Format("issue/{0}", issueKey);
                request = CreateRequest(Method.PUT, path);
                request.AddHeader("ContentType", "application/json");

                var resData = new Dictionary<string, object>();

                var updateData = new Dictionary<string, object>();

                var commBody = new Dictionary<string, string>();
                commBody.Add("body", "Issue is fixed.");

                updateData.Add("comment", new[] { new { add = commBody } });

                resData.Add("update", updateData);

                request.AddBody(resData);

                var response = client.Execute(request);
                responseMsg = response.ErrorMessage;
                AssertStatus(response, HttpStatusCode.NoContent);
            }
            catch(Exception ex)
            {
                Trace.TraceError("ProgressWorkflowAction(issueKey, action, actionID, resolution) error: {0}", ex);
                if (responseMsg == null)
                    responseMsg = "";
                String parameters = Environment.NewLine + responseMsg + Environment.NewLine;
                foreach (Parameter parm in request.Parameters)
                    parameters += "{" + parm.Name + ":" + parm.Value + "}";
                throw new JiraClientException("Could not update issue : " + parameters, ex);
            }

            return GetIssue(issueKey);
        }

        /* Summary
           This method uses a source Issue object to update certain
           fields on the Issue in JIRA. These fields are "summary",
           "description", "labels", "timetracking", "reporter", and all
           custom fields.
           Parameters
           issue :  The issue to update, containing the updated data
           
           Returns
           The updated <link TechTalk.JiraRestClient.Issue, Issue>
           object.                                                      */
        public Issue<TIssueFields> UpdateIssue(Issue<TIssueFields> issue)
        {
            try
            {
                var path = String.Format("issue/{0}", issue.JiraIdentifier);
                var request = CreateRequest(Method.PUT, path);
                request.AddHeader("ContentType", "application/json");

                var updateData = new Dictionary<string, object>();
                if (issue.fields.summary != null)
                    updateData.Add("summary", new[] { new { set = issue.fields.summary } });
                if (issue.fields.description != null)
                    updateData.Add("description", new[] { new { set = issue.fields.description } });
                if (issue.fields.labels != null && issue.fields.labels.Count > 0)
                    updateData.Add("labels", new[] { new { set = issue.fields.labels } });
                if (issue.fields.timetracking != null && issue.fields.timetracking.originalEstimate != null)
                    updateData.Add("timetracking", new[] { new { set = new { originalEstimate = issue.fields.timetracking.originalEstimate } } });
                if (issue.fields.reporter != null)
                    updateData.Add("reporter", new[] { new { set = issue.fields.reporter } });

                var propertyList = typeof(TIssueFields).GetProperties().Where(p => p.Name.StartsWith("customfield_"));
                foreach (var property in propertyList)
                {
                    var value = property.GetValue(issue.fields, null);
                    if (value != null) updateData.Add(property.Name, new[] { new { set = value } });
                }

                request.AddBody(new { update = updateData });

                var response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.NoContent);

                return LoadIssue(issue);
            }
            catch (Exception ex)
            {
                Trace.TraceError("UpdateIssue(issue) error: {0}", ex);
                throw new JiraClientException("Could not update issue", ex);
            }
        }

        /* Summary
           Deletes the issue from JIRA using the provided <link TechTalk.JiraRestClient.IssueRef, IssueRef>
           object.
           Parameters
           issue :  The IssueRef representing the issue to delete
           
           Remarks
           This method will also delete all subtasks of the deleted
           issue.                                                                                           */
        public void DeleteIssue(IssueRef issue)
        {
            try
            {
                var path = String.Format("issue/{0}?deleteSubtasks=true", issue.id);
                var request = CreateRequest(Method.DELETE, path);

                var response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.NoContent);
            }
            catch (Exception ex)
            {
                Trace.TraceError("DeleteIssue(issue) error: {0}", ex);
                throw new JiraClientException("Could not delete issue", ex);
            }
        }


        /* Summary
           This gets a list of Transition objects for all of the issue's
           state transitions. It takes a target <link TechTalk.JiraRestClient.IssueRef, IssueRef>
           as a parameter.
           Parameters
           issue :  The target issue for which to retrieve transition
                    information
           
           Returns
           An IEnumerable\<<link TechTalk.JiraRestClient.Transition, Transition>\>
           of all transition objects for the issue.                                               */
        public IEnumerable<Transition> GetTransitions(IssueRef issue)
        {
            try
            {
                var path = String.Format("issue/{0}/transitions?expand=transitions.fields", issue.id);
                var request = CreateRequest(Method.GET, path);

                var response = ExecuteRequest(request);
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

        /* Summary
           This method transitions the state of an issue. It takes an <link TechTalk.JiraRestClient.IssueRef, IssueRef>
           and a <link TechTalk.JiraRestClient.Transition, Transition>
           as its parameters.
           
           
           Parameters
           issue :       An IssueRef for the issue to modify
           transition :  The Transition object for the target state
           
           Returns
           The updated <link TechTalk.JiraRestClient.Issue, Issue>
           object.                                                                                                      */
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

                var response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.NoContent);

                return LoadIssue(issue);
            }
            catch (Exception ex)
            {
                Trace.TraceError("TransitionIssue(issue, transition) error: {0}", ex);
                throw new JiraClientException("Could not transition issue state", ex);
            }
        }


        /* Summary
           Gets a list of <link TechTalk.JiraRestClient.JiraUser, JiraUser>s
           who are watching the specified <link TechTalk.JiraRestClient.IssueRef, IssueRef>.
           Parameters
           issue :  The IssueRef for which to retrieve watchers
           
           Returns
           An IEnumerable\<<link TechTalk.JiraRestClient.JiraUser, JiraUser>\>
           of watchers for the specified <link TechTalk.JiraRestClient.IssueRef, IssueRef>.  */
        public IEnumerable<JiraUser> GetWatchers(IssueRef issue)
        {
            try
            {
                var path = String.Format("issue/{0}/watchers", issue.id);
                var request = CreateRequest(Method.GET, path);

                var response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.OK);

                return deserializer.Deserialize<WatchersContainer>(response).watchers;
            }
            catch (Exception ex)
            {
                Trace.TraceError("GetWatchers(issue) error: {0}", ex);
                throw new JiraClientException("Could not load watchers", ex);
            }
        }


        /* Summary
           Get all comments for the provided <link TechTalk.JiraRestClient.IssueRef, IssueRef>.
           Parameters
           issue :  The IssueRef for which to retrieve comments
           
           Returns
           An IEnumerable\<<link TechTalk.JiraRestClient.Comment, Comment>\>
           with all comments for the requested issue.                                           */
        public IEnumerable<Comment> GetComments(IssueRef issue)
        {
            try
            {
                var path = String.Format("issue/{0}/comment", issue.id);
                var request = CreateRequest(Method.GET, path);

                var response = ExecuteRequest(request);
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

        /* Summary
           Adds a comment to the specified issue.
           Parameters
           issue :    The IssueRef for the issue to add the comment to
           comment :  A string containing the text of the comment to add
           
           Returns
           A newly created <link TechTalk.JiraRestClient.Comment, Comment>
           object.                                                         */
        public Comment CreateComment(IssueRef issue, String comment)
        {
            try
            {
                var path = String.Format("issue/{0}/comment", issue.id);
                var request = CreateRequest(Method.POST, path);
                request.AddHeader("ContentType", "application/json");
                request.AddBody(new Comment { body = comment });

                var response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.Created);

                return deserializer.Deserialize<Comment>(response);
            }
            catch (Exception ex)
            {
                Trace.TraceError("CreateComment(issue, comment) error: {0}", ex);
                throw new JiraClientException("Could not create comment", ex);
            }
        }

        /* Summary
           Deletes a <link TechTalk.JiraRestClient.Comment, Comment>
           from the provided <link TechTalk.JiraRestClient.IssueRef, IssueRef>.
           Parameters
           issue :    An IssueRef for the target issue in JIRA
           comment :  The Comment object to remove                              */
        public void DeleteComment(IssueRef issue, Comment comment)
        {
            try
            {
                var path = String.Format("issue/{0}/comment/{1}", issue.id, comment.id);
                var request = CreateRequest(Method.DELETE, path);

                var response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.NoContent);
            }
            catch (Exception ex)
            {
                Trace.TraceError("DeleteComment(issue, comment) error: {0}", ex);
                throw new JiraClientException("Could not delete comment", ex);
            }
        }


        /* Summary
           Gets a list of Attachment objects for the specified <link TechTalk.JiraRestClient.IssueRef, IssueRef>.
           Parameters
           issue :  The IssueRef for which to retrieve Attachments
           
           Returns
           An IEnumerable\<<link TechTalk.JiraRestClient.Attachment, Attachment>\>
           for the requested <link TechTalk.JiraRestClient.IssueRef, IssueRef>.                                   */
        public IEnumerable<Attachment> GetAttachments(IssueRef issue)
        {
            return LoadIssue(issue).fields.attachment;
        }

        /* Summary
           Create an <link TechTalk.JiraRestClient.Attachment, Attachment>
           for a specified <link TechTalk.JiraRestClient.IssueRef, IssueRef>.
           Parameters
           issue :       The IssueRef to attach the new Attachment to
           fileStream :  A FileStream object for the attachment file
           fileName :    The name of the file without path information
           
           Returns
           A new <link TechTalk.JiraRestClient.Attachment, Attachment>
           object.
           Remarks
           The provided file name must be a plain file name with
           extension. If the full path to the source file is passed in
           the view attachment link will not work in the issue view
           through the JIRA site service.                                     */
        public Attachment CreateAttachment(IssueRef issue, Stream fileStream, String fileName)
        {
            try
            {
                var path = String.Format("issue/{0}/attachments", issue.id);
                var request = CreateRequest(Method.POST, path);
                request.AddHeader("X-Atlassian-Token", "nocheck");
                request.AddHeader("ContentType", "multipart/form-data");
                request.AddFile("file", stream => fileStream.CopyTo(stream), fileName);

                var response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.OK);

                return deserializer.Deserialize<List<Attachment>>(response).Single();
            }
            catch (Exception ex)
            {
                Trace.TraceError("CreateAttachment(issue, fileStream, fileName) error: {0}", ex);
                throw new JiraClientException("Could not create attachment", ex);
            }
        }

        /* Summary
           Removes the requested <link TechTalk.JiraRestClient.Attachment, Attachment>
           from JIRA.
           Parameters
           attachment :  The Attachment object to remove                               */
        public void DeleteAttachment(Attachment attachment)
        {
            try
            {
                var path = String.Format("attachment/{0}", attachment.id);
                var request = CreateRequest(Method.DELETE, path);

                var response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.NoContent);
            }
            catch (Exception ex)
            {
                Trace.TraceError("DeleteAttachment(attachment) error: {0}", ex);
                throw new JiraClientException("Could not delete attachment", ex);
            }
        }


        /* Summary
           Gets a list of IssueLink objects associated with a JIRA
           issue.
           
           
           Parameters
           issue :  An IssueRef object of the issue to retrieve links for
           
           Returns
           An enumerable list of <link TechTalk.JiraRestClient.IssueLink, IssueLink>
           objects.                                                                  */
        public IEnumerable<IssueLink> GetIssueLinks(IssueRef issue)
        {
            return LoadIssue(issue).fields.issuelinks;
        }

        /* Summary
           This method returns an existing issue link from a parent <link TechTalk.JiraRestClient.IssueRef, IssueRef>
           to a child <link TechTalk.JiraRestClient.IssueRef, IssueRef>
           with the specified relationship.
           Parameters
           parent :        An IssueRef for a parent issue
           child :         An IssueRef for a child issue
           relationship :  A string containing the name of the
                           relationship between the two IssueRefs
           
           Returns
           An <link TechTalk.JiraRestClient.IssueLink, IssueLink>
           containing the data for the specified issue relationship.                                                  */
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

        /* Summary
           Create an IssueLink between two IssueRef objects with the
           specified relationship.
           Parameters
           parent :        The parent IssueRef
           child :         The child IssueRef
           relationship :  The desired relationship
           
           Returns
           A new <link TechTalk.JiraRestClient.IssueLink, IssueLink>
           object.                                                   */
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

                var response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.Created);

                return LoadIssueLink(parent, child, relationship);
            }
            catch (Exception ex)
            {
                Trace.TraceError("CreateIssueLink(parent, child, relationship) error: {0}", ex);
                throw new JiraClientException("Could not link issues", ex);
            }
        }

        /* Summary
           Deletes the specified <link TechTalk.JiraRestClient.IssueLink, IssueLink>.
           Parameters
           link :  The IssueLink to delete                                            */
        public void DeleteIssueLink(IssueLink link)
        {
            try
            {
                var path = String.Format("issueLink/{0}", link.id);
                var request = CreateRequest(Method.DELETE, path);

                var response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.NoContent);
            }
            catch (Exception ex)
            {
                Trace.TraceError("DeleteIssueLink(link) error: {0}", ex);
                throw new JiraClientException("Could not delete issue link", ex);
            }
        }


        /* Summary
           Gets a list of all <link TechTalk.JiraRestClient.RemoteLink, RemoteLink>
           objects for the specified <link TechTalk.JiraRestClient.IssueRef, IssueRef>.
           Parameters
           issue :  The IssueRef for which to retrieve links
           
           Returns
           An IEnumerable\<<link TechTalk.JiraRestClient.RemoteLink, RemoteLink>\>
           for the specified <link TechTalk.JiraRestClient.IssueRef, IssueRef>.         */
        public IEnumerable<RemoteLink> GetRemoteLinks(IssueRef issue)
        {
            try
            {
                var path = string.Format("issue/{0}/remotelink", issue.id);
                var request = CreateRequest(Method.GET, path);
                request.AddHeader("ContentType", "application/json");

                var response = ExecuteRequest(request);
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

        /* Summary
           Creates a new <link TechTalk.JiraRestClient.RemoteLink, RemoteLink>
           in the specified <link TechTalk.JiraRestClient.IssueLink, IssueRef>.
           Parameters
           issue :       The IssueRef to add the link to
           remoteLink :  The RemoteLink object to add
           
           Returns
           The newly added <link TechTalk.JiraRestClient.RemoteLink, RemoteLink>
           object.                                                               */
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

                var response = ExecuteRequest(request);
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

        /* Summary
           Updates the contents of a remote link. Takes an <link TechTalk.JiraRestClient.IssueRef, IssueRef>
           and a <link TechTalk.JiraRestClient.RemoteLink, RemoteLink>
           as parameters.
           Parameters
           issue :       An IssueRef for the issue to be modified
           remoteLink :  A RemoteLink object with the new values to be
                         submitted
           
           Returns
           An updated <link TechTalk.JiraRestClient.RemoteLink, RemoteLink>
           object with the updated values.                                                                   */
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

                var response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.NoContent);

                return GetRemoteLinks(issue).Single(rl => rl.id == remoteLink.id);
            }
            catch (Exception ex)
            {
                Trace.TraceError("UpdateRemoteLink(issue, remoteLink) error: {0}", ex);
                throw new JiraClientException("Could not update external link for issue", ex);
            }
        }

        /* Summary
           Deletes the specified <link TechTalk.JiraRestClient.RemoteLink, RemoteLink>
           from the provided <link TechTalk.JiraRestClient.IssueRef, IssueRef>.
           Parameters
           issue :       The IssueRef representing the issue to remove the
                         link from
           remoteLink :  The RemoteLink object to remove                               */
        public void DeleteRemoteLink(IssueRef issue, RemoteLink remoteLink)
        {
            try
            {
                var path = string.Format("issue/{0}/remotelink/{1}", issue.id, remoteLink.id);
                var request = CreateRequest(Method.DELETE, path);
                request.AddHeader("ContentType", "application/json");

                var response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.NoContent);
            }
            catch (Exception ex)
            {
                Trace.TraceError("DeleteRemoteLink(issue, remoteLink) error: {0}", ex);
                throw new JiraClientException("Could not delete external link for issue", ex);
            }
        }

        /* Summary
           Gets a list of all issue types from JIRA.
           
           
           Returns
           A List\<<link TechTalk.JiraRestClient.IssueType, IssueType>\>
           of all issue types in JIRA.                                   */
        public IEnumerable<IssueType> GetIssueTypes()
        {
            try
            {
                var request = CreateRequest(Method.GET, "issuetype");
                request.AddHeader("ContentType", "application/json");

                var response = ExecuteRequest(request);
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

        /* Summary
           Gets an IssueType object matching the requested issue type.
           
           
           Parameters
           typeID :  The type id to retrieve
           
           Returns
           An <link TechTalk.JiraRestClient.IssueType, IssueType> object
           for the requested issue type.                                 */
        public IssueType GetIssueType(String typeID)
        {
            try
            {
                String command = String.Format("issuetype/{0}", typeID);
                var request = CreateRequest(Method.GET, command);
                request.AddHeader("ContentType", "application/json");

                var response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.OK);

                var data = deserializer.Deserialize<IssueType>(response);
                return data;

            }
            catch (Exception ex)
            {
                Trace.TraceError("GetIssueTypes() error: {0}", ex);
                throw new JiraClientException("Could not load issue types", ex);
            }
        }

        /* Summary
           Retrieves server information.
           Returns
           A <link TechTalk.JiraRestClient.ServerInfo, ServerInfo>
           object with some basic identification data.             */
        public ServerInfo GetServerInfo()
        {
            try
            {
                var request = CreateRequest(Method.GET, "serverInfo");
                request.AddHeader("ContentType", "application/json");

                var response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.OK);

                return deserializer.Deserialize<ServerInfo>(response);
            }
            catch (Exception ex)
            {
                Trace.TraceError("GetServerInfo() error: {0}", ex);
                throw new JiraClientException("Could not retrieve server information", ex);
            }
        }

        /* Summary
           Gets user data for the requested user name.
           Parameters
           username :  The JIRA username for which to retrieve data
           
           Returns
           A <link TechTalk.JiraRestClient.JiraUser, JiraUser> object
           with the requested user's data.                            */
        public JiraUser GetUser(String username)
        {
            try
            {
                var path = String.Format("/user?username={0}", username);
                var request = CreateRequest(Method.GET, path);
                request.AddHeader("ContentType", "application/json");

                var response = ExecuteRequest(request);
                AssertStatus(response, HttpStatusCode.OK);

                return deserializer.Deserialize<JiraUser>(response);
            }
            catch (Exception ex)
            {
                Trace.TraceError("GetUser() error: {0}", ex);
                throw new JiraClientException("Could not retrieve user information ", ex);
            }
        }

        /* Summary
           Adds a JIRA user as a watcher on the specified issue.
           Parameters
           issueKey :      The issue key to watch
           jiraUsername :  The JIRA user name to add as a watcher */
        public void AddWatcher(String issueKey, String jiraUsername)
        {
            try
            {
                String url = baseApiUrl + "issue/" + issueKey + "/watchers";
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Accept = "application/json";
                httpWebRequest.Method = "POST";
                httpWebRequest.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(String.Format("{0}:{1}", username, password))));

                String target = String.Format("\"{0}\"", jiraUsername);
                byte[] data = System.Text.Encoding.ASCII.GetBytes(target.ToCharArray());

                Stream stream = httpWebRequest.GetRequestStream();
                stream.Write(data, 0, data.Length);
                stream.Close();

                httpWebRequest.GetResponse();
            }
            catch (Exception ex)
            {
                Trace.TraceError("AddWatcher() error: {0}", ex);
                throw new JiraClientException("Could not add watcher ", ex);
            }
        }
    }
}
