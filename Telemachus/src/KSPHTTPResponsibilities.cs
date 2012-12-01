﻿using System;
using System.Collections.Generic;
using System.Text;
using MinimalHTTPServer;
using System.Reflection;
using System.Security.Cryptography;

namespace Telemachus
{
    class TelemachusResponsibility : IHTTPRequestResponsibility
    {
        Dictionary<string, string> hashCheck =
            new Dictionary<string, string>();

        public TelemachusResponsibility()
        {
            String[] hashes = new String[] {"1C-6A-92-61-27-1B-C5-4F-CF-3D-54-D7-A9-12-DF-06-1B-12-3C-E6",
                                           "17-B4-75-CA-5A-E4-5B-BE-E1-52-C7-F9-DD-91-41-BF-64-E1-72-20",
                                           "D0-67-05-A0-A9-5B-09-FC-AF-30-E6-D2-1A-AA-38-08-35-69-4D-4D"};

            foreach (String hash in hashes)
            {
                hashCheck.Add(hash, "");
            }
        }

        String PAGE_PREFIX = "/telemachus";
        public bool process(AsynchronousServer.ClientConnection cc, HTTPRequest request)
        {
            if (request.path.StartsWith(PAGE_PREFIX))
            {
                try
                {
                    String fileName = request.path.Substring(PAGE_PREFIX.Length - 1);
                    KSP.IO.TextReader tr = null;
                    if (fileName.Length > 0)
                    {
                         tr = KSP.IO.TextReader.CreateForType<TelemachusDataLink>
                            (fileName);
                    }

                    String fileContents = tr.ReadToEnd();

                    if(!checkHash(fileContents))
                    {
                        return false;
                    }
                    
                    cc.Send(new OKPage(fileContents).ToString());
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private bool checkHash(String contents)
        {
            string hash = "";
            using (var cryptoProvider = new SHA1CryptoServiceProvider())
            {
                hash = BitConverter
                        .ToString(cryptoProvider.ComputeHash(GetBytes(contents)));
            }

            if (hashCheck.ContainsKey(hash))
            {
                return true;
            }

            return false;
        }

        public static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }
    }

    class DataLinkResponsibility : IHTTPRequestResponsibility
    {
        DataLinks dataLinks = null;
        Dictionary<string, ReflectiveArgumentData> abstractArgument =
            new Dictionary<string, ReflectiveArgumentData>();

        public DataLinkResponsibility(DataLinks dataLinks)
        {
            this.dataLinks = dataLinks;
        }

        public bool process(AsynchronousServer.ClientConnection cc, HTTPRequest request)
        {
            if (request.path.StartsWith("/telemachus/datalink"))
            {
                String args = request.path.Remove(0, request.path.IndexOf('?') + 1);

                String[] argsSplit = args.Split('&');

                cc.Send(new OKPage(argumentsParse(args)).ToString());
             
                return true;
            }

            return false;
        }

        public void clear()
        {
            abstractArgument.Clear();
        }

        private String argumentsParse(String args)
        {
            StringBuilder sb = new StringBuilder();
            String[] argsSplit = args.Split('&');

            foreach (String arg in argsSplit)
            {
                sb.Append(argumentParse(arg));
            }

            return sb.ToString();
        }

        private String argumentParse(String args)
        {
            String[] argsSplit = args.Split('=');
            ReflectiveArgumentData ad = null;
            abstractArgument.TryGetValue(argsSplit[1], out ad);

            if (ad == null)
            {
                ad = new ReflectiveArgumentData();
                ad.key = argsSplit[1];
                abstractArgument.Add(argsSplit[1], ad);
            }

            ad.variableName = argsSplit[0];
            ad.updateValue(dataLinks);

            return ad.ToString();
        }

        public class ReflectiveArgumentData
        {
            public String variableName { get; set; }
            public String key { get; set; }
            private  Object value = null;
            private Object parentValue = null;

            FieldInfo field = null;

            public void updateValue(DataLinks dl)
            {
                if (field == null)
                {
                    reflectiveUpdate(dl);
                }
                else
                {
                    value = field.GetValue(parentValue);
                }
            }

            private void reflectiveUpdate(DataLinks dl)
            {
                String[] argsSplit = key.Split('.');
                Type type = dl.GetType();
                value = dl;

                foreach (String s in argsSplit)
                {
                    field = type.GetField(s);
                    parentValue = value;
                    value = field.GetValue(parentValue);

                    type = value.GetType();
                }
            }

            public override String ToString()
            {
                StringBuilder sb = new StringBuilder();

                sb.Append(variableName);
                sb.Append(" = ");
                sb.Append(value.ToString());
                sb.Append(";");

                return sb.ToString();
            }
        }
    }

    public class DataLinks
    {
        public Vessel vessel;
        public Orbit orbit;
    }

    class ElseResponsibility : IHTTPRequestResponsibility
    {
        public bool process(AsynchronousServer.ClientConnection cc, HTTPRequest request)
        {
            cc.Send(new IOLessDataLinkNotFound().ToString());
            return true;
        }
    }

    class InformationResponsibility : IHTTPRequestResponsibility
    {
        public bool process(AsynchronousServer.ClientConnection cc, HTTPRequest request)
        {
            if (request.path.StartsWith("/telemachus/information"))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("<h1>Telemachus Information Page</h1>");
                sb.Append("Version: " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());
                sb.Append("</br>");
                sb.Append("<h1>API</h1>");
                Type type = typeof(Vessel); 
                FieldInfo[] fields = type.GetFields(); 
                foreach (var field in fields) 
                {
                    sb.Append("Vessel: " + field.Name + "</br>");
                }

                type = typeof(Orbit); 
                fields = type.GetFields();
                foreach (var field in fields)
                {
                    sb.Append("Orbit: " + field.Name + "</br>");
                }

                sb.Append("</br>");
                sb.Append("<h1>Hash</h1>");

                String[] files = new String[] { "altitude.html", "gforce.html", "velocity.html" };

                foreach(String fileName in files)
                {
                    KSP.IO.TextReader tr = null;

                        tr = KSP.IO.TextReader.CreateForType<TelemachusDataLink>
                           (fileName);

                        using (var cryptoProvider = new SHA1CryptoServiceProvider())
                        {
                            string hash = BitConverter
                                    .ToString(cryptoProvider.ComputeHash(
                                    TelemachusResponsibility.GetBytes(tr.ReadToEnd())));

                            sb.Append(fileName + ": " + hash + "</br>");
                        }

                }

                cc.Send(new OKPage(sb.ToString()).ToString());

                return true;
            }


            return false;
        }
    }
}
