﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using GoogleTranslateFreeApi;
using Newtonsoft.Json.Linq;

namespace NicoNicoTranslator
{
    class Program
    {
        static void Main(string[] args)
        {
            NicoNicoServer NNC = new NicoNicoServer(80, 20);
            NNC.RunServer();
        }
    }
    class NicoNicoServer
    {
        GoogleTranslator googleTranslator;
        Language from;
        Language to;
        private int portNum;
        private int listenNum;
        private int translator;
        CookieContainer cc = new CookieContainer();
        public enum Translators
        {
            Google = 1,
            None,
            User
        }
        public NicoNicoServer(int PortNum, int ListenNum, int Translator = (int)Translators.User)
        {
            translator = Translator;
            portNum = PortNum;
            listenNum = ListenNum;
            while (true)
            {
                if (translator == (int)Translators.User)
                {
                    WriteConsole("사용할 번역 서비스를 선택해주세요.", ConsoleColor.Cyan);
                    WriteConsole("1. Google 번역");
                    WriteConsole("2. 사용하지 않음");
                    translator = int.Parse(Console.ReadLine());
                }
                if (translator == (int)Translators.Google || translator == (int)Translators.None)
                {
                    break;
                }
            }
            if (translator == (int)Translators.Google)
            {
                googleTranslator = new GoogleTranslator();
                from = GoogleTranslator.GetLanguageByName("Japanese");
                to = GoogleTranslator.GetLanguageByName("Korean");

            }

        }
        public void RunServer()
        {
            WriteConsole("서버가 127.0.0.1:80에서 작동되고 있습니다...", ConsoleColor.Cyan);
            IPEndPoint ipep = new IPEndPoint(IPAddress.Any, portNum);
            Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            server.Bind(ipep);
            server.Listen(listenNum);
            while (true)
            {
                Socket client = server.Accept();
                WriteConsole("접속 :" + ((IPEndPoint)client.RemoteEndPoint).Address + ":" + ((IPEndPoint)client.RemoteEndPoint).Port, ConsoleColor.Green);
                Thread t = new Thread(() =>
                {

                    RunServerThreadAsync(client);
                }
                );
                t.Start();
            }

        }
        public async void RunServerThreadAsync(Socket client)
        {
            try
            {
                String originalResponse = Recieve(client); //헤더 가져오기
                if (originalResponse != "")
                {
                    foreach (String headers in GetHeaders(originalResponse))
                    {
                        WriteConsole(headers, ConsoleColor.DarkGray);
                    }
                    if (originalResponse.Contains("api.json/")) // 일반적인 경우
                    {
                        if (translator == (int)Translators.None)
                        {
                            SendOriginalServer(client, "api.json/", originalResponse);
                        }
                        else
                        {
                            var http = (HttpWebRequest)WebRequest.Create(new Uri("http://202.248.252.234/api.json/"));
                            foreach (string header in GetHeaders(originalResponse))
                            {
                                if (header.Contains("Host"))
                                {
                                    http.Host = GetHeaderValue(header);
                                }
                                else if (header.Contains("User-Agent"))
                                {
                                    http.UserAgent = GetHeaderValue(header);
                                }
                                else if (header.Contains("Content-Type"))
                                {
                                    http.UserAgent = GetHeaderValue(header);
                                }
                                else if (header.Contains("Accept"))
                                {
                                    http.Accept = GetHeaderValue(header);
                                }
                                else if (header.Contains("Referer"))
                                {
                                    http.Referer = GetHeaderValue(header);
                                }
                                else
                                {
                                    http.Headers.Add(header);
                                }

                            }
                            http.Method = "POST";
                            string parsedContent = GetPayloads(originalResponse);
                            if (parsedContent != "")
                            {

                                UTF8Encoding encoding = new UTF8Encoding();
                                Byte[] bytes = encoding.GetBytes(parsedContent);

                                Stream newStream = http.GetRequestStream();
                                newStream.Write(bytes, 0, bytes.Length);
                                newStream.Close();

                                var response = http.GetResponse();
                                string responseOriginal = Encoding.UTF8.GetString(response.Headers.ToByteArray());
                                var stream = response.GetResponseStream();
                                var sr = new StreamReader(stream);
                                string content = sr.ReadToEnd();
                                response.Close();
                                JArray jArray = JArray.Parse(content);
                                List<JToken> jTokens = new List<JToken>();
                                List<string> splited = new List<string>();
                                List<string> translated = new List<string>();
                                var jTokensList = new List<List<JToken>>();
                                int c = 0;
                                int d = 0;
                                int cb = 0;
                                foreach (var item in jArray)
                                {
                                    if (item["chat"] != null && item["chat"]["content"] != null)
                                    {
                                        jTokens.Add(item["chat"]["content"]);
                                    }
                                }
                                while (true)
                                {
                                    d = 0;
                                    cb = c;
                                    for (int i = 0; i < 5000 && c < jTokens.Count; c++, d++)
                                    {
                                        i += (jTokens[c] + Environment.NewLine).Length;
                                    }
                                    if (c == jTokens.Count)
                                    {
                                        jTokensList.Add(jTokens.GetRange(cb, d));
                                        break;
                                    }
                                    else
                                    {
                                        jTokensList.Add(jTokens.GetRange(cb, d - 1));
                                        c--;
                                    }


                                }
                                foreach (List<JToken> list in jTokensList)
                                {
                                    string s = "";
                                    foreach (JToken item in list)
                                    {
                                        s += (item + Environment.NewLine);
                                    }
                                    splited.Add(s);
                                }
                                for (int i = 0; i < splited.Count; i++)
                                {
                                    if(i!=0) Thread.Sleep(5000); 
                                    TranslationResult result = await googleTranslator.TranslateAsync(splited[i], from, to);
                                    string[] lines = result.MergedTranslation.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
                                    for (int j = 0; j < lines.Length; j++)
                                    {
                                        translated.Add(lines[j]);
                                    }
                                    Console.WriteLine("{0} / {1} 완료", translated.Count, jTokens.Count);
                                }
                                c = 0;//재활용
                                foreach (JToken item in jArray)
                                {
                                    if (item["chat"] != null && item["chat"]["content"] != null)
                                    {
                                        item["chat"]["content"] = translated[c++];
                                    }
                                }
                                client.Send(GetSendByte(client, jArray.ToString(), responseOriginal)); //클라이언트에 HTML 등 전송
                            }
                        }






                    }
                    else
                    {
                        SendOriginalServer(client, originalResponse.Split('/')[1].Split(' ')[0], originalResponse);
                    }
                }

                WriteConsole("접속해제 :" + ((IPEndPoint)client.RemoteEndPoint).Address + ":" + ((IPEndPoint)client.RemoteEndPoint).Port, ConsoleColor.Green);
                client.Close();
            }
            catch (Exception ex)
            {
                WriteConsole("클라이언트와의 연결에서 오류가 발생했습니다. 자세한 오류는 아래를 참고해주시기 바랍니다.", ConsoleColor.Red);
                WriteConsole(ex.ToString(), ConsoleColor.Red);
                client.Close();
            }
        }
        public void SendOriginalServer(Socket client, string others, string originalResponse)
        {
            var http = (HttpWebRequest)WebRequest.Create(new Uri("http://202.248.252.234/" + others));
            foreach (string header in GetHeaders(originalResponse))
            {
                if (header.Contains("Host"))
                {
                    http.Host = GetHeaderValue(header);
                }
                else if (header.Contains("User-Agent"))
                {
                    http.UserAgent = GetHeaderValue(header);
                }
                else if (header.Contains("Content-Type"))
                {
                    http.UserAgent = GetHeaderValue(header);
                }
                else if (header.Contains("Accept"))
                {
                    http.Accept = GetHeaderValue(header);
                }
                else if (header.Contains("Referer"))
                {
                    http.Referer = GetHeaderValue(header);
                }
                else
                {
                    http.Headers.Add(header);
                }

            }
            http.Method = originalResponse.Contains("POST") ? "POST" : "GET";
            string parsedContent = GetPayloads(originalResponse);
            if (parsedContent != "")
            {

                UTF8Encoding encoding = new UTF8Encoding();
                Byte[] bytes = encoding.GetBytes(parsedContent);

                Stream newStream = http.GetRequestStream();
                newStream.Write(bytes, 0, bytes.Length);
                newStream.Close();
            }
            var response = http.GetResponse();
            string responseOriginal = Encoding.UTF8.GetString(response.Headers.ToByteArray());
            var stream = response.GetResponseStream();
            var sr = new StreamReader(stream);
            string content = sr.ReadToEnd();
            response.Close();
            client.Send(GetSendByte(client, content, responseOriginal)); //클라이언트에 HTML 등 전송
        }
        public static bool isHangulHanjaJapaness(String str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                char ch = str[i];
                UnicodeCategory unicodeBlock = char.GetUnicodeCategory(ch);
                if (UnicodeCategory.OtherLetter == unicodeBlock)
                {
                    return true;
                }
            }
            return false;
        }
        public string GetHeaderValue(String header)
        {
            string[] host = header.Split(new string[] { ": " }, StringSplitOptions.None);
            return host[1];
        }
        public string GetPayloads(String Response)
        {

            String[] buf = Response.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            int end = 0;
            for (int i = 0; i < buf.Length; i++)
            {
                if (buf[i] == "")
                {
                    end = i;
                }
            }
            if (buf.Length == end + 2) return buf[end + 1];
            return "";
        }
        public List<string> GetHeaders(String Response)
        {
            String[] buf = Response.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            List<string> headers = new List<string>();
            foreach (string header in buf)
            {
                if (header.Contains(":") && !header.Contains("Connection") && !header.Contains("Content-Length"))
                {
                    headers.Add(header);

                }
                if (header == "") break;
            }
            return headers;
        }
        public String Recieve(Socket client)
        {
            byte[] _data = new byte[4096];
            client.Receive(_data);
            return Encoding.Default.GetString(_data).Trim('\0');
        }


        public byte[] GetSendByte(Socket client, String Content, string originalResponse)
        {
            byte[] _data2 = Encoding.UTF8.GetBytes(Content);
            try
            {
                GetHeaders(originalResponse);
                String _buf = "HTTP/1.1 200 OK\r\n";
                foreach (string header in GetHeaders(originalResponse))
                {
                    _buf += header + "\r\n";
                }
                _buf += "ServiceBy: 009342@naver.com\r\n";
                _buf += "\r\n";
                client.Send(Encoding.UTF8.GetBytes(_buf));
            }
            catch
            {
                String _buf = "HTTP/1.1 100 BedRequest ok\r\n";
                _buf += "ServiceBy: 009342@naver.com\r\n";
                _buf += "content-type:text/html\r\n";
                _buf += "\r\n";
                client.Send(Encoding.UTF8.GetBytes(_buf));
                _data2 = Encoding.UTF8.GetBytes("Bed Request");
            }
            return _data2;
        }
        public void WriteConsole(string str, ConsoleColor Fore = ConsoleColor.Gray, ConsoleColor Back = ConsoleColor.Black)
        {
            Console.ForegroundColor = Fore;
            Console.BackgroundColor = Back;
            Console.WriteLine(str);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.BackgroundColor = ConsoleColor.Black;
        }
    }

}
