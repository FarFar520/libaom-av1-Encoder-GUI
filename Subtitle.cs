using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace 破片压缩器 {
    internal class Subtitle {
        public static Encoding getEncoding(FileInfo fi) {
            byte[] fileBytes = new byte[3]; // 创建一个缓冲区
            try {
                using (FileStream fileStream = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read)) {
                    fileStream.Read(fileBytes, 0, fileBytes.Length);//读取文件的一部分数据
                }
            } catch { }
            Encoding encoding = Encoding.Default;
            //UTF - 8
            if (fileBytes.Length == 3) {
                if ((fileBytes[0] == 34 && fileBytes[1] == 232 && fileBytes[2] == 167) ||
                    (fileBytes[0] == 239 && fileBytes[1] == 187 && fileBytes[2] == 191) ||
                    (fileBytes[0] == 229 && fileBytes[1] == 186 && fileBytes[2] == 143)
                    ) {
                    encoding = Encoding.UTF8;
                } else if (fileBytes[0] == 34 && fileBytes[1] == 202 && fileBytes[2] == 211) {
                    encoding = Encoding.GetEncoding("GB2312");
                } else if ((fileBytes[0] == 255 && fileBytes[1] == 254 && fileBytes[2] == 34) ||
                        (fileBytes[0] == 254 && fileBytes[1] == 255 && fileBytes[2] == 0)) {
                    encoding = Encoding.UTF32;
                }
            }
            return encoding;
        }

        public static Regex regex日时分秒 = new Regex(@"(?<Day>\d+[\.:])?(?<Hour>\d{1,2})[:：](?<Min>\d{1,2})[:：](?<Sec>\d{1,2})(?:[\., ](?<MS>\d+))?", RegexOptions.Compiled);

        public static double match日时分秒_to_秒(string text) {
            Match match = regex日时分秒.Match(text);
            if (match.Success) {
                double.TryParse(match.Groups["Sec"].Value, out double Sec);
                if (int.TryParse(match.Groups["Day"].Value, out int day)) Sec += day + 86400;
                if (int.TryParse(match.Groups["Hour"].Value, out int hour)) Sec += hour * 3600;
                if (int.TryParse(match.Groups["Min"].Value, out int min)) Sec += min * 60;
                if (float.TryParse("0." + match.Groups["MS"].Value, out float ms)) Sec += ms;//ASS毫秒单位保留两位数字，整除100

                return Sec;
            }
            return 0;
        }

        public class SRT {

            static Regex regexLines = new Regex(@"\d+\s+(?<Hour1>\d{2,})[:：](?<Min1>\d{2})[:：](?<Sec1>\d{2})(?:[\.,](?<MS1>\d{1,3}))\s*-->\s*(?<Hour2>\d{2,})[:：](?<Min2>\d{2})[:：](?<Sec2>\d{2})(?:[\.,](?<MS2>\d{1,3}))\s+(?<txt>.+?)", RegexOptions.RightToLeft | RegexOptions.Compiled);

            Dictionary<double, List<Line>> _dic_sec时间戳_内容 = new Dictionary<double, List<Line>>( );

            double[] sec排序时间戳;

            public class Line {
                public double sec开始显示, sec结束显示;
                public string txt;
                public Line(Match match, string txt) {
                    this.txt = txt.Trim( );
                    if (!string.IsNullOrEmpty(this.txt)) {
                        double.TryParse(match.Groups["Sec1"].Value, out sec开始显示);
                        if (int.TryParse(match.Groups["Hour1"].Value, out int hour1)) sec开始显示 += hour1 * 3600;
                        if (int.TryParse(match.Groups["Min1"].Value, out int Min1)) sec开始显示 += Min1 * 60;
                        if (float.TryParse("0." + match.Groups["MS1"].Value, out float sec_ms1)) sec开始显示 += sec_ms1;

                        double.TryParse(match.Groups["Sec2"].Value, out sec结束显示);
                        if (int.TryParse(match.Groups["Hour2"].Value, out int hour2)) sec结束显示 += hour2 * 3600;
                        if (int.TryParse(match.Groups["Min2"].Value, out int Min2)) sec结束显示 += Min2 * 60;
                        if (float.TryParse("0." + match.Groups["MS2"].Value, out float sec_ms2)) sec结束显示 += sec_ms2;
                    }
                }
                public void append切片秒时间戳(ref StringBuilder sb, int index, double sec切片开始, double sec切片结束) {
                    double sec切片开始显示 = sec开始显示 - sec切片开始;
                    double sec切片结束显示 = sec结束显示 - sec切片开始;

                    if (sec切片开始显示 < 0) sec切片开始显示 = 0;
                    if (sec切片结束显示 > sec切片结束) sec切片结束显示 = sec切片结束;

                    TimeSpan span切片开始显示 = TimeSpan.FromSeconds(sec切片开始显示);
                    TimeSpan span切片结束显示 = TimeSpan.FromSeconds(sec切片结束显示);

                    sb.AppendFormat("{0}\r\n{1:D2}:{2:D2}:{3:D2},{4:D3} --> {5:D2}:{6:D2}:{7:D2},{8:D3}\r\n{9}\r\n\r\n"
                        , index
                        , span切片开始显示.Days * 24 + span切片开始显示.Hours, span切片开始显示.Minutes, span切片开始显示.Seconds, span切片开始显示.Milliseconds
                        , span切片开始显示.Days * 24 + span切片结束显示.Hours, span切片结束显示.Minutes, span切片结束显示.Seconds, span切片结束显示.Milliseconds
                        , txt
                        );
                }

            }


            public SRT(FileInfo file) {
                if (file.Length < 104857600) {
                    string str;
                    try { str = File.ReadAllText(file.FullName, getEncoding(file)); } catch { return; }
                    MatchCollection matchs = regexLines.Matches(str);
                    for (int i = 0; i < matchs.Count; i++) {
                        Add(new Line(matchs[i], matchs[i].Groups["txt"].Value));
                    }
                }

                if (_dic_sec时间戳_内容.Count > 0) {
                    sec排序时间戳 = _dic_sec时间戳_内容.Keys.OrderBy(k => k).ToArray( );
                } else {
                    sec排序时间戳 = new double[0];
                }
            }
            void Add(Line line) {
                if (!string.IsNullOrEmpty(line.txt)) {
                    if (_dic_sec时间戳_内容.TryGetValue(line.sec开始显示, out List<Line> list相同开始)) {
                        bool b插入 = false, b合并 = false;
                        for (int i = 0; i < list相同开始.Count; i++) {
                            if (line.sec结束显示 < list相同开始[i].sec结束显示) {
                                list相同开始.Insert(i, line);
                                b插入 = true;
                                break;
                            } else if (line.sec结束显示 == list相同开始[i].sec结束显示) {
                                if (list相同开始[i].txt != line.txt)
                                    list相同开始[i].txt = (list相同开始[i].txt + "\r\n" + line.txt).Trim( );
                                b合并 = true;
                                break;
                            }
                        }
                        if (!b插入 && !b合并) list相同开始.Add(line);
                    } else {
                        _dic_sec时间戳_内容.Add(line.sec开始显示, new List<Line> { line });
                    }

                    if (_dic_sec时间戳_内容.TryGetValue(line.sec结束显示, out List<Line> list相同结束)) {
                        bool b插入 = false, b合并 = false;
                        for (int i = 0; i < list相同结束.Count; i++) {
                            if (line.sec开始显示 < list相同结束[i].sec开始显示) {
                                list相同结束.Insert(i, line);
                                b插入 = true; break;
                            } else if (line.sec开始显示 == list相同结束[i].sec开始显示) {
                                b合并 = true; break;//相同重复无需合并，相同开始已经合并。
                            }
                        }

                        if (!b插入 && !b合并) list相同结束.Add(line);
                    } else {
                        _dic_sec时间戳_内容.Add(line.sec结束显示, new List<Line> { line });
                    }
                }
            }

            int index = 0;
            public void fx顺序分割并保存(DirectoryInfo di, double sec开始时间戳, double sec结束时间戳, string name) {
                int i开始 = -1, i结束 = -1;
                if (sec排序时间戳.Length > 0) {
                    if (index >= sec排序时间戳.Length) index = sec排序时间戳.Length - 1;
                    while (index > 0 && sec排序时间戳[index] > sec开始时间戳) index--;

                    for (int i = index; i < sec排序时间戳.Length; i++) {
                        if (sec排序时间戳[i] >= sec开始时间戳) {
                            i开始 = i; break;
                        }
                    }
                    for (int i = index; i < sec排序时间戳.Length; i++) {
                        if (sec排序时间戳[i] > sec结束时间戳) {
                            i结束 = i; break;
                        }
                    }
                    if (i结束 < 0) i结束 = sec排序时间戳.Length;
                }
                if (i开始 >= 0 && i结束 > 0 && i结束 > i开始) {
                    HashSet<Line> lines = new HashSet<Line>( );
                    StringBuilder sb = new StringBuilder( );
                    for (int i = i开始; i < i结束; i++) {
                        if (_dic_sec时间戳_内容.TryGetValue(sec排序时间戳[i], out List<Line> list)) {
                            for (int s = 0; s < list.Count; s++) {
                                if (lines.Add(list[s])) {
                                    list[s].append切片秒时间戳(ref sb, lines.Count, sec开始时间戳, sec结束时间戳);
                                }
                            }
                        }
                    }
                    index = i结束;

                    if (lines.Count > 0) {
                        string file = di.FullName + "\\" + name + ".srt";
                        try {
                            File.WriteAllText(file, sb.ToString( ));
                        } catch { }
                    }
                }
            }
        }

        public class ASS {
            string head = string.Empty;

            static Regex regexEvents = new Regex(@"Format:(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            int iStart = 0, iEnd = 0;

            Dictionary<double, List<Line>> _dic_sec时间戳_内容 = new Dictionary<double, List<Line>>( );

            double[] sec排序时间戳;
            public string Extension;
            public ASS(FileInfo file) {
                if (file.Length < 104857600) {
                    Extension = file.Extension;
                    string[] arr;
                    try { arr = File.ReadAllLines(file.FullName, getEncoding(file)); } catch { return; }
                    int i = 0;
                    StringBuilder sbHead = new StringBuilder( );
                    bool bEvents = false;
                    for (; i < arr.Length; i++) {
                        sbHead.AppendLine(arr[i]);
                        if (string.Equals(arr[i], "[Events]", StringComparison.OrdinalIgnoreCase)) {
                            bEvents = true; break;
                        }
                    }

                    if (bEvents) {
                        for (++i; i < arr.Length; i++) {
                            sbHead.AppendLine(arr[i]);
                            Match match = regexEvents.Match(arr[i]);
                            if (match.Success) {
                                string[] formats = match.Groups[1].Value.Split(',');
                                for (int f = 0; f < formats.Length; f++) {
                                    string format = formats[f].Trim( );
                                    if (string.Equals(format, "Start", StringComparison.OrdinalIgnoreCase)) {
                                        iStart = f;
                                    } else if (string.Equals(format, "End", StringComparison.OrdinalIgnoreCase)) {
                                        iEnd = f;
                                    }
                                }
                                break;
                            }
                        }

                        head = sbHead.AppendLine( ).ToString( );

                        for (++i; i < arr.Length; i++) {
                            if (arr[i].StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase)) {
                                Add(new Line(arr[i], iStart, iEnd));
                            }
                        }

                        if (_dic_sec时间戳_内容.Count > 0)
                            sec排序时间戳 = _dic_sec时间戳_内容.Keys.OrderBy(k => k).ToArray( );
                        else
                            sec排序时间戳 = new double[0];
                    }
                }

            }

            public class Line {
                List<string> list = new List<string>( );
                public double sec开始显示, sec结束显示;
                public Line(string str, int iStart, int iEnd) {
                    string[] arr = str.Substring(9).Split(',');
                    for (int i = 0; i < arr.Length; i++) {
                        list.Add(arr[i].Trim( ));
                    }
                    if (iStart < list.Count && iEnd < list.Count) {
                        sec开始显示 = match日时分秒_to_秒(arr[iStart]);
                        sec结束显示 = match日时分秒_to_秒(arr[iEnd]);
                    }
                }
                public void append切片秒时间戳(ref StringBuilder sb, int iStart, int iEnd, double sec切片开始, double sec切片结束) {
                    double ms切片开始显示 = sec开始显示 - sec切片开始;
                    double ms切片结束显示 = sec结束显示 - sec切片开始;

                    if (ms切片开始显示 < 0) ms切片开始显示 = 0;
                    if (ms切片结束显示 > sec切片结束) ms切片结束显示 = sec切片结束;

                    TimeSpan span切片开始显示 = TimeSpan.FromSeconds(ms切片开始显示);
                    TimeSpan span切片结束显示 = TimeSpan.FromSeconds(ms切片结束显示);

                    string[] arr = list.ToArray( );
                    arr[iStart] = string.Format("{0:D1}:{1:D1}:{2:D1}.{3:D2}"
                        , span切片开始显示.Days * 24 + span切片开始显示.Hours
                        , span切片开始显示.Minutes
                        , span切片开始显示.Seconds
                        , span切片开始显示.Milliseconds / 10
                        );
                    arr[iEnd] = string.Format("{0:D1}:{1:D1}:{2:D1}.{3:D2}"
                        , span切片结束显示.Days * 24 + span切片开始显示.Hours
                        , span切片结束显示.Minutes
                        , span切片结束显示.Seconds
                        , span切片开始显示.Milliseconds / 10
                        );

                    sb.AppendLine( );
                    sb.Append("Dialogue: ");
                    sb.Append(arr[0]);
                    for (int i = 1; i < arr.Length; i++) {
                        sb.Append(',').Append(arr[i]);
                    }
                }
            }

            void Add(Line line) {
                if (line.sec结束显示 > 0) {
                    if (_dic_sec时间戳_内容.TryGetValue(line.sec开始显示, out List<Line> list相同开始)) {
                        bool b插入 = false;
                        for (int i = 0; i < list相同开始.Count; i++) {
                            if (line.sec结束显示 < list相同开始[i].sec结束显示) {
                                list相同开始.Insert(i, line);
                                b插入 = true;
                                break;
                            }
                        }
                        if (!b插入) list相同开始.Add(line);
                    } else {
                        _dic_sec时间戳_内容.Add(line.sec开始显示, new List<Line> { line });
                    }

                    if (_dic_sec时间戳_内容.TryGetValue(line.sec结束显示, out List<Line> list相同结束)) {
                        bool b插入 = false;
                        for (int i = 0; i < list相同结束.Count; i++) {
                            if (line.sec开始显示 < list相同结束[i].sec开始显示) {
                                list相同结束.Insert(i, line);
                                b插入 = true; break;
                            }
                        }
                        if (!b插入) list相同结束.Add(line);
                    } else {
                        _dic_sec时间戳_内容.Add(line.sec结束显示, new List<Line> { line });
                    }
                }
            }


            int index = 0;
            public void fx顺序分割并保存(DirectoryInfo di, double sec开始时间戳, double sec结束时间戳, string name) {
                int i开始 = -1, i结束 = -1;
                if (sec排序时间戳.Length > 0) {
                    if (index >= sec排序时间戳.Length) index = sec排序时间戳.Length - 1;
                    while (index > 0 && index < sec排序时间戳.Length && sec排序时间戳[index] > sec开始时间戳) index--;

                    for (int i = index; i < sec排序时间戳.Length; i++) {
                        if (sec排序时间戳[i] >= sec开始时间戳) {
                            i开始 = i; break;
                        }
                    }
                    for (int i = index; i < sec排序时间戳.Length; i++) {
                        if (sec排序时间戳[i] > sec结束时间戳) {
                            i结束 = i; break;
                        }
                    }
                    if (i结束 < 0) i结束 = sec排序时间戳.Length;
                }

                if (i开始 >= 0 && i结束 > 0 && i结束 >= i开始) {
                    HashSet<Line> lines = new HashSet<Line>( );
                    StringBuilder sb = new StringBuilder(head);
                    for (int i = i开始; i < i结束; i++) {
                        if (_dic_sec时间戳_内容.TryGetValue(sec排序时间戳[i], out List<Line> list)) {
                            for (int s = 0; s < list.Count; s++) {
                                if (lines.Add(list[s])) {
                                    list[s].append切片秒时间戳(ref sb, iStart, iEnd, sec开始时间戳, sec结束时间戳);
                                }
                            }
                        }
                    }
                    index = i结束;

                    if (lines.Count > 0) {
                        string file = di.FullName + "\\" + name + Extension;
                        try {
                            File.WriteAllText(file, sb.ToString( ));
                        } catch { }
                    }
                }
            }
        }
    }
}
