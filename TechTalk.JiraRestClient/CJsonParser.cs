﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Runtime.Serialization.Json;
using System.Xml;

namespace TechTalk.JiraRestClient
{
    public class CParser
    {
        private Stack<ParserState> m_state;
        private ParserState m_currentState;
        //private String m_objkey = "";
        //private String m_objfield = "";
        //private Object m_objvalue = null;

        public CParser()
        {
            m_currentState = new ParserState();
            m_state = new Stack<ParserState>();
            m_state.Push(m_currentState);
        }

        public String StripChangelog(String objText)
        {
            int openBraceCount = 0;
            for(int i = 0; i < objText.Length; i++)
            {
                if (objText[i] == '{')
                    openBraceCount++;
                if(openBraceCount > 1)
                {
                    int strlen = objText.Length - i - 1;
                    return objText.Substring(i, strlen);
                }
            }
            return objText;
        }

        public ParserState Parse(String objText)
        {
            byte[] jsonChars = Encoding.ASCII.GetBytes(objText);
            XmlDictionaryReaderQuotas quotas = XmlDictionaryReaderQuotas.Max;
            XmlDictionaryReader xmlReader = JsonReaderWriterFactory.CreateJsonReader(jsonChars, quotas);
            //xmlReader.
            return m_currentState;
        }

        private void enterField(String key)
        {
            List<Object> valueList = new List<Object>();
            KeyValuePair<String, List<Object>> entry = new KeyValuePair<string,List<object>>(key, valueList);
            m_currentState.FieldPos = FieldState.NAME;
            m_currentState.Fields.Add(entry);
        }

        private void leaveField(String key, Object value)
        {
            KeyValuePair<String, List<Object>> entry = m_currentState.FindEntry(key);
            if (entry.Value == null)
            {
                List<Object> valueList = new List<Object>();
                if(value != null && (String)value != "")
                    valueList.Add(value);
                entry = new KeyValuePair<String, List<Object>>(key, valueList);
                m_currentState.Fields.Add(entry);
            }
            else
            {
                if (value != null && (String)value != "") 
                    entry.Value.Add(value);
            }
        }

        private void enterObject(String key, Object value)
        {
            KeyValuePair<String, List<Object>> entry = m_currentState.FindEntry(key);
            if (entry.Value == null)
            {
                List<Object> valueList = new List<Object>();
                if (value != null && (String)value != "")
                    valueList.Add(value);
                entry = new KeyValuePair<String, List<Object>>(key, valueList);
                m_currentState.Fields.Add(entry);
            }
            else
            {
                if (value != null && (String)value != "")
                    entry.Value.Add(value);
            }
            m_state.Push(m_currentState);
            m_currentState = new ParserState(ParseState.INOBJECT, FieldState.NAME, m_currentState.TextMode, m_currentState.Type, key, value);
        }

        private void leaveObject(String key, Object value)
        {
            KeyValuePair<String, List<Object>> entry = m_currentState.FindEntry(key);
            if (entry.Value == null)
            {
                List<Object> valueList = new List<Object>();
                if (value != null && (String)value != "")
                    valueList.Add(value);
                entry = new KeyValuePair<String, List<Object>>(key, valueList);
                m_currentState.Fields.Add(entry);
            }
            else
            {
                if (value != null && (String)value != "")
                    entry.Value.Add(value);
            }
            ParserState exitState = m_currentState;
            m_currentState = m_state.Pop();
            m_currentState.States.Add(exitState);
        }

        private void enterList(String key, Object value)
        {
            m_currentState.State = ParseState.INLIST;
            List<Object> valueList = new List<Object>();
            KeyValuePair<String, List<Object>> entry = new KeyValuePair<string, List<object>>(key, valueList);
            m_currentState.Fields.Add(entry);
        }

        private void leaveList(String key, Object value)
        {
            KeyValuePair<String, List<Object>> entry = m_currentState.FindEntry(key);
            if (entry.Value == null)
            {
                List<Object> valueList = new List<Object>();
                if (value != null && (String)value != "") 
                    valueList.Add(value);
                entry = new KeyValuePair<String, List<Object>>(key, valueList);
                m_currentState.Fields.Add(entry);
            }
            else
            {
                if (value != null && (String)value != "") 
                    entry.Value.Add(value);
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
        public List<ParserState> States { get; set; }

        public ParserState()
        {
            State = ParseState.ROOT;
            FieldPos = FieldState.NAME;
            TextMode = false;
            Type = ObjectType.STRING;
            Fields = new List<KeyValuePair<String, List<Object>>>();
            States = new List<ParserState>();
        }

        public ParserState(ParseState state, FieldState fpos, bool fmode, ObjectType type, String key, Object value)
        {
            State = state;
            FieldPos = fpos;
            TextMode = fmode;
            Type = type;
            Fields = new List<KeyValuePair<String, List<Object>>>();
            States = new List<ParserState>();
            if (key != "" && value != null)
            {
                List<Object> valueList = new List<Object>();
                if (value != null && (String)value != "") 
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
