using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TechTalk.JiraRestClient
{
    public class EditMeta
    {
        public EditMetaFieldCollection fields { get; set; }
        public List<EditMetaCustomFields> customfield { get; set; }

        public EditMeta()
        {
            customfield = new List<EditMetaCustomFields>();
        }

        public void GetCustomFields(String jsonText)
        {
            CParser parser = new CParser();
            ParserState parseData = parser.Parse(jsonText);
        }
    }
    
    public class EditMetaFieldCollection
    {
        public EditMetaFields summary { get; set; }
        public EditMetaIssueTypeField issuetype { get; set; }
        public EditMetaFields labels { get; set; }
        public EditMetaFields assignee { get; set; }
        public EditMetaFields fixVersions { get; set; }
        public EditMetaFields attachment { get; set; }
        public EditMetaFields reporter { get; set; }
        public EditMetaFields versions { get; set; }
        public EditMetaFields description { get; set; }
        public EditMetaPriorityTypeField priority { get; set; }
        public EditMetaFields issuelinks { get; set; }
        public EditMetaFields comment { get; set; }
    }

    public class EditMetaFields
    {
        public String required { get; set; }
        public EditMetaSchema schema { get; set; }
        public String name { get; set; }
        public String autoCompleteURL { get; set; }
        public String hasDefaultValue { get; set; }
        public List<String> operations { get; set; }
        public List<String> allowedValues { get; set; }

        public EditMetaFields()
        {
            schema = new EditMetaSchema();

            operations = new List<String>();
            allowedValues = new List<String>();
        }
    }

    public class EditMetaIssueTypeField
    {
        public String required { get; set; }
        public EditMetaSchema schema { get; set; }
        public String name { get; set; }
        public String autoCompleteURL { get; set; }
        public String hasDefaultValue { get; set; }
        public List<String> operations { get; set; }
        public List<IssueTypeValues> allowedValues { get; set; }

        public EditMetaIssueTypeField()
        {
            schema = new EditMetaSchema();

            operations = new List<String>();
            allowedValues = new List<IssueTypeValues>();
        }
    }

    public class IssueTypeValues
    {
        public String self { get; set; }
        public String id { get; set; }
        public String name { get; set; }
        public String description { get; set; }
        public String iconUrl { get; set; }
        public String subtask { get; set; }
    }

    public class EditMetaPriorityTypeField
    {
        public String required { get; set; }
        public EditMetaSchema schema { get; set; }
        public String name { get; set; }
        public String autoCompleteURL { get; set; }
        public String hasDefaultValue { get; set; }
        public List<String> operations { get; set; }
        public List<PriorityTypeValues> allowedValues { get; set; }

        public EditMetaPriorityTypeField()
        {
            schema = new EditMetaSchema();

            operations = new List<String>();
            allowedValues = new List<PriorityTypeValues>();
        }
    }

    public class PriorityTypeValues
    {
        public String self { get; set; }
        public String id { get; set; }
        public String name { get; set; }
        public String iconUrl { get; set; }
    }

    public class EditMetaCustomFields
    {
        public String required { get; set; }
        public EditMetaCustomSchema schema { get; set; }
        public String name { get; set; }
        public List<String> operations { get; set; }

        public EditMetaCustomFields()
        {
            schema = new EditMetaCustomSchema();

            operations = new List<String>();
        }
    }

    public class EditMetaSchema
    {
        public String type { get; set; }
        public String items { get; set; }
        public String system { get; set; }
        public String custom { get; set; }
        public String customId { get; set; }
    }

    public class EditMetaCustomSchema
    {
        public String type { get; set; }
        public String custom { get; set; }
        public String customId { get; set; }
    }

    public class PriorityValue
    {
        public String self { get; set; }
        public String iconUrl { get; set; }
        public String name { get; set; }
        public String id { get; set; }
    }
}
