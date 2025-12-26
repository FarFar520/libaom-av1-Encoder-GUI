using System.Text.RegularExpressions;

namespace 破片压缩器 {
    internal class Regex参数 {
        public static Regex regexNum = new Regex(@"\d+(\.\d+)?", RegexOptions.Compiled);

        public static Regex regex逗号 = new Regex(@"\s*[,，]+\s*");
        public static Regex rege多空格 = new Regex(" {2,}", RegexOptions.Compiled);
        public static Regex regex切片号 = new Regex(@"'(\d+)\.mkv'", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static Regex regexPTS_Time = new Regex(@"pts_time:(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static Regex regex秒长 = new Regex(@"\[FORMAT\]\s+duration=(\d+\.\d+)\s+\[/FORMAT\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        public static Regex regexFrame = new Regex(@"frame=\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static Regex regexSize = new Regex(@"size=\s*(\d+(?:\.\d+)?)KiB", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static Regex regexTime = new Regex(@"time=\s*(\d+[\d:\.]+\d+)\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static Regex regexBitrate = new Regex(@"bitrate=\s*(\d+(?:\.\d+)?)kbits/s", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static Regex regex日时分秒 = new Regex(@"(?<Day>\d+[\.:])?(?<Hour>\d{1,2})[:：](?<Min>\d{1,2})[:：](?<Sec>\d{1,2})(?:[\., ](?<MS>\d+))?", RegexOptions.Compiled);

        public static Regex regexWH = new Regex(@"(?<w>[1-9]\d+)x(?<h>[1-9]\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static Regex regexFPS = new Regex(@"(?<fps>\d+(\.\d+)?) fps", RegexOptions.IgnoreCase | RegexOptions.Compiled);//总帧数÷总时长（平均帧率）
        public static Regex regexTBR = new Regex(@"(?<tbr>\d+(\.\d+)?) tbr", RegexOptions.IgnoreCase | RegexOptions.Compiled);//基准帧率
        public static Regex regexDAR = new Regex(@"DAR\s*(?<darW>\d+):(?<darH>\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);//备用
        public static Regex regexAudio = new Regex(@"Audio: (?<code>\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        public static Regex regex隔行扫描 = new Regex(@"(top|bottom)\s+first", RegexOptions.IgnoreCase | RegexOptions.Compiled);//交错视频
        public static Regex regex音频信息 = new Regex(@"Stream #(?<map>\d+:\d+)(?<轨道码>\[0x[^]]+\])?(?:\((?<语言>\w+)\))?: Audio: (?<编码>[^,]+), (?<采样率>\d+ Hz), (?<声道>[^,]+)(?:, (?<位深>[^,]+))?(?:, (?<码率Kbps>\d+) kb/s[^,]*)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

       
    }
}
