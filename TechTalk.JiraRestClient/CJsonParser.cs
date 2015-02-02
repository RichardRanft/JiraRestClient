using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace TechTalk.JiraRestClient
{
    public class CParser
    {
        private Stack<ParserState> m_state;
        private ParserState m_currentState;

        public CParser()
        {
            m_currentState = new ParserState();

            m_state = new Stack<ParserState>();
            m_state.Push(m_currentState);
        }

        public ParserState Parse(String objText)
        {
            char[] jsonChars = objText.ToCharArray();
            String objkey = "";
            String objfield = "";
            Object objvalue = null;
            int index = 0;
            foreach (char ch in jsonChars)
            {
                index++;
                if(ch == ':')
                {
                    // enter field data
                    m_currentState.FieldPos = FieldState.DATA;
                    if(index >= 0 && index < jsonChars.Length)
                    {
                        char tmpCh = jsonChars[index];
                        m_currentState.Type = ObjectType.RAW;
                        if (tmpCh == '"')
                            m_currentState.Type = ObjectType.STRING;
                        if (tmpCh == '{')
                            m_currentState.Type = ObjectType.OBJECT;
                        if (tmpCh == '[')
                            m_currentState.Type = ObjectType.LIST;
                    }
                    //enterData(objkey);
                    objfield = "";
                    objvalue = null;
                    continue;
                }
                if (ch == '{')
                {
                    // enter object
                    enterObject(objkey, objfield);
                    objkey = "";
                    objfield = "";
                    objvalue = null;
                    continue;
                }
                if (ch == '}')
                {
                    // exit object
                    leaveObject(objkey, objfield);
                    continue;
                }
                if (ch == '[')
                {
                    // enter list
                    enterList(objkey, objfield);
                    objkey = "";
                    objfield = "";
                    objvalue = null;
                    continue;
                }
                if (ch == ']')
                {
                    // exit list
                    leaveList(objkey, objfield);
                    continue;
                }
                if (ch == ',')
                {
                    // exit current field
                    if(m_currentState.TextMode)
                    {
                        switch (m_currentState.FieldPos)
                        {
                            case FieldState.NAME:
                                objkey += ch;
                                break;
                            case FieldState.DATA:
                                objfield += ch;
                                break;
                        }
                        continue;
                    }
                    leaveField(objkey, objfield);
                    objkey = "";
                    objfield = "";
                    objvalue = null;
                    continue;
                }
                if(ch == '"')
                {
                    m_currentState.TextMode = !m_currentState.TextMode;
                }

                // collect characters
                switch (m_currentState.FieldPos)
                {
                    case FieldState.NAME:
                        objkey += ch;
                        break;
                    case FieldState.DATA:
                        objfield += ch;
                        break;
                }
            }
            return m_currentState;
        }

        private void enterField(String key)
        {
            List<Object> valueList = new List<Object>();
            KeyValuePair<String, List<Object>> entry = new KeyValuePair<string,List<object>>(key, valueList);
            m_currentState.Fields.Add(entry);
        }

        private void leaveField(String key, Object value)
        {
            KeyValuePair<String, List<Object>> entry = m_currentState.FindEntry(key);
            if (entry.Value == null)
            {
                List<Object> valueList = new List<Object>();
                valueList.Add(value);
                entry = new KeyValuePair<String, List<Object>>(key, valueList);
                m_currentState.Fields.Add(entry);
            }
            else
            {
                entry.Value.Add(value);
            }
            ParserState exitState = m_currentState;
            if (m_state.Count > 0)
            {
                m_currentState = m_state.Pop();
                foreach (KeyValuePair<String, List<Object>> listEntry in exitState.Fields)
                {
                    KeyValuePair<String, List<Object>> temp = m_currentState.FindEntry(listEntry.Key);
                    if(temp.Key == null || temp.Value == null)
                        m_currentState.Fields.Add(listEntry);
                    else
                    {
                        foreach(Object val in listEntry.Value)
                        {
                            temp.Value.Add(val);
                        }
                    }
                }
            }
        }

        private void enterObject(String key, Object value)
        {
            m_currentState.State = ParseState.INOBJECT;
            m_state.Push(m_currentState);
            m_currentState = new ParserState(ParseState.INOBJECT, FieldState.NAME, m_currentState.TextMode, m_currentState.Type, key, value);
        }

        private void leaveObject(String key, Object value)
        {
            KeyValuePair<String, List<Object>> entry = m_currentState.FindEntry(key);
            if (entry.Value == null)
            {
                List<Object> valueList = new List<Object>();
                valueList.Add(value);
                entry = new KeyValuePair<String, List<Object>>(key, valueList);
                m_currentState.Fields.Add(entry);
            }
            else
            {
                entry.Value.Add(value);
            }
            ParserState exitState = m_currentState;
            if (m_state.Count > 0)
            {
                m_currentState = m_state.Pop();
                foreach (KeyValuePair<String, List<Object>> listEntry in exitState.Fields)
                {
                    KeyValuePair<String, List<Object>> temp = m_currentState.FindEntry(listEntry.Key);
                    if (temp.Key == null || temp.Value == null)
                        m_currentState.Fields.Add(listEntry);
                    else
                    {
                        foreach (Object val in listEntry.Value)
                        {
                            temp.Value.Add(val);
                        }
                    }
                }
            }
        }

        private void enterList(String key, Object value)
        {
            m_currentState.State = ParseState.INLIST;
            if (key != "")
            {
                List<Object> valueList = new List<Object>();
                valueList.Add(value);
                KeyValuePair<String, List<Object>> entry = new KeyValuePair<String, List<Object>>(key, valueList);
                m_currentState.Fields.Add(entry);
            }
            m_state.Push(m_currentState);
            m_currentState = new ParserState(ParseState.INLIST, FieldState.NAME, m_currentState.TextMode, m_currentState.Type, key, value);
        }

        private void leaveList(String key, Object value)
        {
            KeyValuePair<String, List<Object>> entry = m_currentState.FindEntry(key);
            if (entry.Value == null)
            {
                List<Object> valueList = new List<Object>();
                valueList.Add(value);
                entry = new KeyValuePair<String, List<Object>>(key, valueList);
                m_currentState.Fields.Add(entry);
            }
            else
            {
                entry.Value.Add(value);
            }
            ParserState exitState = m_currentState;
            if (m_state.Count > 0)
            {
                m_currentState = m_state.Pop();
                foreach (KeyValuePair<String, List<Object>> listEntry in exitState.Fields)
                {
                    KeyValuePair<String, List<Object>> temp = m_currentState.FindEntry(listEntry.Key);
                    if (temp.Key == null || temp.Value == null)
                        m_currentState.Fields.Add(listEntry);
                    else
                    {
                        foreach (Object val in listEntry.Value)
                        {
                            temp.Value.Add(val);
                        }
                    }
                }
            }
        }
    }

    public class ParserState
    {
        public ParseState State { get; set; }
        public FieldState FieldPos { get; set; }
        public bool TextMode { get; set; }
        public ObjectType Type { get; set; }
        public List<KeyValuePair<String, List<Object>>> Fields { get; set; }

        public ParserState()
        {
            State = ParseState.ROOT;
            FieldPos = FieldState.NAME;
            TextMode = false;
            Type = ObjectType.STRING;
            Fields = new List<KeyValuePair<String, List<Object>>>();
        }

        public ParserState(ParseState state, FieldState fpos, bool fmode, ObjectType type, String key, Object value)
        {
            State = state;
            FieldPos = fpos;
            TextMode = fmode;
            Type = type;
            Fields = new List<KeyValuePair<String, List<Object>>>();
            if(key != "" && value != null)
            {
                List<Object> valueList = new List<Object>();
                valueList.Add(value);
                KeyValuePair<String, List<Object>> entry = new KeyValuePair<string, List<object>>(key, valueList);
            }
        }

        public KeyValuePair<String, List<Object>> FindEntry(String key)
        {
            KeyValuePair<String, List<Object>> temp = new KeyValuePair<String, List<Object>>();
            foreach(KeyValuePair<String, List<Object>> entry in Fields)
            {
                if (entry.Key.Equals(key))
                    temp = entry;
            }
            return temp;
        }
    }

    public enum ParseState
    {
        ROOT,
        INFIELD,
        INOBJECT,
        INLIST
    }

    public enum FieldState
    {
        NAME,
        DATA
    }

    public enum ObjectType
    {
        RAW,
        STRING,
        OBJECT,
        LIST
    }
}
