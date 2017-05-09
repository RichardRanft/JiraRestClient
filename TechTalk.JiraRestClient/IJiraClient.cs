using System;
using System.Collections.Generic;
using System.IO;

namespace TechTalk.JiraRestClient
{
    /* Summary
       The IJiraClient interface handles all communication with the
       consuming application.                                       */
    public interface IJiraClient<TIssueFields> where TIssueFields : IssueFields, new()
    {
        /// <summary>Returns all projects that the user can see</summary>
        ProjectList GetProjects();

        /// <summary>Returns all issues for the given project</summary>
        IEnumerable<Issue<TIssueFields>> GetIssues(String projectKey);
        /// <summary>Returns all issues of the specified type for the given project</summary>
        IEnumerable<Issue<TIssueFields>> GetIssues(String projectKey, String issueType);
        /// <summary>Returns a specific issue</summary>
        Issue GetIssue(String issueKey);
        /* Summary
           GetIssueChangelog() gets the data that is found in the
           Transitions tab of an issue within JIRA.
           
           
           Parameters
           issueKey :  A string containing the issue key of the desired
                       issue.
           
           Returns
           \Returns a <link TechTalk.JiraRestClient.ChangeLog, ChangeLog>
           object containing the history data for the requested issue.    */
        ChangeLog GetIssueChangelog(String issueKey);
        /* Summary
           This method gets the EditMeta object for the issue requested.
           The fields in the editmeta correspond to the fields in the
           edit screen for the issue. Fields not in the screen will not
           be in the editemeta.
           Parameters
           issueKey :  The issue key for which we want to retrieve edit
                       screen data.
           
           Returns
           An <link TechTalk.JiraRestClient.EditMeta, EditMeta> object
           with the data for the issue's edit screen.                    */
        EditMeta GetEditMeta(String issueKey);
        /// <summary>Returns the worklog count for a specific issue</summary>
        int GetWorklogCount(String issueKey);
        /// <summary>Adds worklogs to a specific issue</summary>
        Worklog CreateWorklog(String issueKey, Worklog worklog);
        /// <summary>Returns the worklogs for a specific issue</summary>
        Worklog GetWorklog(String issueKey, int startAt = 0, int queryCount = 20);
        /// <summary>Progresses the issue's workflow to the specified state.</summary>
        Issue ProgressWorkflowAction(String issueKey, String action, String actionID);
        /// <summary>Progresses the issue's workflow to the specified state.</summary>
        Issue ProgressWorkflowAction(String issueKey, String action, String actionID, String resolution);
        /// <summary>Returns all issues of the given type and the given project filtered by the given JQL query</summary>
        IEnumerable<Issue<TIssueFields>> GetIssuesByQuery(String projectKey, String issueType, String jqlQuery);
        /// <summary>Enumerates through all issues for the given project</summary>
        IEnumerable<Issue<TIssueFields>> EnumerateIssues(String projectKey);
        /// <summary>Enumerates through all issues of the specified type for the given project</summary>
        IEnumerable<Issue<TIssueFields>> EnumerateIssues(String projectKey, String issueType);
        /// <summary>Enumerates through all issues of the specified type for the given project</summary>
        IEnumerable<Issue> GetIssuesQuery(Filter filter);

        /// <summary>Returns the issue identified by the given ref</summary>
        Issue<TIssueFields> LoadIssue(String issueRef);
        /// <summary>Returns the issue identified by the given ref</summary>
        Issue<TIssueFields> LoadIssue(IssueRef issueRef);
        /// <summary>Creates an issue of the specified type for the given project</summary>
        Issue<TIssueFields> CreateIssue(String projectKey, String issueType, String summary);
        /// <summary>Creates an issue of the specified type for the given project</summary>
        Issue<TIssueFields> CreateIssue(String projectKey, String issueType, TIssueFields issueFields);
        /// <summary>Creates an issue of the specified type for the given project</summary>
        Issue<TIssueFields> CreateIssue(String projectKey, String issueType, Issue issue);
        /// <summary>Updates the given issue on the remote system</summary>
        Issue<TIssueFields> UpdateIssue(Issue<TIssueFields> issue);
        /// <summary>Deletes the given issue from the remote system</summary>
        void DeleteIssue(IssueRef issue);

        /// <summary>Returns all transitions avilable to the given issue</summary>
        IEnumerable<Transition> GetTransitions(IssueRef issue);
        /// <summary>Changes the state of the given issue as described by the transition</summary>
        Issue<TIssueFields> TransitionIssue(IssueRef issue, Transition transition);

        /// <summary>Returns all watchers for the given issue</summary>
        IEnumerable<JiraUser> GetWatchers(IssueRef issue);

        /// <summary>Returns all comments for the given issue</summary>
        IEnumerable<Comment> GetComments(IssueRef issue);
        /// <summary>Adds a comment to the given issue</summary>
        Comment CreateComment(IssueRef issue, String comment);
        /// <summary>Deletes the given comment</summary>
        void DeleteComment(IssueRef issue, Comment comment);

        /// <summary>Return all attachments for the given issue</summary>
        IEnumerable<Attachment> GetAttachments(IssueRef issue);
        /// <summary>Creates an attachment to the given issue</summary>
        Attachment CreateAttachment(IssueRef issue, Stream stream, String fileName);
        /// <summary>Deletes the given attachment</summary>
        void DeleteAttachment(Attachment attachment);

        /// <summary>Returns all links for the given issue</summary>
        IEnumerable<IssueLink> GetIssueLinks(IssueRef issue);
        /// <summary>Returns the link between two issues of the given relation</summary>
        IssueLink LoadIssueLink(IssueRef parent, IssueRef child, String relationship);
        /// <summary>Creates a link between two issues with the given relation</summary>
        IssueLink CreateIssueLink(IssueRef parent, IssueRef child, String relationship);
        /// <summary>Removes the given link of two issues</summary>
        void DeleteIssueLink(IssueLink link);

        /// <summary>Returns all remote links (attached urls) for the given issue</summary>
        IEnumerable<RemoteLink> GetRemoteLinks(IssueRef issue);
        /// <summary>Creates a remote link (attached url) for the given issue</summary>
        RemoteLink CreateRemoteLink(IssueRef issue, RemoteLink remoteLink);
        /// <summary>Updates the given remote link (attached url) of the specified issue</summary>
        RemoteLink UpdateRemoteLink(IssueRef issue, RemoteLink remoteLink);
        /// <summary>Removes the given remote link (attached url) of the specified issue</summary>
        void DeleteRemoteLink(IssueRef issue, RemoteLink remoteLink);

        /// <summary>Returns all issue types</summary>
        IEnumerable<IssueType> GetIssueTypes();

        /// <summary>Returns the issue type of the requested issue</summary>
        IssueType GetIssueType(String typeID);

        /// <summary>Returns information about the JIRA server</summary>
        ServerInfo GetServerInfo();

        /// <summary>Returns information about the JIRA user</summary>
        JiraUser GetUser(String username);

        /// <summary>
        /// Adds a watcher to an issue
        /// </summary>
        /// <param name="issueKey"></param>
        /// <param name="username"></param>
        void AddWatcher(String issueKey, String username);
    }
}
